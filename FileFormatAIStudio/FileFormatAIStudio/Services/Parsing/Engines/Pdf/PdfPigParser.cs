using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;
using UglyToad.PdfPig;

namespace FileFormatAIStudio.Services.Parsing.Engines.Pdf
{
    /// <summary>
    /// Open-source PDF document parser utilizing UglyToad.PdfPig.
    /// Extracts text page by page with high accuracy.
    /// </summary>
    public class PdfPigParser : IDocumentParser
    {
        public const string ParserEngineId = "pdfpig";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf"
        };

        public DocumentCategory Category => DocumentCategory.Pdf;

        public string EngineId => ParserEngineId;

        public string DisplayName => "PdfPig (.NET)";

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
                throw new NotSupportedException($"PdfPigParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

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
            }, cancellationToken);
        }
    }
}

