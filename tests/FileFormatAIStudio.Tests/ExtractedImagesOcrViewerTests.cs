using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Parsing;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class ExtractedImagesOcrViewerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly string _tempStorageDir;
        private readonly FakeAiClientFactory _fakeAiFactory;
        private readonly DocumentParserFactory _parserFactory;
        private readonly TextChunker _textChunker;

        public ExtractedImagesOcrViewerTests()
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

            _tempStorageDir = Path.Combine(Path.GetTempPath(), "FFStudio_OcrViewerTests_" + Guid.NewGuid().ToString("N"));
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
        public void ParseOcrItems_IdentifiesPdfPageOcrBlocks()
        {
            // Arrange
            string docText = "Introduction to document.\n\n[Page 2 Image 1 Text: Confidential Report Draft]\n\nConclusion.";

            // Act
            var items = ViewExtractedTextViewModel.ParseOcrItems(docText, "pdf");

            // Assert
            items.Should().HaveCount(1);
            var item = items[0];
            item.Title.Should().Be("Page 2 Image 1");
            item.SourceContext.Should().Be("Scanned PDF Page Image");
            item.OcrText.Should().Be("Confidential Report Draft");
            item.CharacterCount.Should().Be("Confidential Report Draft".Length);
            item.Glyph.Should().Be("\uEA90");
        }

        [Fact]
        public void ParseOcrItems_IdentifiesWordAndPowerPointFigures()
        {
            // Arrange
            string docText = "Word Doc Start\n[Embedded Image 1 Text: System Flowchart v2]\nMiddle\n[Slide 4 Image 2 Text: Revenue Chart Q3 2026]\nEnd";

            // Act
            var items = ViewExtractedTextViewModel.ParseOcrItems(docText, "docx");

            // Assert
            items.Should().HaveCount(2);

            var wordFig = items[0];
            wordFig.Title.Should().Be("Embedded Image 1");
            wordFig.SourceContext.Should().Be("Word Document Figure");
            wordFig.OcrText.Should().Be("System Flowchart v2");

            var pptFig = items[1];
            pptFig.Title.Should().Be("Slide 4 Image 2");
            pptFig.SourceContext.Should().Be("PowerPoint Slide Picture");
            pptFig.OcrText.Should().Be("Revenue Chart Q3 2026");
        }

        [Fact]
        public void ParseOcrItems_IdentifiesDiagramDescription()
        {
            // Arrange
            string docText = "Diagram below:\n[Diagram: Microservices communication topology showing API Gateway and auth service]";

            // Act
            var items = ViewExtractedTextViewModel.ParseOcrItems(docText, "pdf");

            // Assert
            items.Should().HaveCount(1);
            items[0].Title.Should().Be("Diagram");
            items[0].SourceContext.Should().Be("AI Vision Diagram Description");
            items[0].OcrText.Should().Be("Microservices communication topology showing API Gateway and auth service");
        }

        [Fact]
        public void ParseOcrItems_HandlesStandaloneImageFile()
        {
            // Arrange
            string rawOcrText = "Receipt #98721\nTotal: $45.50\nThank you for your business!";

            // Act
            var items = ViewExtractedTextViewModel.ParseOcrItems(rawOcrText, "png");

            // Assert
            items.Should().HaveCount(1);
            items[0].Title.Should().Be("Image OCR Text");
            items[0].SourceContext.Should().Be("PNG Image File");
            items[0].OcrText.Should().Be(rawOcrText);
        }

        [Fact]
        public void ParseOcrItems_ReturnsEmptyForPureNativeDigitalDocument()
        {
            // Arrange
            string docText = "This is a standard PDF document containing only digital text streams with no OCR markers.";

            // Act
            var items = ViewExtractedTextViewModel.ParseOcrItems(docText, "pdf");

            // Assert
            items.Should().BeEmpty();
        }

        [Fact]
        public async Task LoadDocumentAsync_PopulatesOcrItemsAndCount()
        {
            // Arrange
            using var context = new AppDbContext(_options);
            var service = CreateService(context);

            var kbId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            const string docText = "Invoice Header\n[Page 1 Image 1 Text: Total Due: $1,250.00 Net 30]\nFooter Notes";

            context.Knowledgebases.Add(new KnowledgebaseEntity
            {
                Id = kbId,
                Name = "Test KB",
                ParserEngine = "Auto",
                EmbeddingProvider = "Fake",
                EmbeddingModel = "fake",
                VectorDimensions = 1536
            });

            var docEntity = new KnowledgebaseDocumentEntity
            {
                Id = docId,
                KnowledgebaseId = kbId,
                FileName = "scanned_invoice.pdf",
                FilePath = "scanned_invoice.pdf",
                FileType = ".pdf",
                FileSize = 125000,
                Status = "Indexed",
                ParserEngineUsed = "PdfPig",
                ChunkCount = 1,
                RawExtractedText = docText
            };
            context.KnowledgebaseDocuments.Add(docEntity);
            await context.SaveChangesAsync();

            var vm = new ViewExtractedTextViewModel(service);

            // Act
            await vm.LoadDocumentAsync(docId, docEntity);

            // Assert
            vm.OcrItems.Should().HaveCount(1);
            vm.OcrItemCount.Should().Be(1);
            vm.HasOcrItems.Should().BeTrue();
            vm.OcrItems[0].OcrText.Should().Be("Total Due: $1,250.00 Net 30");
        }

        [Fact]
        public void BenchmarkTextDiffViewModel_ShowsOcrFigureCountsInHeaderStats()
        {
            // Arrange
            var run1 = new BenchmarkEngineRunResult(
                "aspose-pdf", "Aspose.Pdf", true, TimeSpan.FromMilliseconds(80), 2048, 2000, 350, 95.0, 1,
                Array.Empty<MetricScoreResult>(),
                "Digital contract text.\n[Page 1 Image 1 Text: Signature confirmed]\n[Page 2 Image 1 Text: Stamp verified]");

            var run2 = new BenchmarkEngineRunResult(
                "pdfpig", "PdfPig", true, TimeSpan.FromMilliseconds(45), 1024, 1800, 320, 85.0, 2,
                Array.Empty<MetricScoreResult>(),
                "Digital contract text only.");

            var docResult = new BenchmarkDocumentResult(
                "C:\\path\\contract.pdf", "contract.pdf", ".pdf", DocumentCategory.Pdf, 50000,
                new[] { run1, run2 }, run1);

            var vm = new BenchmarkTextDiffViewModel();

            // Act
            vm.Initialize(docResult);

            // Assert
            vm.HeaderStatsA.Should().Contain("2 OCR figure(s)");
            vm.HeaderStatsB.Should().NotContain("OCR figure(s)");
        }
    }
}

