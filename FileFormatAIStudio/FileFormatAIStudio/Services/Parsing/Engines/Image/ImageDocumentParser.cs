using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Ocr;

namespace FileFormatAIStudio.Services.Parsing.Engines.Image
{
    /// <summary>
    /// Document parser for standalone raster image files (.png, .jpg, .jpeg, .bmp, .tiff, .tif, .webp) utilizing Windows Native OCR.
    /// Allows image files containing scanned receipts, contracts, and infographics to be ingested directly into knowledgebases.
    /// </summary>
    public class ImageDocumentParser : IDocumentParser
    {
        public const string ParserEngineId = "windows-ocr-image";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".webp"
        };

        private readonly IOcrEngine? _ocrEngine;
        private readonly IImageExtractorService? _imageExtractor;

        public DocumentCategory Category => DocumentCategory.Image;

        public string EngineId => ParserEngineId;

        public string DisplayName => "Windows Native OCR (Image)";

        public int Priority => 100;

        public bool IsAvailable => _ocrEngine?.IsAvailable ?? true;

        public IReadOnlySet<string> SupportedExtensions => Extensions;

        public ImageDocumentParser(IOcrEngine? ocrEngine = null, IImageExtractorService? imageExtractor = null)
        {
            _ocrEngine = ocrEngine;
            _imageExtractor = imageExtractor;
        }

        public async Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !Extensions.Contains(ext))
            {
                throw new NotSupportedException($"ImageDocumentParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            if (_ocrEngine == null || !_ocrEngine.IsAvailable)
            {
                throw new InvalidOperationException("Windows OCR engine is not available on this system.");
            }

            byte[] imageBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

            if (_imageExtractor != null)
            {
                var info = await _imageExtractor.InspectAndFilterAsync(
                    imageBytes,
                    pageNumber: 1,
                    imageIndex: 1,
                    cancellationToken: cancellationToken);

                if (!info.IsQualifyingForOcr)
                {
                    // Image was identified as an icon, bullet, or decorative border
                    return string.Empty;
                }
            }

            var result = await _ocrEngine.RecognizeTextAsync(imageBytes, cancellationToken: cancellationToken);
            return result.Text ?? string.Empty;
        }
    }
}

