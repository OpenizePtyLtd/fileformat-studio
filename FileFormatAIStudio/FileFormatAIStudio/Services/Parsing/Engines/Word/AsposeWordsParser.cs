using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Ocr;

namespace FileFormatAIStudio.Services.Parsing.Engines.Word
{
    /// <summary>
    /// Word processing document parser utilizing Aspose.Words for .NET.
    /// Supports .docx, .doc, .dot, .dotx, .rtf, .odt with full formatting preservation and embedded image OCR.
    /// </summary>
    public class AsposeWordsParser : IDocumentParser
    {
        public const string ParserEngineId = "aspose-words";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".doc", ".dot", ".dotx", ".rtf", ".odt"
        };

        private readonly IAsposeLicenseService? _licenseService;
        private readonly IOcrEngine? _ocrEngine;
        private readonly IImageExtractorService? _imageExtractor;

        public AsposeWordsParser(
            IAsposeLicenseService? licenseService = null,
            IOcrEngine? ocrEngine = null,
            IImageExtractorService? imageExtractor = null)
        {
            _licenseService = licenseService;
            _ocrEngine = ocrEngine;
            _imageExtractor = imageExtractor;
        }

        public DocumentCategory Category => DocumentCategory.Word;

        public string EngineId => ParserEngineId;

        public string DisplayName => "Aspose.Words (.NET)";

        public int Priority => _licenseService?.IsWordsLicensed == true ? 100 : 20;

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
                throw new NotSupportedException($"AsposeWordsParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var doc = new Aspose.Words.Document(filePath);
                var text = doc.ToString(Aspose.Words.SaveFormat.Text) ?? string.Empty;
                var sb = new StringBuilder(text.Trim());

                // Hybrid embedded image OCR
                if (_ocrEngine != null && _ocrEngine.IsAvailable && _imageExtractor != null)
                {
                    try
                    {
                        var shapes = doc.GetChildNodes(Aspose.Words.NodeType.Shape, true);
                        int imgIndex = 1;
                        foreach (Aspose.Words.Drawing.Shape shape in shapes)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (shape.HasImage && shape.ImageData?.ImageBytes != null && shape.ImageData.ImageBytes.Length > 0)
                            {
                                var bytes = shape.ImageData.ImageBytes;
                                var info = await _imageExtractor.InspectAndFilterAsync(
                                    bytes,
                                    pageNumber: 1,
                                    imageIndex: imgIndex,
                                    cancellationToken: cancellationToken);

                                if (info.IsQualifyingForOcr)
                                {
                                    var ocrResult = await _ocrEngine.RecognizeTextAsync(bytes, cancellationToken: cancellationToken);
                                    if (ocrResult.HasText)
                                    {
                                        if (sb.Length > 0)
                                        {
                                            sb.AppendLine();
                                            sb.AppendLine();
                                        }
                                        sb.AppendLine($"[Embedded Image {imgIndex} Text: {ocrResult.Text.Trim()}]");
                                    }
                                }
                                imgIndex++;
                            }
                        }
                    }
                    catch
                    {
                        // Non-fatal
                    }
                }

                return sb.ToString().Trim();
            }, cancellationToken);
        }
    }
}

