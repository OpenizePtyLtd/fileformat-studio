using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExcelDataReader;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing.Engines.Excel
{
    /// <summary>
    /// Open-source Excel parser utilizing ExcelDataReader.
    /// Supports .xlsx and legacy .xls spreadsheets with multi-sheet traversal.
    /// </summary>
    public class ExcelDataReaderParser : IDocumentParser
    {
        public const string ParserEngineId = "exceldatareader";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx", ".xls"
        };

        static ExcelDataReaderParser()
        {
            // Register code pages encoding provider for legacy Excel binary encodings
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public DocumentCategory Category => DocumentCategory.Excel;

        public string EngineId => ParserEngineId;

        public string DisplayName => "ExcelDataReader (.NET)";

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
                throw new NotSupportedException($"ExcelDataReaderParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

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
            }, cancellationToken);
        }
    }
}

