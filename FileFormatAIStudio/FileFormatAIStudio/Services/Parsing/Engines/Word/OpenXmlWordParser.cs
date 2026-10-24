using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Ocr;

namespace FileFormatAIStudio.Services.Parsing.Engines.Word
{
    /// <summary>
    /// Open-source Word document parser utilizing Microsoft DocumentFormat.OpenXml SDK.
    /// Extracts paragraphs, table cell contents, and embedded image OCR text from modern .docx files.
    /// </summary>
    public class OpenXmlWordParser : IDocumentParser
    {
        public const string ParserEngineId = "openxml-words";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx"
        };

        private readonly IOcrEngine? _ocrEngine;
        private readonly IImageExtractorService? _imageExtractor;

        public DocumentCategory Category => DocumentCategory.Word;

        public string EngineId => ParserEngineId;

        public string DisplayName => "DocumentFormat.OpenXml (.NET)";

        public int Priority => 50;

        public bool IsAvailable => true;

        public IReadOnlySet<string> SupportedExtensions => Extensions;

        public OpenXmlWordParser(IOcrEngine? ocrEngine = null, IImageExtractorService? imageExtractor = null)
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
                throw new NotSupportedException($"OpenXmlWordParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(async () =>
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
                        foreach (var row in table.Elements<TableRow>())
                        {
                            var cellTexts = new List<string>();
                            foreach (var cell in row.Elements<TableCell>())
                            {
                                var cellText = cell.InnerText?.Trim();
                                if (!string.IsNullOrEmpty(cellText))
                                    cellTexts.Add(cellText);
                            }

                            if (cellTexts.Count > 0)
                            {
                                if (sb.Length > 0)
                                    sb.AppendLine();
                                sb.AppendLine(string.Join("\t", cellTexts));
                            }
                        }
                    }
                }

                // Hybrid embedded image OCR
                if (_ocrEngine != null && _ocrEngine.IsAvailable && _imageExtractor != null && doc.MainDocumentPart?.ImageParts != null)
                {
                    try
                    {
                        int imgIndex = 1;
                        foreach (var imagePart in doc.MainDocumentPart.ImageParts)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            using var stream = imagePart.GetStream();
                            using var ms = new MemoryStream();
                            await stream.CopyToAsync(ms, cancellationToken);
                            var bytes = ms.ToArray();

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

