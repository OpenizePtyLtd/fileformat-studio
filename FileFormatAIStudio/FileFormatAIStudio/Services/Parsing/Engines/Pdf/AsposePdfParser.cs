using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Ocr;

namespace FileFormatAIStudio.Services.Parsing.Engines.Pdf
{
    /// <summary>
    /// Aspose.PDF document parser.
    /// Extracts text with high fidelity, structural layout, and hybrid OCR for embedded images.
    /// Priority is dynamically elevated when an active Aspose license is loaded.
    /// </summary>
    public class AsposePdfParser : IDocumentParser
    {
        public const string ParserEngineId = "aspose-pdf";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf"
        };

        private readonly IAsposeLicenseService? _licenseService;
        private readonly IOcrEngine? _ocrEngine;
        private readonly IImageExtractorService? _imageExtractor;

        public DocumentCategory Category => DocumentCategory.Pdf;

        public string EngineId => ParserEngineId;

        public string DisplayName => "Aspose.PDF (.NET)";

        public int Priority => _licenseService?.IsPdfLicensed == true ? 100 : 20;

        public bool IsAvailable => true;

        public IReadOnlySet<string> SupportedExtensions => Extensions;

        public AsposePdfParser(
            IAsposeLicenseService? licenseService = null,
            IOcrEngine? ocrEngine = null,
            IImageExtractorService? imageExtractor = null)
        {
            _licenseService = licenseService;
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
                throw new NotSupportedException($"AsposePdfParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var pdfDoc = new Aspose.Pdf.Document(filePath);
                var textAbsorber = new Aspose.Pdf.Text.TextAbsorber();
                pdfDoc.Pages.Accept(textAbsorber);
                var mainText = textAbsorber.Text ?? string.Empty;

                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(mainText))
                {
                    sb.Append(mainText.Trim());
                }

                if (_ocrEngine != null && _ocrEngine.IsAvailable && _imageExtractor != null)
                {
                    try
                    {
                        int pageNum = 1;
                        foreach (Aspose.Pdf.Page page in pdfDoc.Pages)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (page.Resources?.Images != null && page.Resources.Images.Count > 0)
                            {
                                int imgIndex = 1;
                                foreach (Aspose.Pdf.XImage image in page.Resources.Images)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    using var ms = new MemoryStream();
                                    image.Save(ms);
                                    var imageBytes = ms.ToArray();

                                    var info = await _imageExtractor.InspectAndFilterAsync(
                                        imageBytes,
                                        pageNumber: pageNum,
                                        imageIndex: imgIndex,
                                        cancellationToken: cancellationToken);

                                    if (info.IsQualifyingForOcr)
                                    {
                                        var ocrResult = await _ocrEngine.RecognizeTextAsync(
                                            imageBytes,
                                            cancellationToken: cancellationToken);

                                        if (ocrResult.HasText)
                                        {
                                            if (sb.Length > 0)
                                            {
                                                sb.AppendLine();
                                                sb.AppendLine();
                                            }
                                            sb.AppendLine($"[Page {pageNum} Image {imgIndex} Text: {ocrResult.Text.Trim()}]");
                                        }
                                    }
                                    imgIndex++;
                                }
                            }
                            pageNum++;
                        }
                    }
                    catch
                    {
                        // Soft-fail for OCR to safeguard baseline text
                    }
                }

                return sb.ToString().Trim();
            }, cancellationToken);
        }
    }
}

