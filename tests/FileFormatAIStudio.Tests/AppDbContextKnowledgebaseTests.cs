using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class AppDbContextKnowledgebaseTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public AppDbContextKnowledgebaseTests()
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

        [Fact]
        public async Task Knowledgebase_WithDocumentsAndChunks_CanBePersistedAndRetrieved()
        {
            var kbId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var chunkId = Guid.NewGuid();

            float[] originalFloats = [0.123f, 0.456f, 0.789f, -0.987f];
            byte[] vectorBytes = MemoryMarshal.AsBytes(originalFloats.AsSpan()).ToArray();

            using (var context = new AppDbContext(_options))
            {
                var kb = new KnowledgebaseEntity
                {
                    Id = kbId,
                    Name = "Engineering Handbook",
                    Description = "Architecture specifications and standards",
                    ParserEngine = "Aspose",
                    EmbeddingProvider = "OpenAI",
                    EmbeddingModel = "text-embedding-3-small",
                    VectorDimensions = 1536
                };

                var doc = new KnowledgebaseDocumentEntity
                {
                    Id = docId,
                    KnowledgebaseId = kbId,
                    FileName = "architecture.pdf",
                    FilePath = "C:/docs/architecture.pdf",
                    FileType = ".pdf",
                    FileSize = 1048576,
                    Status = "Indexed",
                    ParserEngineUsed = "Aspose",
                    ChunkCount = 1,
                    IndexedAt = DateTime.UtcNow
                };

                var chunk = new DocumentChunkEntity
                {
                    Id = chunkId,
                    KnowledgebaseId = kbId,
                    DocumentId = docId,
                    TextContent = "Microservices communicate via gRPC over TLS.",
                    SourceFileName = "architecture.pdf",
                    PageOrSectionNumber = 3,
                    ChunkIndex = 0,
                    TokenCount = 7,
                    EmbeddingVector = vectorBytes
                };

                doc.Chunks.Add(chunk);
                kb.Documents.Add(doc);

                context.Knowledgebases.Add(kb);
                await context.SaveChangesAsync();
            }

            using (var readContext = new AppDbContext(_options))
            {
                var loadedKb = await readContext.Knowledgebases
                    .Include(k => k.Documents)
                    .ThenInclude(d => d.Chunks)
                    .FirstOrDefaultAsync(k => k.Id == kbId);

                loadedKb.Should().NotBeNull();
                loadedKb!.Name.Should().Be("Engineering Handbook");
                loadedKb.ParserEngine.Should().Be("Aspose");
                loadedKb.EmbeddingModel.Should().Be("text-embedding-3-small");
                loadedKb.VectorDimensions.Should().Be(1536);

                loadedKb.Documents.Should().HaveCount(1);
                var loadedDoc = loadedKb.Documents[0];
                loadedDoc.Id.Should().Be(docId);
                loadedDoc.FileName.Should().Be("architecture.pdf");
                loadedDoc.Status.Should().Be("Indexed");
                loadedDoc.ParserEngineUsed.Should().Be("Aspose");

                loadedDoc.Chunks.Should().HaveCount(1);
                var loadedChunk = loadedDoc.Chunks[0];
                loadedChunk.Id.Should().Be(chunkId);
                loadedChunk.TextContent.Should().Be("Microservices communicate via gRPC over TLS.");
                loadedChunk.PageOrSectionNumber.Should().Be(3);
                loadedChunk.EmbeddingVector.Should().BeEquivalentTo(vectorBytes);

                // Verify BLOB cast back to floats
                var restoredFloats = MemoryMarshal.Cast<byte, float>(loadedChunk.EmbeddingVector.AsSpan()).ToArray();
                restoredFloats.Should().Equal(originalFloats);
            }
        }

        [Fact]
        public async Task DeletingKnowledgebase_CascadesToDeleteDocumentsAndChunks()
        {
            var kbId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var chunkId = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                var kb = new KnowledgebaseEntity { Id = kbId, Name = "Temporary KB" };
                var doc = new KnowledgebaseDocumentEntity
                {
                    Id = docId,
                    KnowledgebaseId = kbId,
                    FileName = "temp.docx"
                };
                var chunk = new DocumentChunkEntity
                {
                    Id = chunkId,
                    KnowledgebaseId = kbId,
                    DocumentId = docId,
                    TextContent = "Temporary chunk",
                    EmbeddingVector = [1, 2, 3, 4]
                };

                doc.Chunks.Add(chunk);
                kb.Documents.Add(doc);
                context.Knowledgebases.Add(kb);
                await context.SaveChangesAsync();
            }

            using (var deleteContext = new AppDbContext(_options))
            {
                var kb = await deleteContext.Knowledgebases
                    .Include(k => k.Documents)
                    .Include(k => k.Chunks)
                    .FirstAsync(k => k.Id == kbId);

                deleteContext.Knowledgebases.Remove(kb);
                await deleteContext.SaveChangesAsync();
            }

            using (var verifyContext = new AppDbContext(_options))
            {
                (await verifyContext.Knowledgebases.AnyAsync(k => k.Id == kbId)).Should().BeFalse();
                (await verifyContext.KnowledgebaseDocuments.AnyAsync(d => d.Id == docId)).Should().BeFalse();
                (await verifyContext.DocumentChunks.AnyAsync(c => c.Id == chunkId)).Should().BeFalse();
            }
        }

        [Fact]
        public async Task DeletingDocument_CascadesToDeleteItsChunksOnly()
        {
            var kbId = Guid.NewGuid();
            var doc1Id = Guid.NewGuid();
            var doc2Id = Guid.NewGuid();
            var chunk1Id = Guid.NewGuid();
            var chunk2Id = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                var kb = new KnowledgebaseEntity { Id = kbId, Name = "Multi-Doc KB" };
                var doc1 = new KnowledgebaseDocumentEntity { Id = doc1Id, KnowledgebaseId = kbId, FileName = "doc1.pdf" };
                var doc2 = new KnowledgebaseDocumentEntity { Id = doc2Id, KnowledgebaseId = kbId, FileName = "doc2.pdf" };

                var chunk1 = new DocumentChunkEntity { Id = chunk1Id, KnowledgebaseId = kbId, DocumentId = doc1Id, TextContent = "Chunk 1" };
                var chunk2 = new DocumentChunkEntity { Id = chunk2Id, KnowledgebaseId = kbId, DocumentId = doc2Id, TextContent = "Chunk 2" };

                doc1.Chunks.Add(chunk1);
                doc2.Chunks.Add(chunk2);
                kb.Documents.Add(doc1);
                kb.Documents.Add(doc2);

                context.Knowledgebases.Add(kb);
                await context.SaveChangesAsync();
            }

            using (var deleteContext = new AppDbContext(_options))
            {
                var doc1 = await deleteContext.KnowledgebaseDocuments
                    .Include(d => d.Chunks)
                    .FirstAsync(d => d.Id == doc1Id);

                deleteContext.KnowledgebaseDocuments.Remove(doc1);
                await deleteContext.SaveChangesAsync();
            }

            using (var verifyContext = new AppDbContext(_options))
            {
                (await verifyContext.KnowledgebaseDocuments.AnyAsync(d => d.Id == doc1Id)).Should().BeFalse();
                (await verifyContext.DocumentChunks.AnyAsync(c => c.Id == chunk1Id)).Should().BeFalse();

                (await verifyContext.KnowledgebaseDocuments.AnyAsync(d => d.Id == doc2Id)).Should().BeTrue();
                (await verifyContext.DocumentChunks.AnyAsync(c => c.Id == chunk2Id)).Should().BeTrue();
            }
        }

        [Fact]
        public async Task SessionKnowledgebase_ManyToManyRelationship_SupportsMultipleKBsAndCleanSessionDeletion()
        {
            var sessionId = Guid.NewGuid();
            var kb1Id = Guid.NewGuid();
            var kb2Id = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                var session = new ChatSessionEntity { Id = sessionId, Title = "Multi-KB Analysis" };
                var kb1 = new KnowledgebaseEntity { Id = kb1Id, Name = "Legal Docs" };
                var kb2 = new KnowledgebaseEntity { Id = kb2Id, Name = "Technical Specs" };

                var link1 = new SessionKnowledgebaseEntity { SessionId = sessionId, KnowledgebaseId = kb1Id };
                var link2 = new SessionKnowledgebaseEntity { SessionId = sessionId, KnowledgebaseId = kb2Id };

                context.Sessions.Add(session);
                context.Knowledgebases.AddRange(kb1, kb2);
                context.SessionKnowledgebases.AddRange(link1, link2);

                await context.SaveChangesAsync();
            }

            // Verify both KBs are attached to the session
            using (var readContext = new AppDbContext(_options))
            {
                var loadedSession = await readContext.Sessions
                    .Include(s => s.SessionKnowledgebases)
                    .ThenInclude(sk => sk.Knowledgebase)
                    .FirstAsync(s => s.Id == sessionId);

                loadedSession.SessionKnowledgebases.Should().HaveCount(2);
                loadedSession.SessionKnowledgebases.Select(sk => sk.Knowledgebase.Name)
                    .Should().Contain(["Legal Docs", "Technical Specs"]);
            }

            // Delete session and verify join records are deleted, but KBs remain
            using (var deleteContext = new AppDbContext(_options))
            {
                var session = await deleteContext.Sessions
                    .Include(s => s.SessionKnowledgebases)
                    .FirstAsync(s => s.Id == sessionId);

                deleteContext.Sessions.Remove(session);
                await deleteContext.SaveChangesAsync();
            }

            using (var verifyContext = new AppDbContext(_options))
            {
                (await verifyContext.Sessions.AnyAsync(s => s.Id == sessionId)).Should().BeFalse();
                (await verifyContext.SessionKnowledgebases.AnyAsync(sk => sk.SessionId == sessionId)).Should().BeFalse();
                (await verifyContext.Knowledgebases.AnyAsync(k => k.Id == kb1Id)).Should().BeTrue();
                (await verifyContext.Knowledgebases.AnyAsync(k => k.Id == kb2Id)).Should().BeTrue();
            }
        }

        [Fact]
        public async Task DbInitializer_AppliesMigration_CreatesKnowledgebaseAndVectorTablesSuccessfully()
        {
            using var freshConnection = new SqliteConnection("Data Source=:memory:");
            await freshConnection.OpenAsync();

            var freshOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(freshConnection)
                .Options;

            using (var context = new AppDbContext(freshOptions))
            {
                // Run DbInitializer which applies pending migrations
                await DbInitializer.InitializeAsync(context);
            }

            // Verify tables and indexes exist in SQLite schema
            using (var checkCmd = freshConnection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                var tableNames = new List<string>();
                using (var reader = await checkCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        tableNames.Add(reader.GetString(0));
                    }
                }

                tableNames.Should().Contain("Knowledgebases");
                tableNames.Should().Contain("KnowledgebaseDocuments");
                tableNames.Should().Contain("SessionKnowledgebases");
                tableNames.Should().Contain("DocumentChunks");
            }

            using (var indexCmd = freshConnection.CreateCommand())
            {
                indexCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index';";
                var indexNames = new List<string>();
                using (var reader = await indexCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        indexNames.Add(reader.GetString(0));
                    }
                }

                indexNames.Should().Contain("IX_DocumentChunks_KnowledgebaseId");
                indexNames.Should().Contain("IX_DocumentChunks_DocumentId");
                indexNames.Should().Contain("IX_KnowledgebaseDocuments_KnowledgebaseId");
                indexNames.Should().Contain("IX_SessionKnowledgebases_KnowledgebaseId");
            }

            using (var columnCmd = freshConnection.CreateCommand())
            {
                columnCmd.CommandText = "PRAGMA table_info(\"DocumentChunks\");";
                var columnTypes = new Dictionary<string, string>();
                using (var reader = await columnCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var colName = reader.GetString(1);
                        var colType = reader.GetString(2);
                        columnTypes[colName] = colType;
                    }
                }

                columnTypes.Should().ContainKey("EmbeddingVector");
                columnTypes["EmbeddingVector"].Should().Be("BLOB");
            }
        }
    }
}

