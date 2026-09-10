using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Parsing
{
    /// <summary>
    /// Contract for document parsers capable of extracting plain text from supported file formats.
    /// Concrete implementations encapsulate engine-specific mechanics (e.g. Aspose, Node.js officeparser, open-source .NET).
    /// </summary>
    public interface IDocumentParser
    {
        /// <summary>
        /// Unique identifier for the parser engine (e.g. "aspose", "officeparser", "dotnet-oss", "plaintext").
        /// </summary>
        string EngineId { get; }

        /// <summary>
        /// User-friendly display name of the parser engine.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Priority ranking of the engine when multiple parsers support the same format.
        /// Higher number indicates higher priority (e.g. 100 for Aspose, 60 for OfficeParser, 10 for PlainText).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Indicates whether the engine is currently available and ready to run on the system
        /// (e.g. required runtimes, native binaries, or dependencies are present).
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Set of supported file extensions, normalized in lower-case with leading dot (e.g. ".docx", ".pdf", ".txt").
        /// </summary>
        IReadOnlySet<string> SupportedExtensions { get; }

        /// <summary>
        /// Extracts raw plain text from the specified document file.
        /// </summary>
        /// <param name="filePath">Full path to the document file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Extracted plain text content from the document.</returns>
        Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default);
    }
}

