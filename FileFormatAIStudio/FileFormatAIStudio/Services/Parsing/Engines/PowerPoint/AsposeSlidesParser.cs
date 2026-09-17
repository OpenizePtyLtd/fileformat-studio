using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing.Engines.PowerPoint
{
    /// <summary>
    /// Presentation document parser utilizing Aspose.Slides for .NET.
    /// Supports .pptx, .ppt, .pps, .ppsx, .odp with slide-by-slide text, shape, and speaker notes extraction.
    /// </summary>
    public class AsposeSlidesParser : IDocumentParser
    {
        public const string ParserEngineId = "aspose-slides";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pptx", ".ppt", ".pps", ".ppsx", ".odp"
        };

        private readonly IAsposeLicenseService? _licenseService;

        public AsposeSlidesParser(IAsposeLicenseService? licenseService = null)
        {
            _licenseService = licenseService;
        }

        public DocumentCategory Category => DocumentCategory.PowerPoint;

        public string EngineId => ParserEngineId;

        public string DisplayName => "Aspose.Slides (.NET)";

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
                throw new NotSupportedException($"AsposeSlidesParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var pres = new Aspose.Slides.Presentation(filePath);
                var sb = new StringBuilder();

                for (int i = 0; i < pres.Slides.Count; i++)
                {
                    var slide = pres.Slides[i];

                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine();
                    }

                    sb.AppendLine($"--- Slide {i + 1} ---");

                    var textFrames = Aspose.Slides.Util.SlideUtil.GetAllTextBoxes(slide);
                    if (textFrames != null)
                    {
                        foreach (var tf in textFrames)
                        {
                            var text = tf.Text?.Trim();
                            if (!string.IsNullOrEmpty(text))
                            {
                                sb.AppendLine(text);
                            }
                        }
                    }

                    // Append speaker notes if present
                    var notesSlide = slide.NotesSlideManager?.NotesSlide;
                    if (notesSlide?.NotesTextFrame != null)
                    {
                        var notesText = notesSlide.NotesTextFrame.Text?.Trim();
                        if (!string.IsNullOrEmpty(notesText))
                        {
                            sb.AppendLine($"[Speaker Notes: {notesText}]");
                        }
                    }
                }

                return sb.ToString();
            }, cancellationToken);
        }
    }
}

