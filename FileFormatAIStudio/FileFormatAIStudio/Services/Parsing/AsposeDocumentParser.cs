using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Parsing
{
    /// <summary>
    /// Document parser utilizing Aspose .NET libraries (Words, Cells, Slides, PDF)
    /// to extract high-fidelity structured text and metadata from documents.
    /// </summary>
    public class AsposeDocumentParser : IDocumentParser
    {
        public const string ParserEngineId = "aspose";

        private static readonly HashSet<string> WordExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".doc", ".dot", ".dotx", ".rtf", ".odt"
        };

        private static readonly HashSet<string> ExcelExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx", ".xls", ".xlsm", ".xlsb", ".ods", ".csv"
        };

        private static readonly HashSet<string> SlideExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pptx", ".ppt", ".pps", ".ppsx", ".odp"
        };

        private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf"
        };

        private static readonly HashSet<string> AllSupportedExtensions;

        static AsposeDocumentParser()
        {
            AllSupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AllSupportedExtensions.UnionWith(WordExtensions);
            AllSupportedExtensions.UnionWith(ExcelExtensions);
            AllSupportedExtensions.UnionWith(SlideExtensions);
            AllSupportedExtensions.UnionWith(PdfExtensions);
        }

        private readonly IAsposeLicenseService? _licenseService;

        public AsposeDocumentParser(IAsposeLicenseService? licenseService = null)
        {
            _licenseService = licenseService;
        }

        public string EngineId => ParserEngineId;

        public string DisplayName => "Aspose Document Engine (.NET)";

        public int Priority => 100;

        public bool IsAvailable => true;

        public IReadOnlySet<string> SupportedExtensions => AllSupportedExtensions;

        public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !AllSupportedExtensions.Contains(ext))
            {
                throw new NotSupportedException($"AsposeDocumentParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            // Execute parser on background thread pool to keep UI responsive
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (WordExtensions.Contains(ext))
                {
                    return ExtractWordText(filePath);
                }
                else if (ExcelExtensions.Contains(ext))
                {
                    return ExtractExcelText(filePath);
                }
                else if (SlideExtensions.Contains(ext))
                {
                    return ExtractSlidesText(filePath);
                }
                else if (PdfExtensions.Contains(ext))
                {
                    return ExtractPdfText(filePath);
                }

                throw new NotSupportedException($"Unsupported extension: {ext}");
            }, cancellationToken);
        }

        private static string ExtractWordText(string filePath)
        {
            var doc = new Aspose.Words.Document(filePath);
            return doc.ToString(Aspose.Words.SaveFormat.Text);
        }

        private static string ExtractExcelText(string filePath)
        {
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
        }

        private static string ExtractSlidesText(string filePath)
        {
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
        }

        private static string ExtractPdfText(string filePath)
        {
            using var pdfDoc = new Aspose.Pdf.Document(filePath);
            var textAbsorber = new Aspose.Pdf.Text.TextAbsorber();
            pdfDoc.Pages.Accept(textAbsorber);
            return textAbsorber.Text ?? string.Empty;
        }
    }
}

