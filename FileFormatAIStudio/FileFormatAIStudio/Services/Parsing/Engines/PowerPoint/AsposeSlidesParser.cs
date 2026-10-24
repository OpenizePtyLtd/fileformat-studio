using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Ocr;

namespace FileFormatAIStudio.Services.Parsing.Engines.PowerPoint
{
    /// <summary>
    /// Presentation document parser utilizing Aspose.Slides for .NET.
    /// Supports .pptx, .ppt, .pps, .ppsx, .odp with slide-by-slide text, shape, speaker notes, and embedded image OCR.
    /// </summary>
    public class AsposeSlidesParser : IDocumentParser
    {
        public const string ParserEngineId = "aspose-slides";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pptx", ".ppt", ".pps", ".ppsx", ".odp"
        };

        private readonly IAsposeLicenseService? _licenseService;
        private readonly IOcrEngine? _ocrEngine;
        private readonly IImageExtractorService? _imageExtractor;

        public AsposeSlidesParser(
            IAsposeLicenseService? licenseService = null,
            IOcrEngine? ocrEngine = null,
            IImageExtractorService? imageExtractor = null)
        {
            _licenseService = licenseService;
            _ocrEngine = ocrEngine;
            _imageExtractor = imageExtractor;
        }

        public DocumentCategory Category => DocumentCategory.PowerPoint;

        public string EngineId => ParserEngineId;

        public string DisplayName => "Aspose.Slides (.NET)";

        public int Priority => _licenseService?.IsSlidesLicensed == true ? 100 : 20;

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
                throw new NotSupportedException($"AsposeSlidesParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var pres = new Aspose.Slides.Presentation(filePath);
                var sb = new StringBuilder();

                for (int i = 0; i < pres.Slides.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var slide = pres.Slides[i];

                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine();
                    }

                    sb.AppendLine($"--- Slide {i + 1} ---");

                    // Extract text from slide shapes
                    foreach (var shape in slide.Shapes)
                    {
                        if (shape is Aspose.Slides.IAutoShape autoShape && autoShape.TextFrame != null)
                        {
                            var text = autoShape.TextFrame.Text?.Trim();
                            if (!string.IsNullOrEmpty(text))
                            {
                                sb.AppendLine(text);
                            }
                        }
                        else if (shape is Aspose.Slides.ITable table)
                        {
                            for (int r = 0; r < table.Rows.Count; r++)
                            {
                                var cellTexts = new List<string>();
                                for (int c = 0; c < table.Columns.Count; c++)
                                {
                                    var cellText = table[c, r].TextFrame?.Text?.Trim();
                                    if (!string.IsNullOrEmpty(cellText))
                                        cellTexts.Add(cellText);
                                }

                                if (cellTexts.Count > 0)
                                {
                                    sb.AppendLine(string.Join("\t", cellTexts));
                                }
                            }
                        }
                    }

                    // Extract speaker notes
                    var notesSlide = slide.NotesSlideManager?.NotesSlide;
                    if (notesSlide?.NotesTextFrame != null)
                    {
                        var notesText = notesSlide.NotesTextFrame.Text?.Trim();
                        if (!string.IsNullOrEmpty(notesText))
                        {
                            sb.AppendLine();
                            sb.AppendLine($"[Notes: {notesText}]");
                        }
                    }

                    // Hybrid embedded image OCR
                    if (_ocrEngine != null && _ocrEngine.IsAvailable && _imageExtractor != null)
                    {
                        try
                        {
                            int imgIndex = 1;
                            foreach (Aspose.Slides.IShape shape in slide.Shapes)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (shape is Aspose.Slides.IPictureFrame pf && pf.PictureFormat?.Picture?.Image != null)
                                {
                                    var bytes = pf.PictureFormat.Picture.Image.BinaryData;
                                    if (bytes != null && bytes.Length > 0)
                                    {
                                        var info = await _imageExtractor.InspectAndFilterAsync(
                                            bytes,
                                            pageNumber: i + 1,
                                            imageIndex: imgIndex,
                                            cancellationToken: cancellationToken);

                                        if (info.IsQualifyingForOcr)
                                        {
                                            var ocrResult = await _ocrEngine.RecognizeTextAsync(bytes, cancellationToken: cancellationToken);
                                            if (ocrResult.HasText)
                                            {
                                                sb.AppendLine();
                                                sb.AppendLine($"[Slide {i + 1} Image {imgIndex} Text: {ocrResult.Text.Trim()}]");
                                            }
                                        }
                                        imgIndex++;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Non-fatal
                        }
                    }
                }

                return sb.ToString().Trim();
            }, cancellationToken);
        }
    }
}

