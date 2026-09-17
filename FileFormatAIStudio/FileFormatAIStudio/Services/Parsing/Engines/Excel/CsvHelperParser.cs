using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing.Engines.Excel
{
    /// <summary>
    /// Delimited tabular data parser utilizing CsvHelper.
    /// Supports .csv and .tsv files with delimiter auto-detection and robust row parsing.
    /// </summary>
    public class CsvHelperParser : IDocumentParser
    {
        public const string ParserEngineId = "csvhelper";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".csv", ".tsv"
        };

        public DocumentCategory Category => DocumentCategory.Excel;

        public string EngineId => ParserEngineId;

        public string DisplayName => "CsvHelper (.NET)";

        public int Priority => 40;

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
                throw new NotSupportedException($"CsvHelperParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

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
            }, cancellationToken);
        }
    }
}

