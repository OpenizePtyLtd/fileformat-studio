using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing.Engines.Pdf
{
    /// <summary>
    /// PDF document parser utilizing Aspose.PDF for .NET.
    /// Extracts structured text using TextAbsorber.
    /// </summary>
    public class AsposePdfParser : IDocumentParser
    {
        public const string ParserEngineId = "aspose-pdf";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf"
        };

        private readonly IAsposeLicenseService? _licenseService;

        public AsposePdfParser(IAsposeLicenseService? licenseService = null)
        {
            _licenseService = licenseService;
        }

        public DocumentCategory Category => DocumentCategory.Pdf;

        public string EngineId => ParserEngineId;

        public string DisplayName => "Aspose.PDF (.NET)";

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
                throw new NotSupportedException($"AsposePdfParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var pdfDoc = new Aspose.Pdf.Document(filePath);
                var textAbsorber = new Aspose.Pdf.Text.TextAbsorber();
                pdfDoc.Pages.Accept(textAbsorber);
                return textAbsorber.Text ?? string.Empty;
            }, cancellationToken);
        }
    }
}

