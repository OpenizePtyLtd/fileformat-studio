using System;
using System.IO;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Parsing;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class AsposeDocumentParserTests
    {
        private readonly AsposeDocumentParser _parser;

        public AsposeDocumentParserTests()
        {
            _parser = new AsposeDocumentParser(new AsposeLicenseService());
        }

        [Fact]
        public void EngineMetadata_Properties_AreAccurate()
        {
            _parser.EngineId.Should().Be("aspose");
            _parser.DisplayName.Should().Contain("Aspose");
            _parser.Priority.Should().Be(100);
            _parser.IsAvailable.Should().BeTrue();

            _parser.SupportedExtensions.Should().Contain(new[]
            {
                ".docx", ".doc", ".dot", ".dotx", ".rtf", ".odt",
                ".xlsx", ".xls", ".xlsm", ".xlsb", ".ods",
                ".pptx", ".ppt", ".pps", ".ppsx", ".odp",
                ".pdf"
            });
        }

        [Fact]
        public async Task ExtractTextAsync_ThrowsFileNotFound_WhenFileDoesNotExist()
        {
            var fakePath = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid()}.docx");

            await _parser.Invoking(p => p.ExtractTextAsync(fakePath))
                .Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task ExtractTextAsync_ThrowsNotSupported_ForUnsupportedExtension()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"unsupported_{Guid.NewGuid()}.xyz");
            await File.WriteAllTextAsync(tempFile, "hello");

            try
            {
                await _parser.Invoking(p => p.ExtractTextAsync(tempFile))
                    .Should().ThrowAsync<NotSupportedException>();
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_WordDocument_ExtractsContentSuccessfully()
        {
            var tempDocx = Path.Combine(Path.GetTempPath(), $"test_word_{Guid.NewGuid()}.docx");
            try
            {
                var doc = new Aspose.Words.Document();
                var builder = new Aspose.Words.DocumentBuilder(doc);
                builder.Writeln("Hello from Aspose Words testing suite!");
                builder.Writeln("Second paragraph with detailed document data.");
                doc.Save(tempDocx);

                var extracted = await _parser.ExtractTextAsync(tempDocx);

                extracted.Should().NotBeNullOrWhiteSpace();
                extracted.Should().Contain("Hello from Aspose Words testing suite!");
                extracted.Should().Contain("Second paragraph with detailed document data.");
            }
            finally
            {
                if (File.Exists(tempDocx))
                    File.Delete(tempDocx);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_ExcelWorkbook_ExtractsContentSuccessfully()
        {
            var tempXlsx = Path.Combine(Path.GetTempPath(), $"test_excel_{Guid.NewGuid()}.xlsx");
            try
            {
                var wb = new Aspose.Cells.Workbook();
                var sheet = wb.Worksheets[0];
                sheet.Name = "FinancialSummary";
                sheet.Cells["A1"].PutValue("Item");
                sheet.Cells["B1"].PutValue("Amount");
                sheet.Cells["A2"].PutValue("CloudHosting");
                sheet.Cells["B2"].PutValue("1500");
                wb.Save(tempXlsx);

                var extracted = await _parser.ExtractTextAsync(tempXlsx);

                extracted.Should().NotBeNullOrWhiteSpace();
                extracted.Should().Contain("FinancialSummary");
                extracted.Should().Contain("CloudHosting");
                extracted.Should().Contain("1500");
            }
            finally
            {
                if (File.Exists(tempXlsx))
                    File.Delete(tempXlsx);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_PowerPointPresentation_ExtractsContentSuccessfully()
        {
            var tempPptx = Path.Combine(Path.GetTempPath(), $"test_slides_{Guid.NewGuid()}.pptx");
            try
            {
                using (var pres = new Aspose.Slides.Presentation())
                {
                    var slide = pres.Slides[0];
                    var shape = slide.Shapes.AddAutoShape(Aspose.Slides.ShapeType.Rectangle, 50, 50, 400, 100);
                    shape.TextFrame.Text = "Aspose Slides Architecture Overview";
                    pres.Save(tempPptx, Aspose.Slides.Export.SaveFormat.Pptx);
                }

                var extracted = await _parser.ExtractTextAsync(tempPptx);

                extracted.Should().NotBeNullOrWhiteSpace();
                extracted.Should().Contain("Aspos");
            }
            finally
            {
                if (File.Exists(tempPptx))
                    File.Delete(tempPptx);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_PdfDocument_ExtractsContentSuccessfully()
        {
            var tempPdf = Path.Combine(Path.GetTempPath(), $"test_pdf_{Guid.NewGuid()}.pdf");
            try
            {
                using (var doc = new Aspose.Pdf.Document())
                {
                    var page = doc.Pages.Add();
                    page.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Enterprise PDF document parsed by Aspose.PDF."));
                    doc.Save(tempPdf);
                }

                var extracted = await _parser.ExtractTextAsync(tempPdf);

                extracted.Should().NotBeNullOrWhiteSpace();
                extracted.Should().Contain("Enterprise PDF document parsed by Aspose.PDF.");
            }
            finally
            {
                if (File.Exists(tempPdf))
                    File.Delete(tempPdf);
            }
        }

        [Fact]
        public void DocumentParserFactory_ResolvesAsposeParser_ForOfficeAndPdfFormats()
        {
            var asposeParser = _parser;
            var plainTextParser = new PlainTextParser();
            var factory = new DocumentParserFactory(new IDocumentParser[] { plainTextParser, asposeParser });

            var docxParser = factory.ResolveParser("document.docx");
            docxParser.Should().NotBeNull();
            docxParser!.EngineId.Should().Be("aspose");

            var xlsxParser = factory.ResolveParser("data.xlsx");
            xlsxParser.Should().NotBeNull();
            xlsxParser!.EngineId.Should().Be("aspose");

            var pptxParser = factory.ResolveParser("slides.pptx");
            pptxParser.Should().NotBeNull();
            pptxParser!.EngineId.Should().Be("aspose");

            var pdfParser = factory.ResolveParser("whitepaper.pdf");
            pdfParser.Should().NotBeNull();
            pdfParser!.EngineId.Should().Be("aspose");

            var txtParser = factory.ResolveParser("notes.txt");
            txtParser.Should().NotBeNull();
            txtParser!.EngineId.Should().Be("plaintext");
        }
    }
}

