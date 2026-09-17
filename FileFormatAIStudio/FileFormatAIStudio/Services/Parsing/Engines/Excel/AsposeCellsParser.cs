using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing.Engines.Excel
{
    /// <summary>
    /// Spreadsheet document parser utilizing Aspose.Cells for .NET.
    /// Supports .xlsx, .xls, .xlsm, .xlsb, .ods, .csv with multi-sheet and cell structure extraction.
    /// </summary>
    public class AsposeCellsParser : IDocumentParser
    {
        public const string ParserEngineId = "aspose-cells";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx", ".xls", ".xlsm", ".xlsb", ".ods", ".csv"
        };

        private readonly IAsposeLicenseService? _licenseService;

        public AsposeCellsParser(IAsposeLicenseService? licenseService = null)
        {
            _licenseService = licenseService;
        }

        public DocumentCategory Category => DocumentCategory.Excel;

        public string EngineId => ParserEngineId;

        public string DisplayName => "Aspose.Cells (.NET)";

        public int Priority => 100;

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
                throw new NotSupportedException($"AsposeCellsParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var workbook = new Aspose.Cells.Workbook(filePath);
                var sb = new StringBuilder();

                for (int i = 0; i < workbook.Worksheets.Count; i++)
                {
                    var sheet = workbook.Worksheets[i];
                    if (!sheet.IsVisible)
                        continue;

                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine();
                    }

                    sb.AppendLine($"--- Sheet: {sheet.Name} ---");

                    int maxRow = sheet.Cells.MaxDataRow;
                    int maxCol = sheet.Cells.MaxDataColumn;
                    if (maxRow < 0 || maxCol < 0)
                        continue;

                    for (int r = 0; r <= maxRow; r++)
                    {
                        var rowValues = new List<string>(maxCol + 1);
                        bool rowHasData = false;

                        for (int c = 0; c <= maxCol; c++)
                        {
                            var cell = sheet.Cells.CheckCell(r, c);
                            var val = cell?.StringValue?.Trim() ?? string.Empty;
                            if (!string.IsNullOrEmpty(val))
                                rowHasData = true;

                            rowValues.Add(val);
                        }

                        if (rowHasData)
                        {
                            sb.AppendLine(string.Join("\t", rowValues));
                        }
                    }
                }

                return sb.ToString();
            }, cancellationToken);
        }
    }
}

