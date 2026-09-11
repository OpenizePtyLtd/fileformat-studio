using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using Microsoft.Extensions.AI;

namespace FileFormatAIStudio.Services.Knowledgebase
{
    /// <summary>
    /// Parameters required to create a new Knowledgebase.
    /// </summary>
    public sealed record CreateKnowledgebaseRequest(
        string Name,
        string Description = "",
        string ParserEngine = "Auto",
        string EmbeddingProvider = "",
        string EmbeddingModel = "",
        int VectorDimensions = 1536);

    /// <summary>
    /// Parameters to update an existing Knowledgebase.
    /// </summary>
    public sealed record UpdateKnowledgebaseRequest(
        string Name,
        string Description = "",
        string? ParserEngine = null);

    /// <summary>
    /// Configuration options for document ingestion and embedding generation.
    /// </summary>
    public sealed record IngestionOptions
    {
        /// <summary>
        /// Custom chunking options (target tokens, overlap, etc.).
        /// </summary>
        public ChunkingOptions? Chunking { get; init; }

        /// <summary>
        /// Maximum number of text chunks to embed in a single batch request (default: 16).
        /// </summary>
        public int EmbeddingBatchSize { get; init; } = 16;

        /// <summary>
        /// Optional explicit embedding generator instance (useful for unit testing or custom decorators).
        /// </summary>
        public IEmbeddingGenerator<string, Embedding<float>>? CustomEmbeddingGenerator { get; init; }
    }

    /// <summary>
    /// Service contract for managing knowledgebases, documents, and the multi-file ingestion pipeline.
    /// </summary>
    public interface IKnowledgebaseService
    {
        /// <summary>
        /// Retrieves all knowledgebases ordered by most recently updated.
        /// </summary>
        Task<List<KnowledgebaseEntity>> GetKnowledgebasesAsync(CancellationToken ct = default);

        /// <summary>
        /// Retrieves a knowledgebase by ID, including its associated documents.
        /// </summary>
        Task<KnowledgebaseEntity?> GetKnowledgebaseByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Creates a new knowledgebase.
        /// </summary>
        Task<KnowledgebaseEntity> CreateKnowledgebaseAsync(CreateKnowledgebaseRequest request, CancellationToken ct = default);

        /// <summary>
        /// Updates the name, description, or parser engine of an existing knowledgebase.
        /// </summary>
        Task<KnowledgebaseEntity> UpdateKnowledgebaseAsync(Guid id, UpdateKnowledgebaseRequest request, CancellationToken ct = default);

        /// <summary>
        /// Deletes a knowledgebase, its stored document files on disk, and all associated chunks in the vector store.
        /// </summary>
        Task DeleteKnowledgebaseAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all documents belonging to a knowledgebase.
        /// </summary>
        Task<List<KnowledgebaseDocumentEntity>> GetDocumentsAsync(Guid knowledgebaseId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a single document by its ID.
        /// </summary>
        Task<KnowledgebaseDocumentEntity?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default);

        /// <summary>
        /// Deletes a single document, its physical file from storage, and all its vector chunks.
        /// </summary>
        Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default);

        /// <summary>
        /// Ingests a collection of files into the specified knowledgebase:
        /// copies files to application storage, extracts text, chunks content,
        /// generates vector embeddings, validates dimensions, and indexes chunks into SQLite.
        /// </summary>
        Task<List<KnowledgebaseDocumentEntity>> IngestDocumentsAsync(
            Guid knowledgebaseId,
            IEnumerable<string> filePaths,
            IngestionOptions? options = null,
            IProgress<IndexingProgressReport>? progress = null,
            CancellationToken ct = default);

        /// <summary>
        /// Retrieves the extracted plain text for a document. If verbatim raw extracted text is stored,
        /// it is returned; otherwise, chunks stored in SQLite are concatenated in sequence order.
        /// </summary>
        Task<string> GetDocumentExtractedTextAsync(Guid documentId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all chunks for a document ordered by ChunkIndex.
        /// </summary>
        Task<List<DocumentChunkEntity>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct = default);
    }
}

