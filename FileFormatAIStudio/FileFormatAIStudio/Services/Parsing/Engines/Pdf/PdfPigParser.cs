using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Ocr;
using UglyToad.PdfPig;

namespace FileFormatAIStudio.Services.Parsing.Engines.Pdf
{
    /// <summary>
    /// Open-source PDF document parser utilizing UglyToad.PdfPig.
    /// Extracts text page by page with high accuracy and integrates hybrid OCR for embedded figures and scanned pages.
    /// </summary>
    public class PdfPigParser : IDocumentParser
    {
        public const string ParserEngineId = "pdfpig";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf"
        };

        private readonly IOcrEngine? _ocrEngine;
        private readonly IImageExtractorService? _imageExtractor;

        public DocumentCategory Category => DocumentCategory.Pdf;

        public string EngineId => ParserEngineId;

        public string DisplayName => "PdfPig (.NET)";

        public int Priority => 50;

        public bool IsAvailable => true;

        public IReadOnlySet<string> SupportedExtensions => Extensions;

        public PdfPigParser(IOcrEngine? ocrEngine = null, IImageExtractorService? imageExtractor = null)
        {
            _ocrEngine = ocrEngine;
            _imageExtractor = imageExtractor;
        }

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

            return Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var document = PdfDocument.Open(filePath);
                var sb = new StringBuilder();

                foreach (var page in document.GetPages())
                {
                    cancellationToken.ThrowIfCancellationRequested();

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

                    // Hybrid per-page image OCR
                    if (_ocrEngine != null && _ocrEngine.IsAvailable && _imageExtractor != null)
                    {
                        try
                        {
                            var images = page.GetImages();
                            int imageIndex = 1;
                            foreach (var image in images)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                byte[]? imageBytes = null;
                                if (image.TryGetPng(out var pngBytes))
                                {
                                    imageBytes = pngBytes;
                                }
                                else if (!image.RawBytes.IsEmpty)
                                {
                                    imageBytes = image.RawBytes.ToArray();
                                }

                                if (imageBytes != null && imageBytes.Length > 0)
                                {
                                    var info = await _imageExtractor.InspectAndFilterAsync(
                                        imageBytes,
                                        pageNumber: page.Number,
                                        imageIndex: imageIndex,
                                        cancellationToken: cancellationToken);

                                    if (info.IsQualifyingForOcr)
                                    {
                                        var ocrResult = await _ocrEngine.RecognizeTextAsync(
                                            imageBytes,
                                            cancellationToken: cancellationToken);

                                        if (ocrResult.HasText)
                                        {
                                            sb.AppendLine();
                                            sb.AppendLine($"[Page {page.Number} Image {imageIndex} Text: {ocrResult.Text.Trim()}]");
                                        }
                                    }
                                }
                                imageIndex++;
                            }
                        }
                        catch
                        {
                            // Soft-fail for OCR to preserve base digital text extraction
                        }
                    }
                }

                return sb.ToString().Trim();
            }, cancellationToken);
        }
    }
}
