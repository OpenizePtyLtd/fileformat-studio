using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;

namespace FileFormatAIStudio.Services.Knowledgebase
{
    /// <summary>
    /// Service contract for storing document chunks and performing hardware-accelerated SIMD vector similarity searches.
    /// </summary>
    public interface IVectorStoreService
    {
        /// <summary>
        /// Persists a collection of document chunk entities with embedded vector BLOBs into SQLite.
        /// </summary>
        Task StoreChunksAsync(IEnumerable<DocumentChunkEntity> chunks, CancellationToken ct = default);

        /// <summary>
        /// Searches for the most semantically similar document chunks across one or more knowledgebases using SIMD Cosine Similarity.
        /// </summary>
        /// <param name="queryVector">The high-dimensional embedding vector of the search query.</param>
        /// <param name="knowledgebaseIds">List of knowledgebase IDs to search across.</param>
        /// <param name="topK">The maximum number of top matching chunks to return.</param>
        /// <param name="minSimilarity">The minimum cosine similarity threshold (-1.0 to 1.0) for a chunk to qualify.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Ranked list of scored chunks sorted by descending similarity score.</returns>
        Task<IReadOnlyList<ScoredChunkResult>> SearchAsync(
            ReadOnlyMemory<float> queryVector,
            IReadOnlyList<Guid> knowledgebaseIds,
            int topK = 5,
            float minSimilarity = 0.5f,
            CancellationToken ct = default);

        /// <summary>
        /// Deletes all chunks associated with a specific document.
        /// </summary>
        Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default);

        /// <summary>
        /// Deletes all chunks associated with a specific knowledgebase.
        /// </summary>
        Task DeleteByKnowledgebaseIdAsync(Guid knowledgebaseId, CancellationToken ct = default);

        /// <summary>
        /// Gets the total number of chunks stored for a specific knowledgebase.
        /// </summary>
        Task<int> GetChunkCountAsync(Guid knowledgebaseId, CancellationToken ct = default);
    }
}
