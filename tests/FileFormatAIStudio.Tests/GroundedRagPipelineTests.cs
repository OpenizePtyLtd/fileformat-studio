using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Chat;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class GroundedRagPipelineTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public GroundedRagPipelineTests()
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
        public async Task RetrieveGroundedContextAsync_NoAttachedKnowledgebases_ReturnsEmptyResult()
        {
            using var context = new AppDbContext(_options);
            var fakeClientFactory = new FakeAiClientFactory();
            var fakeVectorStore = new FakeVectorStoreService();

            var service = new ChatExecutionService(
                fakeClientFactory,
                fakeVectorStore,
                context,
                NullLogger<ChatExecutionService>.Instance);

            var result = await service.RetrieveGroundedContextAsync(
                "What is the return policy?",
                new List<KnowledgebaseEntity>());

            result.Citations.Should().BeEmpty();
            result.GroundedSystemPrompt.Should().BeEmpty();
        }

        [Fact]
        public async Task RetrieveGroundedContextAsync_WithAttachedKnowledgebase_GeneratesEmbeddingsAndRetrievesTopChunks()
        {
            var kbId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var chunk1Id = Guid.NewGuid();
            var chunk2Id = Guid.NewGuid();

            var kb = new KnowledgebaseEntity
            {
                Id = kbId,
                Name = "Employee Handbook",
                EmbeddingProvider = "OpenAI",
                EmbeddingModel = "text-embedding-3-small",
                VectorDimensions = 4
            };

            using (var context = new AppDbContext(_options))
            {
                context.Providers.Add(new ProviderConfigEntity
                {
                    Name = "OpenAI",
                    ProviderType = "OpenAI",
                    ApiKey = "test-key",
                    IsEnabled = true
                });
                context.Knowledgebases.Add(kb);
                await context.SaveChangesAsync();
            }

            var fakeClientFactory = new FakeAiClientFactory();
            var fakeVectorStore = new FakeVectorStoreService();

            // Populate fake vector store chunks for this KB
            fakeVectorStore.StubbedResults.Add(new ScoredChunkResult(
                ChunkId: chunk1Id,
                DocumentId: docId,
                KnowledgebaseId: kbId,
                TextContent: "Full-time employees receive 20 days of paid annual leave.",
                SourceFileName: "handbook.docx",
                PageOrSectionNumber: 5,
                ChunkIndex: 2,
                TokenCount: 10,
                Score: 0.92f
            ));
            fakeVectorStore.StubbedResults.Add(new ScoredChunkResult(
                ChunkId: chunk2Id,
                DocumentId: docId,
                KnowledgebaseId: kbId,
                TextContent: "Sick leave requires medical certification after 2 consecutive days.",
                SourceFileName: "handbook.docx",
                PageOrSectionNumber: 6,
                ChunkIndex: 3,
                TokenCount: 11,
                Score: 0.81f
            ));

            using (var context = new AppDbContext(_options))
            {
                var service = new ChatExecutionService(
                    fakeClientFactory,
                    fakeVectorStore,
                    context,
                    NullLogger<ChatExecutionService>.Instance);

                var result = await service.RetrieveGroundedContextAsync(
                    "How many vacation days do employees get?",
                    new List<KnowledgebaseEntity> { kb });

                result.Citations.Should().HaveCount(2);

                var first = result.Citations[0];
                first.Index.Should().Be(1);
                first.DocumentName.Should().Be("handbook.docx");
                first.PageOrSectionNumber.Should().Be(5);
                first.KnowledgebaseName.Should().Be("Employee Handbook");
                first.DisplayIndex.Should().Be("[1]");
                first.PageOrSectionDisplay.Should().Be("p. 5");
                first.MatchScoreText.Should().Be("92% match");
                first.Snippet.Should().Contain("20 days of paid annual leave");

                var second = result.Citations[1];
                second.Index.Should().Be(2);
                second.DisplayIndex.Should().Be("[2]");
                second.PageOrSectionDisplay.Should().Be("p. 6");
                second.MatchScoreText.Should().Be("81% match");

                // Grounded system prompt should contain instructions and citation markers
                result.GroundedSystemPrompt.Should().Contain("[1] Source: handbook.docx (Page/Section 5, Knowledge Base: Employee Handbook)");
                result.GroundedSystemPrompt.Should().Contain("[2] Source: handbook.docx (Page/Section 6, Knowledge Base: Employee Handbook)");
                result.GroundedSystemPrompt.Should().Contain("Full-time employees receive 20 days of paid annual leave.");
            }
        }

        [Fact]
        public void ChatMessageItemViewModel_LoadCitationsFromJson_SetsPropertiesCorrectly()
        {
            var citations = new List<CitationReference>
            {
                new()
                {
                    Index = 1,
                    ChunkId = Guid.NewGuid(),
                    DocumentId = Guid.NewGuid(),
                    KnowledgebaseId = Guid.NewGuid(),
                    KnowledgebaseName = "Legal Docs",
                    DocumentName = "NDA.pdf",
                    PageOrSectionNumber = 2,
                    Snippet = "Confidential information includes proprietary source code.",
                    Score = 0.89f
                }
            };

            string json = JsonSerializer.Serialize(citations);

            var vm = new ChatMessageItemViewModel
            {
                Role = "Assistant",
                Content = "According to the agreement [1], source code is confidential."
            };

            vm.HasCitations.Should().BeFalse();

            vm.LoadCitationsFromJson(json);

            vm.HasCitations.Should().BeTrue();
            vm.Citations.Should().HaveCount(1);
            vm.CitationsHeader.Should().Be("Sources (1)");

            var citation = vm.Citations[0];
            citation.DocumentName.Should().Be("NDA.pdf");
            citation.DisplayIndex.Should().Be("[1]");
            citation.PageOrSectionDisplay.Should().Be("p. 2");
            citation.SourceHeaderInfo.Should().Be("KB: Legal Docs • p. 2");
            citation.MatchScoreText.Should().Be("89% match");
        }

        [Fact]
        public async Task ChatSessionService_AddMessageWithCitationJson_PersistsAndRetrievesCitations()
        {
            var sessionId = Guid.NewGuid();
            var citations = new List<CitationReference>
            {
                new()
                {
                    Index = 1,
                    ChunkId = Guid.NewGuid(),
                    DocumentId = Guid.NewGuid(),
                    KnowledgebaseId = Guid.NewGuid(),
                    KnowledgebaseName = "Finance 2026",
                    DocumentName = "Q3_Report.xlsx",
                    PageOrSectionNumber = 1,
                    Snippet = "Net margin increased by 14% year over year.",
                    Score = 0.95f
                }
            };

            string json = JsonSerializer.Serialize(citations);

            using (var context = new AppDbContext(_options))
            {
                context.Sessions.Add(new ChatSessionEntity { Id = sessionId, Title = "Financial Analysis" });
                await context.SaveChangesAsync();

                var service = new ChatSessionService(context);
                await service.AddMessageAsync(sessionId, "Assistant", "Net margin grew by 14% [1].", json);
            }

            using (var verifyContext = new AppDbContext(_options))
            {
                var service = new ChatSessionService(verifyContext);
                var session = await service.GetSessionAsync(sessionId);

                session.Should().NotBeNull();
                session!.Messages.Should().HaveCount(1);
                var msg = session.Messages[0];
                msg.Role.Should().Be("Assistant");
                msg.CitationJson.Should().Be(json);

                // Re-hydrate in ViewModel
                var itemVm = new ChatMessageItemViewModel();
                itemVm.LoadCitationsFromJson(msg.CitationJson!);
                itemVm.HasCitations.Should().BeTrue();
                itemVm.Citations[0].DocumentName.Should().Be("Q3_Report.xlsx");
            }
        }

        private class FakeAiClientFactory : IAIClientFactory
        {
            public IChatClient CreateChatClient(ProviderConfigEntity provider, string modelId) =>
                throw new NotImplementedException();

            public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(ProviderConfigEntity provider, string modelId)
            {
                return new FakeEmbeddingGenerator();
            }

            public Task<(bool Success, string Message)> TestConnectionAsync(ProviderConfigEntity provider, string modelId, CancellationToken ct = default) =>
                Task.FromResult((true, "OK"));

            public Task<(bool Success, string Message, int Dimensions)> TestEmbeddingGenerationAsync(ProviderConfigEntity provider, string modelId, CancellationToken ct = default) =>
                Task.FromResult((true, "OK", 4));

            public Task<(bool Success, string Message)> ValidateProviderAsync(ProviderConfigEntity provider, string? modelId = null, CancellationToken ct = default) =>
                Task.FromResult((true, "OK"));
        }

        private class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
        {
            public EmbeddingGeneratorMetadata Metadata => new("FakeGenerator");

            public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
                IEnumerable<string> values,
                EmbeddingGenerationOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                var embeddings = new List<Embedding<float>>();
                foreach (var _ in values)
                {
                    embeddings.Add(new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f, 0.4f }));
                }

                return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
            }

            public object? GetService(Type serviceType, object? serviceKey = null) => null;
            public void Dispose() { }
        }

        private class FakeVectorStoreService : IVectorStoreService
        {
            public List<ScoredChunkResult> StubbedResults { get; } = new();

            public Task<IReadOnlyList<ScoredChunkResult>> SearchAsync(
                ReadOnlyMemory<float> queryVector,
                IReadOnlyList<Guid> knowledgebaseIds,
                int topK = 5,
                float minSimilarity = 0.5f,
                CancellationToken ct = default)
            {
                var filtered = StubbedResults
                    .Where(r => knowledgebaseIds.Contains(r.KnowledgebaseId) && r.Score >= minSimilarity)
                    .OrderByDescending(r => r.Score)
                    .Take(topK)
                    .ToList();

                return Task.FromResult<IReadOnlyList<ScoredChunkResult>>(filtered);
            }

            public Task StoreChunksAsync(IEnumerable<DocumentChunkEntity> chunks, CancellationToken ct = default) => Task.CompletedTask;
            public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default) => Task.CompletedTask;
            public Task DeleteByKnowledgebaseIdAsync(Guid knowledgebaseId, CancellationToken ct = default) => Task.CompletedTask;
            public Task<int> GetChunkCountAsync(Guid knowledgebaseId, CancellationToken ct = default) => Task.FromResult(StubbedResults.Count);
        }
    }
}

