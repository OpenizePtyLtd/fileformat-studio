using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileFormatAIStudio.Services.Knowledgebase
{
    /// <summary>
    /// Implements hardware-accelerated CPU SIMD vector storage and search directly in SQLite.
    /// Uses <see cref="TensorPrimitives.CosineSimilarity"/> (AVX2 / AVX-512 / ARM NEON) over raw binary BLOB vectors.
    /// </summary>
    public class VectorStoreService : IVectorStoreService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<VectorStoreService> _logger;

        public VectorStoreService(AppDbContext dbContext, ILogger<VectorStoreService>? logger = null)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? NullLogger<VectorStoreService>.Instance;
        }

        /// <inheritdoc />
        public async Task StoreChunksAsync(IEnumerable<DocumentChunkEntity> chunks, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(chunks);

            _dbContext.DocumentChunks.AddRange(chunks);
            await _dbContext.SaveChangesAsync(ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ScoredChunkResult>> SearchAsync(
            ReadOnlyMemory<float> queryVector,
            IReadOnlyList<Guid> knowledgebaseIds,
            int topK = 5,
            float minSimilarity = 0.5f,
            CancellationToken ct = default)
        {
            if (queryVector.IsEmpty || knowledgebaseIds == null || knowledgebaseIds.Count == 0 || topK <= 0)
            {
                return Array.Empty<ScoredChunkResult>();
            }

            int expectedByteLength = queryVector.Length * sizeof(float);

            // Candidate filtering by KnowledgebaseIds leveraging SQLite index IX_DocumentChunks_KnowledgebaseId
            var candidateChunks = await _dbContext.DocumentChunks
                .AsNoTracking()
                .Where(c => knowledgebaseIds.Contains(c.KnowledgebaseId))
                .Select(c => new
                {
                    c.Id,
                    c.DocumentId,
                    c.KnowledgebaseId,
                    c.TextContent,
                    c.SourceFileName,
                    c.PageOrSectionNumber,
                    c.ChunkIndex,
                    c.TokenCount,
                    c.EmbeddingVector
                })
                .ToListAsync(ct);

            if (candidateChunks.Count == 0)
            {
                return Array.Empty<ScoredChunkResult>();
            }

            var querySpan = queryVector.Span;

            // Min-heap PriorityQueue to maintain Top-K scored items in O(N log K) without sorting entire candidate set
            var minHeap = new PriorityQueue<ScoredChunkResult, float>(topK);

            foreach (var chunk in candidateChunks)
            {
                if (chunk.EmbeddingVector == null || chunk.EmbeddingVector.Length != expectedByteLength)
                {
                    _logger.LogWarning(
                        "Chunk {ChunkId} vector byte length {Actual} does not match expected query vector byte length {Expected}. Skipping chunk.",
                        chunk.Id, chunk.EmbeddingVector?.Length ?? 0, expectedByteLength);
                    continue;
                }

                // Zero-copy cast from SQLite BLOB bytes to float32 span
                ReadOnlySpan<float> candidateVector = MemoryMarshal.Cast<byte, float>(chunk.EmbeddingVector.AsSpan());

                // Hardware-accelerated CPU SIMD cosine similarity (AVX2 / AVX-512 / ARM NEON)
                float similarity = TensorPrimitives.CosineSimilarity(querySpan, candidateVector);

                if (float.IsNaN(similarity) || similarity < minSimilarity)
                {
                    continue;
                }

                var scoredResult = new ScoredChunkResult(
                    ChunkId: chunk.Id,
                    DocumentId: chunk.DocumentId,
                    KnowledgebaseId: chunk.KnowledgebaseId,
                    TextContent: chunk.TextContent,
                    SourceFileName: chunk.SourceFileName,
                    PageOrSectionNumber: chunk.PageOrSectionNumber,
                    ChunkIndex: chunk.ChunkIndex,
                    TokenCount: chunk.TokenCount,
                    Score: similarity
                );

                if (minHeap.Count < topK)
                {
                    minHeap.Enqueue(scoredResult, similarity);
                }
                else if (minHeap.TryPeek(out _, out float lowestScore) && similarity > lowestScore)
                {
                    minHeap.Dequeue();
                    minHeap.Enqueue(scoredResult, similarity);
                }
            }

            // Drain min-heap in ascending order, then reverse to return descending by score
            var results = new List<ScoredChunkResult>(minHeap.Count);
            while (minHeap.TryDequeue(out var result, out _))
            {
                results.Add(result);
            }

            results.Reverse();
            return results;
        }

        /// <inheritdoc />
        public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
        {
            await _dbContext.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .ExecuteDeleteAsync(ct);
        }

        /// <inheritdoc />
        public async Task DeleteByKnowledgebaseIdAsync(Guid knowledgebaseId, CancellationToken ct = default)
        {
            await _dbContext.DocumentChunks
                .Where(c => c.KnowledgebaseId == knowledgebaseId)
                .ExecuteDeleteAsync(ct);
        }

        /// <inheritdoc />
        public async Task<int> GetChunkCountAsync(Guid knowledgebaseId, CancellationToken ct = default)
        {
            return await _dbContext.DocumentChunks
                .CountAsync(c => c.KnowledgebaseId == knowledgebaseId, ct);
        }
    }
}
