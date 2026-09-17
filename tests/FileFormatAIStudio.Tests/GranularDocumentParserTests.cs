using System;
using System.IO;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing.Engines.Excel;
using FileFormatAIStudio.Services.Parsing.Engines.Pdf;
using FileFormatAIStudio.Services.Parsing.Engines.PowerPoint;
using FileFormatAIStudio.Services.Parsing.Engines.Word;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class GranularDocumentParserTests
    {
        [Fact]
        public void WordParsers_MetadataAndCategories_AreAccurate()
        {
            var asposeWords = new AsposeWordsParser();
            asposeWords.Category.Should().Be(DocumentCategory.Word);
            asposeWords.EngineId.Should().Be("aspose-words");
            asposeWords.DisplayName.Should().Be("Aspose.Words (.NET)");
            asposeWords.Priority.Should().Be(100);
            asposeWords.IsAvailable.Should().BeTrue();
            asposeWords.SupportedExtensions.Should().Contain(new[] { ".docx", ".doc", ".rtf", ".odt" });

            var openXml = new OpenXmlWordParser();
            openXml.Category.Should().Be(DocumentCategory.Word);
            openXml.EngineId.Should().Be("openxml-words");
            openXml.DisplayName.Should().Be("DocumentFormat.OpenXml (.NET)");
            openXml.Priority.Should().Be(50);
            openXml.IsAvailable.Should().BeTrue();
            openXml.SupportedExtensions.Should().Contain(".docx");
        }

        [Fact]
        public void PdfParsers_MetadataAndCategories_AreAccurate()
        {
            var asposePdf = new AsposePdfParser();
            asposePdf.Category.Should().Be(DocumentCategory.Pdf);
            asposePdf.EngineId.Should().Be("aspose-pdf");
            asposePdf.DisplayName.Should().Be("Aspose.PDF (.NET)");
            asposePdf.Priority.Should().Be(100);
            asposePdf.IsAvailable.Should().BeTrue();
            asposePdf.SupportedExtensions.Should().Contain(".pdf");

            var pdfPig = new PdfPigParser();
            pdfPig.Category.Should().Be(DocumentCategory.Pdf);
            pdfPig.EngineId.Should().Be("pdfpig");
            pdfPig.DisplayName.Should().Be("PdfPig (.NET)");
            pdfPig.Priority.Should().Be(50);
            pdfPig.IsAvailable.Should().BeTrue();
            pdfPig.SupportedExtensions.Should().Contain(".pdf");
        }

        [Fact]
        public void ExcelParsers_MetadataAndCategories_AreAccurate()
        {
            var asposeCells = new AsposeCellsParser();
            asposeCells.Category.Should().Be(DocumentCategory.Excel);
            asposeCells.EngineId.Should().Be("aspose-cells");
            asposeCells.DisplayName.Should().Be("Aspose.Cells (.NET)");
            asposeCells.Priority.Should().Be(100);
            asposeCells.IsAvailable.Should().BeTrue();
            asposeCells.SupportedExtensions.Should().Contain(new[] { ".xlsx", ".xls", ".csv" });

            var excelReader = new ExcelDataReaderParser();
            excelReader.Category.Should().Be(DocumentCategory.Excel);
            excelReader.EngineId.Should().Be("exceldatareader");
            excelReader.DisplayName.Should().Be("ExcelDataReader (.NET)");
            excelReader.Priority.Should().Be(50);
            excelReader.IsAvailable.Should().BeTrue();
            excelReader.SupportedExtensions.Should().Contain(new[] { ".xlsx", ".xls" });

            var csvHelper = new CsvHelperParser();
            csvHelper.Category.Should().Be(DocumentCategory.Excel);
            csvHelper.EngineId.Should().Be("csvhelper");
            csvHelper.DisplayName.Should().Be("CsvHelper (.NET)");
            csvHelper.Priority.Should().Be(40);
            csvHelper.IsAvailable.Should().BeTrue();
            csvHelper.SupportedExtensions.Should().Contain(new[] { ".csv", ".tsv" });
        }

        [Fact]
        public void PowerPointParser_MetadataAndCategories_AreAccurate()
        {
            var asposeSlides = new AsposeSlidesParser();
            asposeSlides.Category.Should().Be(DocumentCategory.PowerPoint);
            asposeSlides.EngineId.Should().Be("aspose-slides");
            asposeSlides.DisplayName.Should().Be("Aspose.Slides (.NET)");
            asposeSlides.Priority.Should().Be(100);
            asposeSlides.IsAvailable.Should().BeTrue();
            asposeSlides.SupportedExtensions.Should().Contain(new[] { ".pptx", ".ppt", ".odp" });
        }

        [Fact]
        public async Task CsvHelperParser_ExtractsDelimitedContentSuccessfully()
        {
            var parser = new CsvHelperParser();
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.csv");
            await File.WriteAllTextAsync(tempFile, "Name,Age,Role\nAlice,30,Engineer\nBob,25,Designer\n");

            try
            {
                var text = await parser.ExtractTextAsync(tempFile);
                text.Should().Contain("Alice");
                text.Should().Contain("Engineer");
                text.Should().Contain("Bob");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task GranularParsers_ThrowFileNotFound_WhenFileMissing()
        {
            var asposeWords = new AsposeWordsParser();
            var openXml = new OpenXmlWordParser();
            var asposePdf = new AsposePdfParser();
            var pdfPig = new PdfPigParser();
            var asposeCells = new AsposeCellsParser();
            var excelReader = new ExcelDataReaderParser();
            var csvHelper = new CsvHelperParser();
            var asposeSlides = new AsposeSlidesParser();

            var fakePath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.docx");

            await asposeWords.Invoking(p => p.ExtractTextAsync(fakePath)).Should().ThrowAsync<FileNotFoundException>();
            await openXml.Invoking(p => p.ExtractTextAsync(fakePath)).Should().ThrowAsync<FileNotFoundException>();
            await asposePdf.Invoking(p => p.ExtractTextAsync(fakePath)).Should().ThrowAsync<FileNotFoundException>();
            await pdfPig.Invoking(p => p.ExtractTextAsync(fakePath)).Should().ThrowAsync<FileNotFoundException>();
            await asposeCells.Invoking(p => p.ExtractTextAsync(fakePath)).Should().ThrowAsync<FileNotFoundException>();
            await excelReader.Invoking(p => p.ExtractTextAsync(fakePath)).Should().ThrowAsync<FileNotFoundException>();
            await csvHelper.Invoking(p => p.ExtractTextAsync(fakePath)).Should().ThrowAsync<FileNotFoundException>();
            await asposeSlides.Invoking(p => p.ExtractTextAsync(fakePath)).Should().ThrowAsync<FileNotFoundException>();
        }
    }
}

