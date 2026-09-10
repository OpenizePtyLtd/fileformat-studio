using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Parsing
{
    /// <summary>
    /// Factory for discovering, resolving, and invoking document parsers across different engines.
    /// </summary>
    public interface IDocumentParserFactory
    {
        /// <summary>
        /// Gets all registered document parsers.
        /// </summary>
        IReadOnlyList<IDocumentParser> GetAllParsers();

        /// <summary>
        /// Gets a specific parser by its unique EngineId (case-insensitive), or null if not found.
        /// </summary>
        IDocumentParser? GetParser(string engineId);

        /// <summary>
        /// Resolves the parser for a file path or extension.
        /// If engineId is specified, attempts to find that specific engine and verifies it supports the extension.
        /// If engineId is null or empty, selects the highest-priority available parser that supports the extension.
        /// </summary>
        /// <param name="filePathOrExtension">File path (e.g. "C:\doc.docx") or extension (e.g. ".docx" or "docx").</param>
        /// <param name="engineId">Optional engine identifier to explicitly select.</param>
        /// <returns>Matching parser, or null if none is available or supports the format.</returns>
        IDocumentParser? ResolveParser(string filePathOrExtension, string? engineId = null);

        /// <summary>
        /// Gets all unique file extensions supported across currently available parsers.
        /// </summary>
        IReadOnlySet<string> GetSupportedExtensions();

        /// <summary>
        /// High-level convenience method: resolves the appropriate parser and extracts plain text from the file.
        /// Throws NotSupportedException if no parser supports the file, or InvalidOperationException if the specified engine is missing/unavailable.
        /// </summary>
        /// <param name="filePath">Full path to the document file.</param>
        /// <param name="engineId">Optional specific parser engine to use (e.g. "aspose", "officeparser"). If null, auto-selects best available.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Extracted plain text from the document.</returns>
        Task<string> ExtractTextAsync(string filePath, string? engineId = null, CancellationToken cancellationToken = default);
    }
}

