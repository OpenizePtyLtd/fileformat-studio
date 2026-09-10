using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Parsing;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class KnowledgebaseServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly string _tempStorageDir;
        private readonly FakeAiClientFactory _fakeAiFactory;
        private readonly DocumentParserFactory _parserFactory;
        private readonly TextChunker _textChunker;

        public KnowledgebaseServiceTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using (var context = new AppDbContext(_options))
            {
                context.Database.EnsureCreated();
            }

            _tempStorageDir = Path.Combine(Path.GetTempPath(), "FFStudio_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempStorageDir);

            _fakeAiFactory = new FakeAiClientFactory();
            _parserFactory = new DocumentParserFactory([new PlainTextParser()]);
            _textChunker = new TextChunker();
        }

        public void Dispose()
        {
            _connection.Dispose();

            if (Directory.Exists(_tempStorageDir))
            {
                try
                {
                    Directory.Delete(_tempStorageDir, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        private KnowledgebaseService CreateService(AppDbContext context)
        {
            var vectorStore = new VectorStoreService(context);
            return new KnowledgebaseService(
                context,
                _parserFactory,
                _textChunker,
                vectorStore,
                _fakeAiFactory,
                _tempStorageDir);
        }

        [Fact]
        public async Task CreateKnowledgebaseAsync_CreatesEntityWithConfiguredProperties()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var request = new CreateKnowledgebaseRequest(
                Name: "Legal Documents",
                Description: "Internal contracts and NDA repository",
                ParserEngine: "Auto",
                EmbeddingProvider: "OpenAI",
                EmbeddingModel: "text-embedding-3-small",
                VectorDimensions: 1536);

            var created = await service.CreateKnowledgebaseAsync(request);

            created.Should().NotBeNull();
            created.Id.Should().NotBeEmpty();
            created.Name.Should().Be("Legal Documents");
            created.Description.Should().Be("Internal contracts and NDA repository");
            created.VectorDimensions.Should().Be(1536);
            created.EmbeddingModel.Should().Be("text-embedding-3-small");

            var fetched = await service.GetKnowledgebaseByIdAsync(created.Id);
            fetched.Should().NotBeNull();
            fetched!.Name.Should().Be("Legal Documents");
        }

        [Fact]
        public async Task CreateKnowledgebaseAsync_ThrowsOnEmptyName()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var act = () => service.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest("   "));
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task UpdateKnowledgebaseAsync_UpdatesFieldsSuccessfully()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var created = await service.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest("Old Name", "Old Desc"));
            var updated = await service.UpdateKnowledgebaseAsync(created.Id, new UpdateKnowledgebaseRequest("New Name", "New Desc", "dotnet-oss"));

            updated.Name.Should().Be("New Name");
            updated.Description.Should().Be("New Desc");
            updated.ParserEngine.Should().Be("dotnet-oss");

            var fetched = await service.GetKnowledgebaseByIdAsync(created.Id);
            fetched!.Name.Should().Be("New Name");
        }

        [Fact]
        public async Task DeleteKnowledgebaseAsync_RemovesEntityAndStorageDirectory()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kb = await service.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest("To Delete"));
            string kbFolder = Path.Combine(_tempStorageDir, kb.Id.ToString());
            Directory.CreateDirectory(kbFolder);
            File.WriteAllText(Path.Combine(kbFolder, "test.txt"), "hello");

            await service.DeleteKnowledgebaseAsync(kb.Id);

            var fetched = await service.GetKnowledgebaseByIdAsync(kb.Id);
            fetched.Should().BeNull();
            Directory.Exists(kbFolder).Should().BeFalse();
        }

        [Fact]
        public async Task IngestDocumentsAsync_ProcessesAndIndexesTextFilesEndToEnd()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kb = await service.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest(
                Name: "Tech Specs",
                EmbeddingProvider: "OpenAI",
                EmbeddingModel: "text-embedding-3-small",
                VectorDimensions: 1536));

            // Create temporary test files
            string file1 = Path.Combine(_tempStorageDir, "source1.txt");
            string file2 = Path.Combine(_tempStorageDir, "source2.md");
            await File.WriteAllTextAsync(file1, "This is the first sample document containing detailed specifications about system architecture.");
            await File.WriteAllTextAsync(file2, "This is the second sample document detailing network configuration and protocols.");

            var progressReports = new List<IndexingProgressReport>();
            var progress = new Progress<IndexingProgressReport>(r => progressReports.Add(r));

            var options = new IngestionOptions
            {
                CustomEmbeddingGenerator = new FakeEmbeddingGenerator(1536),
                EmbeddingBatchSize = 8
            };

            var documents = await service.IngestDocumentsAsync(kb.Id, [file1, file2], options, progress);

            documents.Should().HaveCount(2);
            documents.Should().OnlyContain(d => d.Status == "Indexed");
            documents.Should().OnlyContain(d => d.ChunkCount > 0);
            documents.Should().OnlyContain(d => d.IndexedAt != null);

            // Verify stored chunks in DB
            var storedChunks = await context.DocumentChunks.Where(c => c.KnowledgebaseId == kb.Id).ToListAsync();
            storedChunks.Should().NotBeEmpty();
            storedChunks.Should().OnlyContain(c => c.EmbeddingVector.Length == 1536 * sizeof(float));

            // Verify physical files copied
            string kbFolder = Path.Combine(_tempStorageDir, kb.Id.ToString());
            Directory.GetFiles(kbFolder).Should().HaveCount(2);

            // Verify progress callbacks
            progressReports.Should().Contain(r => r.Stage == IndexingStage.Starting);
            progressReports.Should().Contain(r => r.Stage == IndexingStage.Extracting);
            progressReports.Should().Contain(r => r.Stage == IndexingStage.Completed);
        }

        [Fact]
        public async Task IngestDocumentsAsync_DimensionMismatch_MarksDocumentAsFailed()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kb = await service.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest(
                Name: "Strict 1536 KB",
                VectorDimensions: 1536));

            string file1 = Path.Combine(_tempStorageDir, "mismatch.txt");
            await File.WriteAllTextAsync(file1, "Valid content that will receive wrong dimension embedding.");

            // Generator returns 768 dimensions instead of required 1536
            var options = new IngestionOptions
            {
                CustomEmbeddingGenerator = new FakeEmbeddingGenerator(dimensions: 768)
            };

            var documents = await service.IngestDocumentsAsync(kb.Id, [file1], options);

            documents.Should().HaveCount(1);
            documents[0].Status.Should().Be("Failed");
            documents[0].ErrorMessage.Should().Contain("Dimension mismatch");
            documents[0].ChunkCount.Should().Be(0);

            // Chunks should NOT have been saved
            var storedChunks = await context.DocumentChunks.Where(c => c.KnowledgebaseId == kb.Id).ToListAsync();
            storedChunks.Should().BeEmpty();
        }

        [Fact]
        public async Task IngestDocumentsAsync_PartialFailure_ContinuesWithOtherDocuments()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kb = await service.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest("Batch KB"));

            string validFile = Path.Combine(_tempStorageDir, "valid.txt");
            string unsupportedFile = Path.Combine(_tempStorageDir, "unsupported.xyz123");
            await File.WriteAllTextAsync(validFile, "Valid content that should parse without error.");
            await File.WriteAllTextAsync(unsupportedFile, "Unsupported file content.");

            var options = new IngestionOptions
            {
                CustomEmbeddingGenerator = new FakeEmbeddingGenerator(1536)
            };

            var documents = await service.IngestDocumentsAsync(kb.Id, [unsupportedFile, validFile], options);

            documents.Should().HaveCount(2);

            var failedDoc = documents.First(d => d.FileName.Contains("unsupported"));
            failedDoc.Status.Should().Be("Failed");
            failedDoc.ErrorMessage.Should().NotBeNullOrWhiteSpace();

            var successDoc = documents.First(d => d.FileName.Contains("valid"));
            successDoc.Status.Should().Be("Indexed");
            successDoc.ChunkCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task IngestDocumentsAsync_ThrowsOnNonExistentSourceFile()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kb = await service.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest("Test KB"));
            string nonExistentFile = Path.Combine(_tempStorageDir, "non_existent_file_12345.txt");

            var act = () => service.IngestDocumentsAsync(kb.Id, [nonExistentFile]);
            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task IngestDocumentsAsync_CancellationRequested_CancelsGracefully()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kb = await service.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest("Cancel KB"));

            string file1 = Path.Combine(_tempStorageDir, "cancel_test.txt");
            await File.WriteAllTextAsync(file1, "Content that will be cancelled.");

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancelled token

            var act = () => service.IngestDocumentsAsync(kb.Id, [file1], ct: cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task DeleteDocumentAsync_RemovesDocumentChunksAndPhysicalFile()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kb = await service.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest("Doc Delete KB"));

            string sourceFile = Path.Combine(_tempStorageDir, "to_delete_doc.txt");
            await File.WriteAllTextAsync(sourceFile, "Document content to be deleted later.");

            var options = new IngestionOptions
            {
                CustomEmbeddingGenerator = new FakeEmbeddingGenerator(1536)
            };

            var documents = await service.IngestDocumentsAsync(kb.Id, [sourceFile], options);
            var doc = documents[0];

            File.Exists(doc.FilePath).Should().BeTrue();
            var chunkCountBefore = await context.DocumentChunks.CountAsync(c => c.DocumentId == doc.Id);
            chunkCountBefore.Should().BeGreaterThan(0);

            await service.DeleteDocumentAsync(doc.Id);

            var fetchedDoc = await service.GetDocumentByIdAsync(doc.Id);
            fetchedDoc.Should().BeNull();
            File.Exists(doc.FilePath).Should().BeFalse();

            var chunkCountAfter = await context.DocumentChunks.CountAsync(c => c.DocumentId == doc.Id);
            chunkCountAfter.Should().Be(0);
        }

        [Fact]
        public async Task IngestDocumentsAsync_ResolvesProviderFromDb_WhenNoCustomGeneratorProvided()
        {
            using var context = new AppDbContext(_options);

            // Add a configured provider to the DB
            var provider = new ProviderConfigEntity
            {
                Id = Guid.NewGuid(),
                Name = "OpenAI",
                ProviderType = "OpenAI",
                EndpointUrl = "https://api.openai.com/v1",
                ApiKey = "sk-test-key",
                CreatedAt = DateTime.UtcNow
            };
            context.Providers.Add(provider);
            await context.SaveChangesAsync();

            var service = CreateService(context);

            var kb = await service.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest(
                Name: "Provider Auto Resolve KB",
                EmbeddingProvider: "OpenAI",
                EmbeddingModel: "text-embedding-3-small",
                VectorDimensions: 1536));

            string testFile = Path.Combine(_tempStorageDir, "auto_provider.txt");
            await File.WriteAllTextAsync(testFile, "Auto resolve provider test content.");

            var docs = await service.IngestDocumentsAsync(kb.Id, [testFile]);

            docs.Should().HaveCount(1);
            docs[0].Status.Should().Be("Indexed");
            docs[0].ChunkCount.Should().BeGreaterThan(0);
        }
    }

    internal sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly int _dimensions;

        public FakeEmbeddingGenerator(int dimensions = 1536)
        {
            _dimensions = dimensions;
        }

        public EmbeddingGeneratorMetadata Metadata => new("FakeEmbeddingGenerator");

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var result = new GeneratedEmbeddings<Embedding<float>>();
            foreach (var val in values)
            {
                var vec = new float[_dimensions];
                vec[0] = 0.5f;
                result.Add(new Embedding<float>(vec));
            }
            return Task.FromResult(result);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    internal sealed class FakeAiClientFactory : IAIClientFactory
    {
        public List<string?> TestedModelIds { get; } = new();
        public Func<string?, (bool Success, string Message)>? ValidateResultFunc { get; set; }

        public IChatClient CreateChatClient(ProviderConfigEntity provider, string modelId) => throw new NotImplementedException();

        public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(ProviderConfigEntity provider, string modelId)
        {
            int dims = EmbeddingModelMetadata.GetKnownDimensions(modelId) ?? 1536;
            return new FakeEmbeddingGenerator(dims);
        }

        public Task<(bool Success, string Message)> TestConnectionAsync(ProviderConfigEntity provider, string modelId, CancellationToken ct = default)
        {
            TestedModelIds.Add(modelId);
            if (ValidateResultFunc != null) return Task.FromResult(ValidateResultFunc(modelId));
            return Task.FromResult((true, "OK"));
        }

        public Task<(bool Success, string Message, int Dimensions)> TestEmbeddingGenerationAsync(ProviderConfigEntity provider, string modelId, CancellationToken ct = default)
        {
            TestedModelIds.Add(modelId);
            if (ValidateResultFunc != null)
            {
                var res = ValidateResultFunc(modelId);
                return Task.FromResult((res.Success, res.Message, 1536));
            }
            return Task.FromResult((true, "OK", 1536));
        }

        public Task<(bool Success, string Message)> ValidateProviderAsync(ProviderConfigEntity provider, string? modelId = null, CancellationToken ct = default)
        {
            TestedModelIds.Add(modelId);
            if (ValidateResultFunc != null) return Task.FromResult(ValidateResultFunc(modelId));
            return Task.FromResult((true, "OK"));
        }
    }
}

