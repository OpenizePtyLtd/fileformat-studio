using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing.Engines.Word
{
    /// <summary>
    /// Open-source Word document parser utilizing Microsoft DocumentFormat.OpenXml SDK.
    /// Extracts paragraphs and table cell contents from modern .docx files.
    /// </summary>
    public class OpenXmlWordParser : IDocumentParser
    {
        public const string ParserEngineId = "openxml-words";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx"
        };

        public DocumentCategory Category => DocumentCategory.Word;

        public string EngineId => ParserEngineId;

        public string DisplayName => "DocumentFormat.OpenXml (.NET)";

        public int Priority => 50;

        public bool IsAvailable => true;

        public IReadOnlySet<string> SupportedExtensions => Extensions;

        public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !Extensions.Contains(ext))
            {
                throw new NotSupportedException($"OpenXmlWordParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

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
            }, cancellationToken);
        }
    }
}

