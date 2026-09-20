using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing;
using FileFormatAIStudio.Services.Parsing.Engines.Excel;
using FileFormatAIStudio.Services.Parsing.Engines.Pdf;
using FileFormatAIStudio.Services.Parsing.Engines.PowerPoint;
using FileFormatAIStudio.Services.Parsing.Engines.Word;
using FileFormatAIStudio.Services.Parsing.Node;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class OfficeParserEngineTests
    {
        private readonly NodeJsRuntimeService _runtimeService;
        private readonly NodeJsHostService _hostService;

        public OfficeParserEngineTests()
        {
            _runtimeService = new NodeJsRuntimeService();
            _hostService = new NodeJsHostService(_runtimeService);
        }

        [Fact]
        public void OfficeParserSlidesParser_MetadataAndConfiguration_AreAccurate()
        {
            var parser = new OfficeParserSlidesParser(_hostService);

            parser.Category.Should().Be(DocumentCategory.PowerPoint);
            parser.EngineId.Should().Be("officeparser-slides");
            parser.DisplayName.Should().Be("officeparser (Node.js)");
            parser.Priority.Should().Be(40);
            parser.IsAvailable.Should().Be(_hostService.IsAvailable);
            parser.SupportedExtensions.Should().Contain(new[] { ".pptx", ".odp" });
        }

        [Fact]
        public void OfficeParserWordParser_MetadataAndConfiguration_AreAccurate()
        {
            var parser = new OfficeParserWordParser(_hostService);

            parser.Category.Should().Be(DocumentCategory.Word);
            parser.EngineId.Should().Be("officeparser-words");
            parser.DisplayName.Should().Be("officeparser (Node.js)");
            parser.Priority.Should().Be(30);
            parser.IsAvailable.Should().Be(_hostService.IsAvailable);
            parser.SupportedExtensions.Should().Contain(new[] { ".docx", ".odt" });
        }

        [Fact]
        public void OfficeParserExcelParser_MetadataAndConfiguration_AreAccurate()
        {
            var parser = new OfficeParserExcelParser(_hostService);

            parser.Category.Should().Be(DocumentCategory.Excel);
            parser.EngineId.Should().Be("officeparser-cells");
            parser.DisplayName.Should().Be("officeparser (Node.js)");
            parser.Priority.Should().Be(30);
            parser.IsAvailable.Should().Be(_hostService.IsAvailable);
            parser.SupportedExtensions.Should().Contain(new[] { ".xlsx", ".ods" });
        }

        [Fact]
        public void OfficeParserPdfParser_MetadataAndConfiguration_AreAccurate()
        {
            var parser = new OfficeParserPdfParser(_hostService);

            parser.Category.Should().Be(DocumentCategory.Pdf);
            parser.EngineId.Should().Be("officeparser-pdf");
            parser.DisplayName.Should().Be("officeparser (Node.js)");
            parser.Priority.Should().Be(30);
            parser.IsAvailable.Should().Be(_hostService.IsAvailable);
            parser.SupportedExtensions.Should().Contain(".pdf");
        }

        [Fact]
        public void UniversalNodeJsDocumentParser_MetadataAndConfiguration_AreAccurate()
        {
            var parser = new NodeJsDocumentParser(_hostService);

            parser.EngineId.Should().Be("officeparser");
            parser.DisplayName.Should().Be("officeparser Universal (Node.js)");
            parser.Priority.Should().Be(35);
            parser.IsAvailable.Should().Be(_hostService.IsAvailable);
            parser.SupportedExtensions.Should().Contain(new[] { ".docx", ".pptx", ".xlsx", ".odt", ".odp", ".ods", ".pdf" });
        }

        [Fact]
        public async Task OfficeParserSlidesParser_ExtractsPowerPointContentSuccessfully()
        {
            if (!_hostService.IsAvailable) return;

            var tempFile = Path.Combine(Path.GetTempPath(), $"test_slides_{Guid.NewGuid():N}.pptx");
            try
            {
                using (var pres = new Aspose.Slides.Presentation())
                {
                    var slide = pres.Slides[0];
                    var shape = slide.Shapes.AddAutoShape(Aspose.Slides.ShapeType.Rectangle, 50, 50, 400, 100);
                    shape.TextFrame.Text = "Welcome to OfficeParser PowerPoint Extraction Test";
                    pres.Save(tempFile, Aspose.Slides.Export.SaveFormat.Pptx);
                }

                var parser = new OfficeParserSlidesParser(_hostService);
                var text = await parser.ExtractTextAsync(tempFile);

                text.Should().NotBeNullOrWhiteSpace();
                text.Should().Contain("Welcome to OfficeParser PowerPoint Extraction Test");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task OfficeParserWordParser_ExtractsWordContentSuccessfully()
        {
            if (!_hostService.IsAvailable) return;

            var tempFile = Path.Combine(Path.GetTempPath(), $"test_word_{Guid.NewGuid():N}.docx");
            try
            {
                var doc = new Aspose.Words.Document();
                var builder = new Aspose.Words.DocumentBuilder(doc);
                builder.Writeln("OfficeParser Word extraction verification paragraph.");
                doc.Save(tempFile);

                var parser = new OfficeParserWordParser(_hostService);
                var text = await parser.ExtractTextAsync(tempFile);

                text.Should().NotBeNullOrWhiteSpace();
                text.Should().Contain("OfficeParser Word extraction verification paragraph.");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task OfficeParserExcelParser_ExtractsExcelContentSuccessfully()
        {
            if (!_hostService.IsAvailable) return;

            var tempFile = Path.Combine(Path.GetTempPath(), $"test_excel_{Guid.NewGuid():N}.xlsx");
            try
            {
                var workbook = new Aspose.Cells.Workbook();
                var sheet = workbook.Worksheets[0];
                sheet.Cells["A1"].PutValue("Product");
                sheet.Cells["B1"].PutValue("Revenue");
                sheet.Cells["A2"].PutValue("OfficeParser");
                sheet.Cells["B2"].PutValue("9999");
                workbook.Save(tempFile);

                var parser = new OfficeParserExcelParser(_hostService);
                var text = await parser.ExtractTextAsync(tempFile);

                text.Should().NotBeNullOrWhiteSpace();
                text.Should().Contain("Product");
                text.Should().Contain("Revenue");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task OfficeParserPdfParser_ExtractsPdfContentSuccessfully()
        {
            if (!_hostService.IsAvailable) return;

            var tempFile = Path.Combine(Path.GetTempPath(), $"test_pdf_{Guid.NewGuid():N}.pdf");
            try
            {
                var pdf = new Aspose.Pdf.Document();
                var page = pdf.Pages.Add();
                page.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("OfficeParser PDF Extraction Test Content"));
                pdf.Save(tempFile);

                var parser = new OfficeParserPdfParser(_hostService);
                var text = await parser.ExtractTextAsync(tempFile);

                text.Should().NotBeNullOrWhiteSpace();
                text.Should().Contain("OfficeParser PDF Extraction Test Content");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task OfficeParser_ThrowsFileNotFound_WhenFileDoesNotExist()
        {
            var parser = new OfficeParserWordParser(_hostService);
            var nonExistent = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.docx");

            var act = () => parser.ExtractTextAsync(nonExistent);
            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task OfficeParser_ThrowsNotSupported_WhenExtensionNotSupported()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"invalid_{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(tempFile, "sample text");

            try
            {
                var parser = new OfficeParserWordParser(_hostService);
                var act = () => parser.ExtractTextAsync(tempFile);
                await act.Should().ThrowAsync<NotSupportedException>();
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task OfficeParser_ThrowsArgumentException_WhenPathIsNullOrEmpty()
        {
            var parser = new OfficeParserSlidesParser(_hostService);

            var actNull = () => parser.ExtractTextAsync(null!);
            var actEmpty = () => parser.ExtractTextAsync("   ");

            await actNull.Should().ThrowAsync<ArgumentException>();
            await actEmpty.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task OfficeParser_ThrowsInvalidOperation_WhenNodeUnavailable()
        {
            var unavailableHost = new DummyUnavailableNodeHostService();
            var parser = new OfficeParserWordParser(unavailableHost);

            var tempFile = Path.Combine(Path.GetTempPath(), $"dummy_{Guid.NewGuid():N}.docx");
            await File.WriteAllTextAsync(tempFile, "dummy");

            try
            {
                var act = () => parser.ExtractTextAsync(tempFile);
                var ex = await act.Should().ThrowAsync<InvalidOperationException>();
                ex.WithMessage("*unavailable*");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void DocumentParserFactory_ResolvesOfficeParserEnginesByAlias()
        {
            var slides = new OfficeParserSlidesParser(_hostService);
            var words = new OfficeParserWordParser(_hostService);
            var cells = new OfficeParserExcelParser(_hostService);
            var pdf = new OfficeParserPdfParser(_hostService);

            var factory = new DocumentParserFactory(new IDocumentParser[] { slides, words, cells, pdf });

            var generalParser = factory.GetParser("officeparser");
            generalParser.Should().NotBeNull();
            generalParser!.DisplayName.Should().Be("officeparser (Node.js)");

            var pptxMatch = factory.ResolveParser("presentation.pptx", "officeparser");
            pptxMatch.Should().NotBeNull();
            pptxMatch!.EngineId.Should().Be("officeparser-slides");

            var docxMatch = factory.ResolveParser("document.docx", "officeparser");
            docxMatch.Should().NotBeNull();
            docxMatch!.EngineId.Should().Be("officeparser-words");

            var xlsxMatch = factory.ResolveParser("sheet.xlsx", "node-parser");
            xlsxMatch.Should().NotBeNull();
            xlsxMatch!.EngineId.Should().Be("officeparser-cells");

            var pdfMatch = factory.ResolveParser("file.pdf", "nodejs");
            pdfMatch.Should().NotBeNull();
            pdfMatch!.EngineId.Should().Be("officeparser-pdf");
        }

        [Fact]
        public void DocumentParserFactory_GetParsersByCategory_IncludesOfficeParser()
        {
            var asposeSlides = new AsposeSlidesParser();
            var officeSlides = new OfficeParserSlidesParser(_hostService);

            var factory = new DocumentParserFactory(new IDocumentParser[] { asposeSlides, officeSlides });

            var pptParsers = factory.GetParsersByCategory(DocumentCategory.PowerPoint);
            pptParsers.Should().HaveCount(2);
            pptParsers.Should().Contain(p => p.EngineId == "aspose-slides");
            pptParsers.Should().Contain(p => p.EngineId == "officeparser-slides");
        }

        [Fact]
        public async Task DocumentEnginePreferenceService_ResolvesOfficeParser_WhenSelectedAsPreference()
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<Data.AppDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var context = new Data.AppDbContext(options))
            {
                context.Database.EnsureCreated();
            }

            var categoryRegistry = new DocumentCategoryRegistry();
            var asposeSlides = new AsposeSlidesParser();
            var officeSlides = new OfficeParserSlidesParser(_hostService);
            var factory = new DocumentParserFactory(new IDocumentParser[] { asposeSlides, officeSlides });

            using var prefContext = new Data.AppDbContext(options);
            var prefService = new DocumentEnginePreferenceService(prefContext, categoryRegistry, factory);

            // Set PowerPoint preference to officeparser-slides
            await prefService.SetPreferredEngineIdAsync(DocumentCategory.PowerPoint, "officeparser-slides");

            var resolved = await prefService.ResolveParserForDocumentAsync("sample.pptx");
            resolved.Should().NotBeNull();
            resolved!.EngineId.Should().Be("officeparser-slides");
            resolved.DisplayName.Should().Be("officeparser (Node.js)");
        }

        private class DummyUnavailableNodeHostService : INodeJsHostService
        {
            public bool IsAvailable => false;
            public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(10);

            public Task<NodeJsResponse> ExecuteAsync(NodeJsRequest request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new NodeJsResponse { Success = false, Error = "NodeHost unavailable" });
            }

            public Task<NodeJsResponse> ExecuteAsync(string? engineId, string command, string filePath, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new NodeJsResponse { Success = false, Error = "NodeHost unavailable" });
            }
        }
    }
}

