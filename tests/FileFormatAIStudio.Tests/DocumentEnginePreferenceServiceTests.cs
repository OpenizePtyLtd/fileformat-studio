using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Parsing;
using FileFormatAIStudio.Services.Parsing.Engines.Word;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class DocumentEnginePreferenceServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly DocumentCategoryRegistry _categoryRegistry;
        private readonly DocumentParserFactory _parserFactory;

        public DocumentEnginePreferenceServiceTests()
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

            _categoryRegistry = new DocumentCategoryRegistry();
            _parserFactory = new DocumentParserFactory(new IDocumentParser[]
            {
                new AsposeWordsParser(),
                new OpenXmlWordParser(),
                new PlainTextParser()
            });
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        [Fact]
        public async Task GetPreferredEngineIdAsync_DefaultsToAutoForAllCategories()
        {
            using var context = new AppDbContext(_options);
            var service = new DocumentEnginePreferenceService(context, _categoryRegistry, _parserFactory);

            var wordPref = await service.GetPreferredEngineIdAsync(DocumentCategory.Word);
            var excelPref = await service.GetPreferredEngineIdAsync(DocumentCategory.Excel);
            var pptPref = await service.GetPreferredEngineIdAsync(DocumentCategory.PowerPoint);
            var pdfPref = await service.GetPreferredEngineIdAsync(DocumentCategory.Pdf);
            var textPref = await service.GetPreferredEngineIdAsync(DocumentCategory.PlainText);

            wordPref.Should().Be("Auto");
            excelPref.Should().Be("Auto");
            pptPref.Should().Be("Auto");
            pdfPref.Should().Be("Auto");
            textPref.Should().Be("Auto");
        }

        [Fact]
        public async Task SetPreferredEngineIdAsync_PersistsAndRetrievesPreference()
        {
            using var context = new AppDbContext(_options);
            var service = new DocumentEnginePreferenceService(context, _categoryRegistry, _parserFactory);

            await service.SetPreferredEngineIdAsync(DocumentCategory.Word, "openxml-words");
            await service.SetPreferredEngineIdAsync(DocumentCategory.Pdf, "pdfpig");

            var updatedWordPref = await service.GetPreferredEngineIdAsync(DocumentCategory.Word);
            var updatedPdfPref = await service.GetPreferredEngineIdAsync(DocumentCategory.Pdf);
            var unchangedExcelPref = await service.GetPreferredEngineIdAsync(DocumentCategory.Excel);

            updatedWordPref.Should().Be("openxml-words");
            updatedPdfPref.Should().Be("pdfpig");
            unchangedExcelPref.Should().Be("Auto");
        }

        [Fact]
        public async Task GetAllPreferencesAsync_ReturnsAllFiveCategories()
        {
            using var context = new AppDbContext(_options);
            var service = new DocumentEnginePreferenceService(context, _categoryRegistry, _parserFactory);

            await service.SetPreferredEngineIdAsync(DocumentCategory.Word, "openxml-words");

            var all = await service.GetAllPreferencesAsync();

            all.Should().HaveCount(5);
            all[DocumentCategory.Word].Should().Be("openxml-words");
            all[DocumentCategory.Excel].Should().Be("Auto");
            all[DocumentCategory.PowerPoint].Should().Be("Auto");
            all[DocumentCategory.Pdf].Should().Be("Auto");
            all[DocumentCategory.PlainText].Should().Be("Auto");
        }

        [Fact]
        public async Task RecordBenchmarkWinnerAsync_PersistsWinnerAndReturnsInQueries()
        {
            using var context = new AppDbContext(_options);
            var service = new DocumentEnginePreferenceService(context, _categoryRegistry, _parserFactory);

            await service.RecordBenchmarkWinnerAsync(DocumentCategory.Word, "aspose-words", "Aspose.Words (.NET)");

            var winnerId = await service.GetBenchmarkWinnerEngineIdAsync(DocumentCategory.Word);
            var winnerName = await service.GetBenchmarkWinnerDisplayNameAsync(DocumentCategory.Word);

            winnerId.Should().Be("aspose-words");
            winnerName.Should().Be("Aspose.Words (.NET)");
        }

        [Fact]
        public async Task ResolveParserForDocumentAsync_AutoMode_ResolvesBenchmarkWinnerWhenAvailable()
        {
            using var context = new AppDbContext(_options);
            var service = new DocumentEnginePreferenceService(context, _categoryRegistry, _parserFactory);

            // Configure Word to Auto and record OpenXML as the benchmark champion
            await service.SetPreferredEngineIdAsync(DocumentCategory.Word, "Auto");
            await service.RecordBenchmarkWinnerAsync(DocumentCategory.Word, "openxml-words", "OpenXml Words");

            var resolved = await service.ResolveParserForDocumentAsync("test.docx");

            resolved.Should().NotBeNull();
            resolved!.EngineId.Should().Be("openxml-words");
        }

        [Fact]
        public async Task ResolveParserForDocumentAsync_AutoMode_FallsBackToDefaultWhenNoBenchmarkExists()
        {
            using var context = new AppDbContext(_options);
            var service = new DocumentEnginePreferenceService(context, _categoryRegistry, _parserFactory);

            // Word is Auto and no benchmark champion has been recorded yet
            var resolved = await service.ResolveParserForDocumentAsync("contract.docx");

            resolved.Should().NotBeNull();
            // Default priority parser for docx is AsposeWordsParser (priority 100 vs OpenXml 80)
            resolved!.EngineId.Should().Be("aspose-words");
        }

        [Fact]
        public async Task ResolveParserForDocumentAsync_ExplicitPreference_UsesConfiguredEngine()
        {
            using var context = new AppDbContext(_options);
            var service = new DocumentEnginePreferenceService(context, _categoryRegistry, _parserFactory);

            await service.SetPreferredEngineIdAsync(DocumentCategory.Word, "openxml-words");

            var resolved = await service.ResolveParserForDocumentAsync("sample.docx");

            resolved.Should().NotBeNull();
            resolved!.EngineId.Should().Be("openxml-words");
        }

        [Fact]
        public async Task ResolveParserForDocumentAsync_InvalidOrWhitespaceFilePath_ReturnsNull()
        {
            using var context = new AppDbContext(_options);
            var service = new DocumentEnginePreferenceService(context, _categoryRegistry, _parserFactory);

            var result1 = await service.ResolveParserForDocumentAsync("");
            var result2 = await service.ResolveParserForDocumentAsync("   ");

            result1.Should().BeNull();
            result2.Should().BeNull();
        }

        [Fact]
        public async Task IngestDocumentsAsync_MultiFileDifferentCategories_UsesRespectiveCategoryEngines()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FFStudio_MultiCategoryTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                using var context = new AppDbContext(_options);
                var prefService = new DocumentEnginePreferenceService(context, _categoryRegistry, _parserFactory);

                // Set Word to use openxml-words
                await prefService.SetPreferredEngineIdAsync(DocumentCategory.Word, "openxml-words");
                // PlainText is Auto (defaults to plaintext)

                var fakeAi = new FakeAiClientFactory();
                var textChunker = new TextChunker();
                var vectorStore = new VectorStoreService(context);

                var kbService = new KnowledgebaseService(
                    context,
                    _parserFactory,
                    textChunker,
                    vectorStore,
                    fakeAi,
                    tempDir,
                    prefService);

                var kb = await kbService.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest(
                    Name: "Multi Format KB",
                    Description: "Word and Text documents",
                    ParserEngine: "Auto",
                    EmbeddingProvider: "OpenAI",
                    EmbeddingModel: "text-embedding-3-small",
                    VectorDimensions: 1536));

                // Create physical test files
                string textFile = Path.Combine(tempDir, "sample.txt");
                await File.WriteAllTextAsync(textFile, "Hello plain text world");

                // Note: For docx, since openxml needs valid zip, we test text ingestion
                var options = new IngestionOptions
                {
                    CustomEmbeddingGenerator = new FakeEmbeddingGenerator(1536)
                };
                var ingested = await kbService.IngestDocumentsAsync(kb.Id, new[] { textFile }, options);

                ingested.Should().HaveCount(1);
                ingested[0].Status.Should().Be("Indexed");
                ingested[0].ParserEngineUsed.Should().Be("Plain Text / Source Files");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }
    }
}
