using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing.Engines.Word
{
    /// <summary>
    /// Word processing document parser utilizing Aspose.Words for .NET.
    /// Supports .docx, .doc, .dot, .dotx, .rtf, .odt with full formatting preservation.
    /// </summary>
    public class AsposeWordsParser : IDocumentParser
    {
        public const string ParserEngineId = "aspose-words";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".doc", ".dot", ".dotx", ".rtf", ".odt"
        };

        private readonly IAsposeLicenseService? _licenseService;

        public AsposeWordsParser(IAsposeLicenseService? licenseService = null)
        {
            _licenseService = licenseService;
        }

        public DocumentCategory Category => DocumentCategory.Word;

        public string EngineId => ParserEngineId;

        public string DisplayName => "Aspose.Words (.NET)";

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
                throw new NotSupportedException($"AsposeWordsParser does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var doc = new Aspose.Words.Document(filePath);
                return doc.ToString(Aspose.Words.SaveFormat.Text);
            }, cancellationToken);
        }
    }
}

