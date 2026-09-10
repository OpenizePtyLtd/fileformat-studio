using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ExcelDataReader;
using UglyToad.PdfPig;

namespace FileFormatAIStudio.Services.Parsing
{
    /// <summary>
    /// Document parser utilizing 100% free and open-source .NET libraries
    /// (DocumentFormat.OpenXml, UglyToad.PdfPig, ExcelDataReader, CsvHelper)
    /// to extract structured plain text without external runtimes or commercial licenses.
    /// </summary>
    public class DotNetOssDocumentParser : IDocumentParser
    {
        public const string ParserEngineId = "dotnet-oss";

        private static readonly HashSet<string> WordExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx"
        };

        private static readonly HashSet<string> ExcelExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx", ".xls"
        };

        private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf"
        };

        private static readonly HashSet<string> DelimitedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".csv", ".tsv"
        };

        private static readonly HashSet<string> PlainTextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".markdown", ".json"
        };

        private static readonly HashSet<string> AllSupportedExtensions;

        static DotNetOssDocumentParser()
        {
            // Register code pages encoding provider for ExcelDataReader legacy encodings
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            AllSupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AllSupportedExtensions.UnionWith(WordExtensions);
            AllSupportedExtensions.UnionWith(ExcelExtensions);
            AllSupportedExtensions.UnionWith(PdfExtensions);
            AllSupportedExtensions.UnionWith(DelimitedExtensions);
            AllSupportedExtensions.UnionWith(PlainTextExtensions);
        }

        public string EngineId => ParserEngineId;

        public string DisplayName => "Open-Source .NET Parsers (OpenXML, PdfPig, ExcelDataReader)";

        public int Priority => 50;

        public bool IsAvailable => true;

        public IReadOnlySet<string> SupportedExtensions => AllSupportedExtensions;

        public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !AllSupportedExtensions.Contains(ext))
            {
                throw new NotSupportedException($"DotNetOssDocumentParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (WordExtensions.Contains(ext))
                {
                    return ExtractWordText(filePath);
                }
                else if (PdfExtensions.Contains(ext))
                {
                    return ExtractPdfText(filePath);
                }
                else if (ExcelExtensions.Contains(ext))
                {
                    return ExtractExcelText(filePath);
                }
                else if (DelimitedExtensions.Contains(ext))
                {
                    return ExtractDelimitedText(filePath, ext);
                }
                else if (PlainTextExtensions.Contains(ext))
                {
                    return File.ReadAllText(filePath, Encoding.UTF8);
                }

                throw new NotSupportedException($"Unsupported extension: {ext}");
            }, cancellationToken);
        }

        private static string ExtractWordText(string filePath)
        {
            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var element in body.Elements())
            {
                if (element is Paragraph p)
                {
                    var text = p.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (sb.Length > 0)
                            sb.AppendLine();
                        sb.AppendLine(text);
                    }
                }
                else if (element is Table table)
                {
                    if (sb.Length > 0)
                        sb.AppendLine();

                    foreach (var row in table.Elements<TableRow>())
                    {
                        var cellTexts = new List<string>();
                        bool rowHasData = false;

                        foreach (var cell in row.Elements<TableCell>())
                        {
                            var cellVal = cell.InnerText?.Trim() ?? string.Empty;
                            if (!string.IsNullOrEmpty(cellVal))
                                rowHasData = true;
                            cellTexts.Add(cellVal);
                        }

                        if (rowHasData)
                        {
                            sb.AppendLine(string.Join(" | ", cellTexts));
                        }
                    }
                }
            }

            return sb.ToString().Trim();
        }

        private static string ExtractPdfText(string filePath)
        {
            using var document = PdfDocument.Open(filePath);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                }

                sb.AppendLine($"--- Page {page.Number} ---");
                var text = page.Text?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    sb.AppendLine(text);
                }
            }

            return sb.ToString().Trim();
        }

        private static string ExtractExcelText(string filePath)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);

            var sb = new StringBuilder();

            do
            {
                var sheetName = reader.Name;

                if (sb.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                }

                sb.AppendLine($"--- Sheet: {sheetName} ---");

                while (reader.Read())
                {
                    var rowValues = new List<string>();
                    bool rowHasData = false;

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var val = reader.GetValue(i)?.ToString()?.Trim() ?? string.Empty;
                        if (!string.IsNullOrEmpty(val))
                            rowHasData = true;

                        rowValues.Add(val);
                    }

                    if (rowHasData)
                    {
                        sb.AppendLine(string.Join(" | ", rowValues));
                    }
                }

            } while (reader.NextResult());

            return sb.ToString().Trim();
        }

        private static string ExtractDelimitedText(string filePath, string ext)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = string.Equals(ext, ".tsv", StringComparison.OrdinalIgnoreCase) ? "\t" : ",",
                MissingFieldFound = null,
                BadDataFound = null,
                HeaderValidated = null
            };

            using var reader = new StreamReader(filePath, Encoding.UTF8);
            using var csv = new CsvParser(reader, config);

            var sb = new StringBuilder();

            while (csv.Read())
            {
                if (csv.Record == null || csv.Record.Length == 0)
                    continue;

                bool rowHasData = false;
                var rowValues = new List<string>(csv.Record.Length);

                foreach (var field in csv.Record)
                {
                    var val = field?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(val))
                        rowHasData = true;

                    rowValues.Add(val);
                }

                if (rowHasData)
                {
                    if (sb.Length > 0)
                        sb.AppendLine();

                    sb.Append(string.Join(" | ", rowValues));
                }
            }

            return sb.ToString().Trim();
        }
    }
}

