using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Ocr;
using FileFormatAIStudio.Services.Parsing;
using FileFormatAIStudio.Services.Parsing.Engines.Image;
using FileFormatAIStudio.Services.Parsing.Engines.Word;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class OcrPipelineIntegrationTests : IDisposable
    {
        private readonly string _testTempDir;
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public OcrPipelineIntegrationTests()
        {
            _testTempDir = Path.Combine(Path.GetTempPath(), "FFStudio_OcrPipelineTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testTempDir);

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

            if (Directory.Exists(_testTempDir))
            {
                try
                {
                    Directory.Delete(_testTempDir, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        [Fact]
        public async Task OpenXmlWordParser_WithEmbeddedImage_ExtractsBothNativeTextAndOcr()
        {
            // Arrange
            string docxPath = Path.Combine(_testTempDir, "document_with_figure.docx");
            byte[] figureBytes = CreateBmp(200, 200);

            // Create a real docx with text and an embedded image part
            using (var wordDoc = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body(
                    new Paragraph(new Run(new Text("Chapter 1: System Architecture Overview.")))
                ));

                var imagePart = mainPart.AddImagePart(ImagePartType.Bmp);
                using (var stream = new MemoryStream(figureBytes))
                {
                    imagePart.FeedData(stream);
                }

                mainPart.Document.Save();
            }

            var fakeOcr = new MockOcrEngine("Figure 1.1: Microservices Gateway Topology");
            var extractor = new ImageExtractorService();
            var parser = new OpenXmlWordParser(fakeOcr, extractor);

            // Act
            string extracted = await parser.ExtractTextAsync(docxPath);

            // Assert
            extracted.Should().Contain("Chapter 1: System Architecture Overview.");
            extracted.Should().Contain("[Embedded Image 1 Text: Figure 1.1: Microservices Gateway Topology]");
        }

        [Fact]
        public async Task ImageDocumentParser_ExtractsOcrTextFromStandaloneImage()
        {
            // Arrange
            string imgPath = Path.Combine(_testTempDir, "receipt_scan.bmp");
            byte[] imgBytes = CreateBmp(300, 300);
            await File.WriteAllBytesAsync(imgPath, imgBytes);

            var fakeOcr = new MockOcrEngine("Acme Corporation\nReceipt #48102\nTotal: $120.00");
            var extractor = new ImageExtractorService();
            var parser = new ImageDocumentParser(fakeOcr, extractor);

            // Act
            string extracted = await parser.ExtractTextAsync(imgPath);

            // Assert
            extracted.Should().Contain("Acme Corporation");
            extracted.Should().Contain("Receipt #48102");
            extracted.Should().Contain("Total: $120.00");
        }

        [Fact]
        public async Task OcrPipeline_ParallelStressTest_HandlesConcurrentRequestsSafely()
        {
            // Arrange
            var fakeOcr = new MockOcrEngine("Thread safe OCR output");
            var extractor = new ImageExtractorService();
            var parser = new ImageDocumentParser(fakeOcr, extractor);

            string sampleImg = Path.Combine(_testTempDir, "parallel_sample.bmp");
            await File.WriteAllBytesAsync(sampleImg, CreateBmp(150, 150));

            const int concurrency = 20;
            var tasks = new Task<string>[concurrency];

            // Act
            for (int i = 0; i < concurrency; i++)
            {
                tasks[i] = Task.Run(() => parser.ExtractTextAsync(sampleImg));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(concurrency);
            foreach (var res in results)
            {
                res.Should().Be("Thread safe OCR output");
            }
        }

        [Fact]
        public async Task KnowledgebaseIngestion_WithImageParser_IndexesOcrChunksInDatabase()
        {
            // Arrange
            using var context = new AppDbContext(_options);
            var fakeAi = new FakeAiClientFactory();
            var fakeOcr = new MockOcrEngine("Knowledgebase Ingestion OCR Test Content");
            var extractor = new ImageExtractorService();
            var imageParser = new ImageDocumentParser(fakeOcr, extractor);

            var parserFactory = new DocumentParserFactory(new IDocumentParser[] { imageParser });
            var chunker = new TextChunker();
            var vectorStore = new VectorStoreService(context);

            var kbService = new KnowledgebaseService(
                context,
                parserFactory,
                chunker,
                vectorStore,
                fakeAi,
                _testTempDir);

            context.Providers.Add(new ProviderConfigEntity
            {
                Id = Guid.NewGuid(),
                Name = "MockProvider",
                ProviderType = "Mock",
                IsEnabled = true
            });

            var kb = new KnowledgebaseEntity
            {
                Id = Guid.NewGuid(),
                Name = "Scanned Invoices KB",
                ParserEngine = "windows-ocr-image",
                EmbeddingProvider = "MockProvider",
                EmbeddingModel = "mock-embed",
                VectorDimensions = 1536
            };
            context.Knowledgebases.Add(kb);
            await context.SaveChangesAsync();

            string testImage = Path.Combine(_testTempDir, "invoice_ocr_doc.bmp");
            await File.WriteAllBytesAsync(testImage, CreateBmp(250, 250));

            // Act
            var ingested = await kbService.IngestDocumentsAsync(kb.Id, new[] { testImage });

            // Assert
            ingested.Should().HaveCount(1);
            var docId = ingested[0].Id;

            // Verify raw text stored in SQLite
            var rawText = await kbService.GetDocumentExtractedTextAsync(docId);
            rawText.Should().Contain("Knowledgebase Ingestion OCR Test Content");

            // Verify chunks created and stored
            var chunks = await kbService.GetDocumentChunksAsync(docId);
            chunks.Should().NotBeEmpty();
            chunks[0].TextContent.Should().Contain("Knowledgebase Ingestion OCR Test Content");
            chunks[0].TokenCount.Should().BeGreaterThan(0);
        }

        private static byte[] CreateBmp(int width, int height)
        {
            int rowSize = (width * 3 + 3) & ~3;
            int pixelArraySize = rowSize * height;
            int fileSize = 54 + pixelArraySize;

            byte[] bmp = new byte[fileSize];

            bmp[0] = 0x42;
            bmp[1] = 0x4D;
            BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
            BitConverter.GetBytes(54).CopyTo(bmp, 10);

            BitConverter.GetBytes(40).CopyTo(bmp, 14);
            BitConverter.GetBytes(width).CopyTo(bmp, 18);
            BitConverter.GetBytes(height).CopyTo(bmp, 22);
            BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
            BitConverter.GetBytes((short)24).CopyTo(bmp, 28);
            BitConverter.GetBytes(pixelArraySize).CopyTo(bmp, 34);

            return bmp;
        }

        private class MockOcrEngine : IOcrEngine
        {
            private readonly string _text;

            public MockOcrEngine(string text)
            {
                _text = text;
            }

            public string EngineId => "mock-ocr";
            public string DisplayName => "Mock OCR";
            public bool IsAvailable => true;
            public IReadOnlyList<string> SupportedLanguages => new[] { "en-US" };

            public Task<OcrResult> RecognizeTextAsync(Stream imageStream, string? languageTag = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new OcrResult(_text, Array.Empty<OcrLine>(), null, languageTag ?? "en-US"));
            }

            public Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string? languageTag = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new OcrResult(_text, Array.Empty<OcrLine>(), null, languageTag ?? "en-US"));
            }
        }
    }
}

