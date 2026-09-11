using System.Collections.Generic;

namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Registry providing metadata, category taxonomy, and format lookup for benchmarked documents.
    /// </summary>
    public interface IDocumentCategoryRegistry
    {
        /// <summary>
        /// Gets all defined document categories.
        /// </summary>
        IReadOnlyList<DocumentCategory> GetAllCategories();

        /// <summary>
        /// Returns a human-friendly display name for the specified category.
        /// </summary>
        string GetCategoryDisplayName(DocumentCategory category);

        /// <summary>
        /// Gets all registered document format descriptors.
        /// </summary>
        IReadOnlyList<DocumentFormatDescriptor> GetAllFormats();

        /// <summary>
        /// Gets registered document format descriptors filtered by a specific category.
        /// </summary>
        IReadOnlyList<DocumentFormatDescriptor> GetFormatsByCategory(DocumentCategory category);

        /// <summary>
        /// Retrieves the descriptor for a specific file path or extension, or null if unknown.
        /// </summary>
        DocumentFormatDescriptor? GetFormatDescriptor(string filePathOrExtension);

        /// <summary>
        /// Resolves the document category for a file path or extension.
        /// Falls back to PlainText if the extension is unknown or text-like.
        /// </summary>
        DocumentCategory ResolveCategory(string filePathOrExtension);

        /// <summary>
        /// Checks whether the specified file path or extension is supported by the registry.
        /// </summary>
        bool IsSupported(string filePathOrExtension);

        /// <summary>
        /// Gets all supported file extensions, optionally filtered by category.
        /// </summary>
        IReadOnlySet<string> GetSupportedExtensions(DocumentCategory? category = null);
    }
}

