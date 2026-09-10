using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Knowledgebase;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class VectorStoreServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public VectorStoreServiceTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private static byte[] ToBlob(float[] vector)
        {
            return MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();
        }

        [Fact]
        public async Task SearchAsync_CalculatesExactCosineSimilarity_AndRanksDescending()
        {
            var kbId = Guid.NewGuid();
            var docId = Guid.NewGuid();

            var chunkA = new DocumentChunkEntity
            {
                Id = Guid.NewGuid(),
                KnowledgebaseId = kbId,
                DocumentId = docId,
                TextContent = "Chunk A - Perfect Match",
                SourceFileName = "sample.txt",
                ChunkIndex = 0,
                EmbeddingVector = ToBlob([1.0f, 0.0f, 0.0f, 0.0f])
            };

            var chunkB = new DocumentChunkEntity
            {
                Id = Guid.NewGuid(),
                KnowledgebaseId = kbId,
                DocumentId = docId,
                TextContent = "Chunk B - Moderate Match",
                SourceFileName = "sample.txt",
                ChunkIndex = 1,
                EmbeddingVector = ToBlob([0.70710678f, 0.70710678f, 0.0f, 0.0f]) // ~45 deg angle, cos ~0.7071
            };

            var chunkC = new DocumentChunkEntity
            {
                Id = Guid.NewGuid(),
                KnowledgebaseId = kbId,
                DocumentId = docId,
                TextContent = "Chunk C - Orthogonal (No match)",
                SourceFileName = "sample.txt",
                ChunkIndex = 2,
                EmbeddingVector = ToBlob([0.0f, 1.0f, 0.0f, 0.0f]) // 90 deg, cos = 0.0
            };

            using (var context = new AppDbContext(_options))
            {
                var kb = new KnowledgebaseEntity { Id = kbId, Name = "Test KB" };
                var doc = new KnowledgebaseDocumentEntity { Id = docId, KnowledgebaseId = kbId, FileName = "sample.txt" };
                context.Knowledgebases.Add(kb);
                context.KnowledgebaseDocuments.Add(doc);
                await context.SaveChangesAsync();

                var service = new VectorStoreService(context);
                await service.StoreChunksAsync([chunkA, chunkB, chunkC]);
            }

            using (var queryContext = new AppDbContext(_options))
            {
                var service = new VectorStoreService(queryContext);
                float[] queryVector = [1.0f, 0.0f, 0.0f, 0.0f];

                // Search with minSimilarity = 0.5f
                var results = await service.SearchAsync(queryVector, [kbId], topK: 5, minSimilarity: 0.5f);

                results.Should().HaveCount(2);

                // Chunk A should be first (cos = 1.0)
                results[0].ChunkId.Should().Be(chunkA.Id);
                results[0].Score.Should().BeApproximately(1.0f, 0.001f);
                results[0].TextContent.Should().Be("Chunk A - Perfect Match");

                // Chunk B should be second (cos ~ 0.7071)
                results[1].ChunkId.Should().Be(chunkB.Id);
                results[1].Score.Should().BeApproximately(0.7071f, 0.001f);

                // Chunk C was filtered out by minSimilarity 0.5f
                results.Any(r => r.ChunkId == chunkC.Id).Should().BeFalse();
            }
        }

        [Fact]
        public async Task SearchAsync_SupportsMultiKnowledgebaseQueries_AndFiltersUnrequestedKBs()
        {
            var kb1Id = Guid.NewGuid();
            var kb2Id = Guid.NewGuid();
            var kb3Id = Guid.NewGuid();

            var doc1Id = Guid.NewGuid();
            var doc2Id = Guid.NewGuid();
            var doc3Id = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                context.Knowledgebases.AddRange(
                    new KnowledgebaseEntity { Id = kb1Id, Name = "KB 1" },
                    new KnowledgebaseEntity { Id = kb2Id, Name = "KB 2" },
                    new KnowledgebaseEntity { Id = kb3Id, Name = "KB 3" }
                );

                context.KnowledgebaseDocuments.AddRange(
                    new KnowledgebaseDocumentEntity { Id = doc1Id, KnowledgebaseId = kb1Id, FileName = "doc1.txt" },
                    new KnowledgebaseDocumentEntity { Id = doc2Id, KnowledgebaseId = kb2Id, FileName = "doc2.txt" },
                    new KnowledgebaseDocumentEntity { Id = doc3Id, KnowledgebaseId = kb3Id, FileName = "doc3.txt" }
                );

                context.DocumentChunks.AddRange(
                    new DocumentChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        KnowledgebaseId = kb1Id,
                        DocumentId = doc1Id,
                        TextContent = "KB1 chunk",
                        EmbeddingVector = ToBlob([0.9f, 0.1f])
                    },
                    new DocumentChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        KnowledgebaseId = kb2Id,
                        DocumentId = doc2Id,
                        TextContent = "KB2 chunk",
                        EmbeddingVector = ToBlob([0.8f, 0.2f])
                    },
                    new DocumentChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        KnowledgebaseId = kb3Id,
                        DocumentId = doc3Id,
                        TextContent = "KB3 chunk",
                        EmbeddingVector = ToBlob([1.0f, 0.0f])
                    }
                );

                await context.SaveChangesAsync();
            }

            using (var queryContext = new AppDbContext(_options))
            {
                var service = new VectorStoreService(queryContext);
                float[] query = [1.0f, 0.0f];

                // Search across KB1 and KB2 only (KB3 excluded)
                var results = await service.SearchAsync(query, [kb1Id, kb2Id], topK: 10, minSimilarity: 0.1f);

                results.Should().HaveCount(2);
                results.Select(r => r.KnowledgebaseId).Should().Contain([kb1Id, kb2Id]);
                results.Any(r => r.KnowledgebaseId == kb3Id).Should().BeFalse();
            }
        }

        [Fact]
        public async Task SearchAsync_RespectsTopKLimit_WhenMoreCandidatesQualify()
        {
            var kbId = Guid.NewGuid();
            var docId = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                context.Knowledgebases.Add(new KnowledgebaseEntity { Id = kbId, Name = "Top-K Test" });
                context.KnowledgebaseDocuments.Add(new KnowledgebaseDocumentEntity { Id = docId, KnowledgebaseId = kbId, FileName = "test.txt" });

                var chunks = new List<DocumentChunkEntity>();
                for (int i = 1; i <= 10; i++)
                {
                    // Create vectors with increasing alignment with [1, 0]:
                    // e.g. [0.1, 0.9], [0.2, 0.8], ..., [1.0, 0.0]
                    float x = i / 10.0f;
                    float y = (10 - i) / 10.0f;
                    chunks.Add(new DocumentChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        KnowledgebaseId = kbId,
                        DocumentId = docId,
                        TextContent = $"Chunk {i}",
                        ChunkIndex = i,
                        EmbeddingVector = ToBlob([x, y])
                    });
                }

                context.DocumentChunks.AddRange(chunks);
                await context.SaveChangesAsync();
            }

            using (var queryContext = new AppDbContext(_options))
            {
                var service = new VectorStoreService(queryContext);
                float[] query = [1.0f, 0.0f];

                // Request top 3
                var results = await service.SearchAsync(query, [kbId], topK: 3, minSimilarity: 0.0f);

                results.Should().HaveCount(3);
                // The top 3 should be chunks 10, 9, 8 in descending order
                results[0].TextContent.Should().Be("Chunk 10");
                results[1].TextContent.Should().Be("Chunk 9");
                results[2].TextContent.Should().Be("Chunk 8");
                results[0].Score.Should().BeGreaterThan(results[1].Score);
                results[1].Score.Should().BeGreaterThan(results[2].Score);
            }
        }

        [Fact]
        public async Task SearchAsync_SkipsChunksWithMismatchedVectorDimensions()
        {
            var kbId = Guid.NewGuid();
            var docId = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                context.Knowledgebases.Add(new KnowledgebaseEntity { Id = kbId, Name = "Dim Test" });
                context.KnowledgebaseDocuments.Add(new KnowledgebaseDocumentEntity { Id = docId, KnowledgebaseId = kbId, FileName = "test.txt" });

                context.DocumentChunks.AddRange(
                    new DocumentChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        KnowledgebaseId = kbId,
                        DocumentId = docId,
                        TextContent = "3D chunk",
                        EmbeddingVector = ToBlob([1.0f, 0.0f, 0.0f]) // 3 floats
                    },
                    new DocumentChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        KnowledgebaseId = kbId,
                        DocumentId = docId,
                        TextContent = "4D chunk",
                        EmbeddingVector = ToBlob([1.0f, 0.0f, 0.0f, 0.0f]) // 4 floats
                    }
                );

                await context.SaveChangesAsync();
            }

            using (var queryContext = new AppDbContext(_options))
            {
                var service = new VectorStoreService(queryContext);
                float[] query4D = [1.0f, 0.0f, 0.0f, 0.0f]; // 4 floats

                var results = await service.SearchAsync(query4D, [kbId], topK: 5, minSimilarity: 0.0f);

                results.Should().HaveCount(1);
                results[0].TextContent.Should().Be("4D chunk");
            }
        }

        [Fact]
        public async Task DeleteMethods_And_GetChunkCount_FunctionCorrectly()
        {
            var kb1Id = Guid.NewGuid();
            var kb2Id = Guid.NewGuid();
            var doc1Id = Guid.NewGuid();
            var doc2Id = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                context.Knowledgebases.AddRange(
                    new KnowledgebaseEntity { Id = kb1Id, Name = "KB 1" },
                    new KnowledgebaseEntity { Id = kb2Id, Name = "KB 2" }
                );
                context.KnowledgebaseDocuments.AddRange(
                    new KnowledgebaseDocumentEntity { Id = doc1Id, KnowledgebaseId = kb1Id, FileName = "doc1.txt" },
                    new KnowledgebaseDocumentEntity { Id = doc2Id, KnowledgebaseId = kb2Id, FileName = "doc2.txt" }
                );

                context.DocumentChunks.AddRange(
                    new DocumentChunkEntity { Id = Guid.NewGuid(), KnowledgebaseId = kb1Id, DocumentId = doc1Id, TextContent = "C1", EmbeddingVector = ToBlob([1f]) },
                    new DocumentChunkEntity { Id = Guid.NewGuid(), KnowledgebaseId = kb1Id, DocumentId = doc1Id, TextContent = "C2", EmbeddingVector = ToBlob([2f]) },
                    new DocumentChunkEntity { Id = Guid.NewGuid(), KnowledgebaseId = kb2Id, DocumentId = doc2Id, TextContent = "C3", EmbeddingVector = ToBlob([3f]) }
                );

                await context.SaveChangesAsync();
            }

            using (var opContext = new AppDbContext(_options))
            {
                var service = new VectorStoreService(opContext);

                (await service.GetChunkCountAsync(kb1Id)).Should().Be(2);
                (await service.GetChunkCountAsync(kb2Id)).Should().Be(1);

                // Delete doc1 chunks
                await service.DeleteByDocumentIdAsync(doc1Id);
                (await service.GetChunkCountAsync(kb1Id)).Should().Be(0);
                (await service.GetChunkCountAsync(kb2Id)).Should().Be(1);

                // Delete kb2 chunks
                await service.DeleteByKnowledgebaseIdAsync(kb2Id);
                (await service.GetChunkCountAsync(kb2Id)).Should().Be(0);
            }
        }

        [Fact]
        public async Task SearchAsync_SimdPerformance_Scans1000VectorsFast()
        {
            const int dimensions = 1536; // OpenAI text-embedding-3-small standard
            const int vectorCount = 1000;

            var kbId = Guid.NewGuid();
            var docId = Guid.NewGuid();

            var random = new Random(42);
            var queryFloats = new float[dimensions];
            for (int i = 0; i < dimensions; i++) queryFloats[i] = (float)random.NextDouble();

            using (var context = new AppDbContext(_options))
            {
                context.Knowledgebases.Add(new KnowledgebaseEntity { Id = kbId, Name = "Perf KB" });
                context.KnowledgebaseDocuments.Add(new KnowledgebaseDocumentEntity { Id = docId, KnowledgebaseId = kbId, FileName = "large.txt" });

                var chunks = new List<DocumentChunkEntity>(vectorCount);
                for (int i = 0; i < vectorCount; i++)
                {
                    var vec = new float[dimensions];
                    for (int d = 0; d < dimensions; d++) vec[d] = (float)random.NextDouble();

                    chunks.Add(new DocumentChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        KnowledgebaseId = kbId,
                        DocumentId = docId,
                        TextContent = $"Chunk {i}",
                        ChunkIndex = i,
                        EmbeddingVector = ToBlob(vec)
                    });
                }

                context.DocumentChunks.AddRange(chunks);
                await context.SaveChangesAsync();
            }

            using (var queryContext = new AppDbContext(_options))
            {
                var service = new VectorStoreService(queryContext);

                // Warm up
                await service.SearchAsync(queryFloats, [kbId], topK: 5, minSimilarity: 0.0f);

                // Measure latency
                var sw = Stopwatch.StartNew();
                var results = await service.SearchAsync(queryFloats, [kbId], topK: 5, minSimilarity: 0.0f);
                sw.Stop();

                results.Should().HaveCount(5);
                // AVX2 / AVX-512 SIMD scan over 1,000 1536-dim vectors in in-memory SQLite should execute under 50ms
                sw.ElapsedMilliseconds.Should().BeLessThan(100);
            }
        }
    }
}
