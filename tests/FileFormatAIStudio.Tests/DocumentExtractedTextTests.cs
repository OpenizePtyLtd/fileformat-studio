using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Parsing;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class DocumentExtractedTextTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly string _tempStorageDir;
        private readonly FakeAiClientFactory _fakeAiFactory;
        private readonly DocumentParserFactory _parserFactory;
        private readonly TextChunker _textChunker;

        public DocumentExtractedTextTests()
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

            _tempStorageDir = Path.Combine(Path.GetTempPath(), "FFStudio_ExtractedTextTests_" + Guid.NewGuid().ToString("N"));
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
        public async Task GetDocumentExtractedTextAsync_WhenRawExtractedTextIsStored_ReturnsVerbatimText()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kb = new KnowledgebaseEntity
            {
                Id = Guid.NewGuid(),
                Name = "House Architecture",
                Description = "Floor plans and blueprint archives",
                ParserEngine = "Auto",
                EmbeddingProvider = "FakeProvider",
                EmbeddingModel = "fake-model",
                VectorDimensions = 1536
            };
            context.Knowledgebases.Add(kb);

            const string expectedVerbatimText = "ARCHITECTURAL FLOOR PLAN - LEVEL 1\nBedroom 1: 14' x 12'\nBedroom 2: 12' x 11'\nMaster Suite: 18' x 15'";
            var doc = new KnowledgebaseDocumentEntity
            {
                Id = Guid.NewGuid(),
                KnowledgebaseId = kb.Id,
                FileName = "FloorPlan_A1.pdf",
                FilePath = "C:\\dummy\\FloorPlan_A1.pdf",
                FileType = ".pdf",
                FileSize = 1024,
                Status = "Indexed",
                ParserEngineUsed = "PdfPig",
                ChunkCount = 3,
                RawExtractedText = expectedVerbatimText
            };
            context.KnowledgebaseDocuments.Add(doc);
            await context.SaveChangesAsync();

            var result = await service.GetDocumentExtractedTextAsync(doc.Id);

            result.Should().Be(expectedVerbatimText);
        }

        [Fact]
        public async Task GetDocumentExtractedTextAsync_WhenRawExtractedTextIsNull_ReconstructsFromSequentialChunks()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kb = new KnowledgebaseEntity
            {
                Id = Guid.NewGuid(),
                Name = "Legacy KB",
                Description = "Pre-migration knowledgebase",
                ParserEngine = "Auto",
                EmbeddingProvider = "FakeProvider",
                EmbeddingModel = "fake-model",
                VectorDimensions = 1536
            };
            context.Knowledgebases.Add(kb);

            var doc = new KnowledgebaseDocumentEntity
            {
                Id = Guid.NewGuid(),
                KnowledgebaseId = kb.Id,
                FileName = "LegacySpecs.docx",
                FilePath = "C:\\dummy\\LegacySpecs.docx",
                FileType = ".docx",
                FileSize = 2048,
                Status = "Indexed",
                ParserEngineUsed = "OpenXml",
                ChunkCount = 3,
                RawExtractedText = null // Legacy document before migration
            };
            context.KnowledgebaseDocuments.Add(doc);

            // Add chunks out of order to ensure OrderBy(c => c.ChunkIndex) is validated
            context.DocumentChunks.AddRange(
                new DocumentChunkEntity
                {
                    Id = Guid.NewGuid(),
                    DocumentId = doc.Id,
                    KnowledgebaseId = kb.Id,
                    ChunkIndex = 2,
                    TextContent = "Section 3: Conclusion and Signoff",
                    TokenCount = 10
                },
                new DocumentChunkEntity
                {
                    Id = Guid.NewGuid(),
                    DocumentId = doc.Id,
                    KnowledgebaseId = kb.Id,
                    ChunkIndex = 0,
                    TextContent = "Section 1: Scope of Work",
                    TokenCount = 10
                },
                new DocumentChunkEntity
                {
                    Id = Guid.NewGuid(),
                    DocumentId = doc.Id,
                    KnowledgebaseId = kb.Id,
                    ChunkIndex = 1,
                    TextContent = "Section 2: Material Specifications",
                    TokenCount = 10
                }
            );
            await context.SaveChangesAsync();

            var result = await service.GetDocumentExtractedTextAsync(doc.Id);

            result.Should().Contain("Section 1: Scope of Work");
            result.Should().Contain("Section 2: Material Specifications");
            result.Should().Contain("Section 3: Conclusion and Signoff");

            int idx1 = result.IndexOf("Section 1: Scope of Work", StringComparison.Ordinal);
            int idx2 = result.IndexOf("Section 2: Material Specifications", StringComparison.Ordinal);
            int idx3 = result.IndexOf("Section 3: Conclusion and Signoff", StringComparison.Ordinal);

            idx1.Should().BeLessThan(idx2);
            idx2.Should().BeLessThan(idx3);
        }

        [Fact]
        public async Task GetDocumentExtractedTextAsync_WhenDocumentNotFound_ReturnsEmptyString()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var result = await service.GetDocumentExtractedTextAsync(Guid.NewGuid());

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetDocumentChunksAsync_ReturnsChunksInAscendingIndexOrder()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kbId = Guid.NewGuid();
            var docId = Guid.NewGuid();

            context.Knowledgebases.Add(new KnowledgebaseEntity
            {
                Id = kbId,
                Name = "KB",
                ParserEngine = "Auto",
                EmbeddingProvider = "Fake",
                EmbeddingModel = "fake",
                VectorDimensions = 1536
            });

            context.KnowledgebaseDocuments.Add(new KnowledgebaseDocumentEntity
            {
                Id = docId,
                KnowledgebaseId = kbId,
                FileName = "test.txt",
                FilePath = "test.txt",
                FileType = ".txt"
            });

            context.DocumentChunks.AddRange(
                new DocumentChunkEntity { Id = Guid.NewGuid(), DocumentId = docId, KnowledgebaseId = kbId, ChunkIndex = 5, TextContent = "Fifth" },
                new DocumentChunkEntity { Id = Guid.NewGuid(), DocumentId = docId, KnowledgebaseId = kbId, ChunkIndex = 1, TextContent = "First" },
                new DocumentChunkEntity { Id = Guid.NewGuid(), DocumentId = docId, KnowledgebaseId = kbId, ChunkIndex = 3, TextContent = "Third" }
            );
            await context.SaveChangesAsync();

            var chunks = await service.GetDocumentChunksAsync(docId);

            chunks.Should().HaveCount(3);
            chunks[0].ChunkIndex.Should().Be(1);
            chunks[1].ChunkIndex.Should().Be(3);
            chunks[2].ChunkIndex.Should().Be(5);
        }

        [Fact]
        public async Task IngestDocumentsAsync_StoresRawExtractedTextDirectlyInDatabase()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            context.Providers.Add(new ProviderConfigEntity
            {
                Id = Guid.NewGuid(),
                Name = "FakeProvider",
                ProviderType = "FakeProvider",
                IsEnabled = true
            });

            var kb = new KnowledgebaseEntity
            {
                Id = Guid.NewGuid(),
                Name = "Blueprints KB",
                Description = "House blueprints and room layouts",
                ParserEngine = "Auto",
                EmbeddingProvider = "FakeProvider",
                EmbeddingModel = "fake-model",
                VectorDimensions = 1536
            };
            context.Knowledgebases.Add(kb);
            await context.SaveChangesAsync();

            // Create temporary test file with house plan details
            string testFilePath = Path.Combine(_tempStorageDir, "house_plan_notes.txt");
            const string expectedFileContent = "House Plan 101:\n- Ground Floor: 2 Bed Rooms, 1 Bath, Living Room\n- First Floor: 3 Bed Rooms, 2 Bath\nTotal Bedrooms: 5";
            await File.WriteAllTextAsync(testFilePath, expectedFileContent);

            var ingested = await service.IngestDocumentsAsync(kb.Id, [testFilePath]);

            ingested.Should().HaveCount(1);
            var docId = ingested[0].Id;

            // Query database directly to assert RawExtractedText column was saved
            var savedDoc = await context.KnowledgebaseDocuments.FirstOrDefaultAsync(d => d.Id == docId);
            savedDoc.Should().NotBeNull();
            savedDoc!.RawExtractedText.Should().Be(expectedFileContent);

            // Also verify GetDocumentExtractedTextAsync returns this text
            var retrievedText = await service.GetDocumentExtractedTextAsync(docId);
            retrievedText.Should().Be(expectedFileContent);
        }

        [Fact]
        public async Task ViewExtractedTextViewModel_ComputesCharacterWordAndLineCountsAccurately()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kbId = Guid.NewGuid();
            var docId = Guid.NewGuid();

            const string docText = "First line with four words.\nSecond line with five words here.\nThird line.";
            context.Knowledgebases.Add(new KnowledgebaseEntity
            {
                Id = kbId,
                Name = "KB",
                ParserEngine = "Auto",
                EmbeddingProvider = "Fake",
                EmbeddingModel = "fake",
                VectorDimensions = 1536
            });

            var docEntity = new KnowledgebaseDocumentEntity
            {
                Id = docId,
                KnowledgebaseId = kbId,
                FileName = "FloorPlanReport.pdf",
                FilePath = "FloorPlanReport.pdf",
                FileType = ".pdf",
                FileSize = 54321,
                Status = "Indexed",
                ParserEngineUsed = "dotnet_oss",
                ChunkCount = 1,
                RawExtractedText = docText
            };
            context.KnowledgebaseDocuments.Add(docEntity);

            context.DocumentChunks.Add(new DocumentChunkEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = docId,
                KnowledgebaseId = kbId,
                ChunkIndex = 0,
                TextContent = docText,
                TokenCount = 20
            });
            await context.SaveChangesAsync();

            var vm = new ViewExtractedTextViewModel(service);
            await vm.LoadDocumentAsync(docId, docEntity);

            vm.DocumentName.Should().Be("FloorPlanReport.pdf");
            vm.FileType.Should().Be("PDF");
            vm.ParserEngineUsed.Should().Be(".NET OSS");
            vm.ExtractedText.Should().Be(docText);
            vm.CharacterCount.Should().Be(docText.Length);
            vm.WordCount.Should().Be(13); // 5 words + 6 words + 2 words = 13 words
            vm.LineCount.Should().Be(3);
            vm.HasExtractedText.Should().BeTrue();
            vm.HasChunks.Should().BeTrue();
            vm.Chunks.Should().HaveCount(1);
            vm.Chunks[0].TokenCount.Should().Be(20);
        }

        [Fact]
        public void ViewExtractedTextViewModel_WordWrapToggle_SwitchesTextWrappingMode()
        {
            using var context = new AppDbContext(_options);
            var service = CreateService(context);
            var vm = new ViewExtractedTextViewModel(service);

            vm.IsWordWrap.Should().BeTrue();
            vm.TextWrappingMode.Should().Be(Microsoft.UI.Xaml.TextWrapping.Wrap);

            vm.IsWordWrap = false;
            vm.TextWrappingMode.Should().Be(Microsoft.UI.Xaml.TextWrapping.NoWrap);

            vm.IsWordWrap = true;
            vm.TextWrappingMode.Should().Be(Microsoft.UI.Xaml.TextWrapping.Wrap);
        }
    }
}
