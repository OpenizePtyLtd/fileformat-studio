using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing
{
    /// <summary>
    /// Service for managing user-configured document engine preferences per document category
    /// (Word, Excel, PowerPoint, PDF, PlainText) with automatic resolution to benchmark winners.
    /// </summary>
    public interface IDocumentEnginePreferenceService
    {
        /// <summary>
        /// Gets the configured preferred engine ID for a specific category (e.g. "Auto", "aspose-words", "openxml-words").
        /// Defaults to "Auto".
        /// </summary>
        Task<string> GetPreferredEngineIdAsync(DocumentCategory category, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the configured preferred engine ID for a specific category.
        /// </summary>
        Task SetPreferredEngineIdAsync(DocumentCategory category, string engineId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all configured preferences mapped by DocumentCategory.
        /// </summary>
        Task<IReadOnlyDictionary<DocumentCategory, string>> GetAllPreferencesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves the concrete parser engine to parse the given document file based on its category
        /// and the user's preferred engine or benchmark winner.
        /// </summary>
        /// <param name="filePath">Full path or filename of the document.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Resolved parser engine, or null if no available engine supports the file.</returns>
        Task<IDocumentParser?> ResolveParserForDocumentAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the latest recorded benchmark overall winner engine ID for a category, or null if none is recorded.
        /// </summary>
        Task<string?> GetBenchmarkWinnerEngineIdAsync(DocumentCategory category, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the latest recorded benchmark overall winner display name for a category, or null if none is recorded.
        /// </summary>
        Task<string?> GetBenchmarkWinnerDisplayNameAsync(DocumentCategory category, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records the latest benchmark winner for a specific document category.
        /// </summary>
        Task RecordBenchmarkWinnerAsync(DocumentCategory category, string engineId, string displayName, CancellationToken cancellationToken = default);
    }
}

