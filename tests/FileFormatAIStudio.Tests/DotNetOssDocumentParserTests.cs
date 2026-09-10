using System;
using System.IO;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Parsing;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class DotNetOssDocumentParserTests
    {
        private readonly DotNetOssDocumentParser _parser;

        public DotNetOssDocumentParserTests()
        {
            _parser = new DotNetOssDocumentParser();
        }

        [Fact]
        public void EngineMetadata_Properties_AreAccurate()
        {
            _parser.EngineId.Should().Be("dotnet-oss");
            _parser.DisplayName.Should().Contain("Open-Source");
            _parser.Priority.Should().Be(50);
            _parser.IsAvailable.Should().BeTrue();

            _parser.SupportedExtensions.Should().Contain(new[]
            {
                ".docx",
                ".xlsx", ".xls",
                ".pdf",
                ".csv", ".tsv",
                ".txt", ".md", ".markdown", ".json"
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
        public async Task ExtractTextAsync_ThrowsArgumentException_WhenPathNullOrWhitespace()
        {
            await _parser.Invoking(p => p.ExtractTextAsync(""))
                .Should().ThrowAsync<ArgumentException>();

            await _parser.Invoking(p => p.ExtractTextAsync("   "))
                .Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task ExtractTextAsync_ThrowsNotSupported_ForUnsupportedExtension()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"unsupported_{Guid.NewGuid()}.bin");
            await File.WriteAllTextAsync(tempFile, "binary or unknown content");

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
        public async Task ExtractTextAsync_WordDocument_ExtractsParagraphsAndTables()
        {
            var tempDocx = Path.Combine(Path.GetTempPath(), $"test_oss_word_{Guid.NewGuid()}.docx");
            try
            {
                var doc = new Aspose.Words.Document();
                var builder = new Aspose.Words.DocumentBuilder(doc);
                builder.Writeln("OpenXML extraction test paragraph.");
                builder.Writeln("Second line with additional details.");

                // Insert a simple table
                builder.StartTable();
                builder.InsertCell();
                builder.Write("Header1");
                builder.InsertCell();
                builder.Write("Header2");
                builder.EndRow();
                builder.InsertCell();
                builder.Write("Value1");
                builder.InsertCell();
                builder.Write("Value2");
                builder.EndRow();
                builder.EndTable();

                doc.Save(tempDocx);

                var extracted = await _parser.ExtractTextAsync(tempDocx);

                extracted.Should().NotBeNullOrWhiteSpace();
                extracted.Should().Contain("OpenXML extraction test paragraph.");
                extracted.Should().Contain("Second line with additional details.");
                extracted.Should().Contain("Header1 | Header2");
                extracted.Should().Contain("Value1 | Value2");
            }
            finally
            {
                if (File.Exists(tempDocx))
                    File.Delete(tempDocx);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_Pdf_ExtractsPagesAndDemarcations()
        {
            var tempPdf = Path.Combine(Path.GetTempPath(), $"test_oss_pdf_{Guid.NewGuid()}.pdf");
            try
            {
                var pdf = new Aspose.Pdf.Document();
                var page1 = pdf.Pages.Add();
                page1.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Page 1 Content for PdfPig OSS"));

                var page2 = pdf.Pages.Add();
                page2.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Page 2 Content for PdfPig OSS"));

                pdf.Save(tempPdf);

                var extracted = await _parser.ExtractTextAsync(tempPdf);

                extracted.Should().NotBeNullOrWhiteSpace();
                extracted.Should().Contain("--- Page 1 ---");
                extracted.Should().Contain("Page 1 Content for PdfPig OSS");
                extracted.Should().Contain("--- Page 2 ---");
                extracted.Should().Contain("Page 2 Content for PdfPig OSS");
            }
            finally
            {
                if (File.Exists(tempPdf))
                    File.Delete(tempPdf);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_Excel_ExtractsSheetsAndTabularRows()
        {
            var tempXlsx = Path.Combine(Path.GetTempPath(), $"test_oss_excel_{Guid.NewGuid()}.xlsx");
            try
            {
                var wb = new Aspose.Cells.Workbook();
                var sheet1 = wb.Worksheets[0];
                sheet1.Name = "Q1_Metrics";
                sheet1.Cells["A1"].PutValue("Metric");
                sheet1.Cells["B1"].PutValue("Value");
                sheet1.Cells["A2"].PutValue("ActiveUsers");
                sheet1.Cells["B2"].PutValue("9420");

                var sheet2 = wb.Worksheets.Add("Q2_Metrics");
                sheet2.Cells["A1"].PutValue("Revenue");
                sheet2.Cells["B1"].PutValue("54000");

                wb.Save(tempXlsx);

                var extracted = await _parser.ExtractTextAsync(tempXlsx);

                extracted.Should().NotBeNullOrWhiteSpace();
                extracted.Should().Contain("--- Sheet: Q1_Metrics ---");
                extracted.Should().Contain("Metric | Value");
                extracted.Should().Contain("ActiveUsers | 9420");
                extracted.Should().Contain("--- Sheet: Q2_Metrics ---");
                extracted.Should().Contain("Revenue | 54000");
            }
            finally
            {
                if (File.Exists(tempXlsx))
                    File.Delete(tempXlsx);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_CsvAndTsv_ExtractsDelimitedRows()
        {
            var tempCsv = Path.Combine(Path.GetTempPath(), $"test_oss_{Guid.NewGuid()}.csv");
            var tempTsv = Path.Combine(Path.GetTempPath(), $"test_oss_{Guid.NewGuid()}.tsv");

            try
            {
                await File.WriteAllTextAsync(tempCsv, "Name,Role,Department\nAlice,Architect,AI\nBob,Engineer,Core");
                await File.WriteAllTextAsync(tempTsv, "ColA\tColB\n123\t456");

                var csvResult = await _parser.ExtractTextAsync(tempCsv);
                csvResult.Should().Contain("Name | Role | Department");
                csvResult.Should().Contain("Alice | Architect | AI");

                var tsvResult = await _parser.ExtractTextAsync(tempTsv);
                tsvResult.Should().Contain("ColA | ColB");
                tsvResult.Should().Contain("123 | 456");
            }
            finally
            {
                if (File.Exists(tempCsv))
                    File.Delete(tempCsv);
                if (File.Exists(tempTsv))
                    File.Delete(tempTsv);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_PlainTextAndMarkdown_ExtractsTextDirectly()
        {
            var tempMd = Path.Combine(Path.GetTempPath(), $"test_oss_{Guid.NewGuid()}.md");
            var tempJson = Path.Combine(Path.GetTempPath(), $"test_oss_{Guid.NewGuid()}.json");

            try
            {
                await File.WriteAllTextAsync(tempMd, "# Heading 1\n\nSome markdown text.");
                await File.WriteAllTextAsync(tempJson, "{\"name\": \"FileFormatAIStudio\"}");

                var mdResult = await _parser.ExtractTextAsync(tempMd);
                mdResult.Should().Contain("# Heading 1");
                mdResult.Should().Contain("Some markdown text.");

                var jsonResult = await _parser.ExtractTextAsync(tempJson);
                jsonResult.Should().Contain("\"name\": \"FileFormatAIStudio\"");
            }
            finally
            {
                if (File.Exists(tempMd))
                    File.Delete(tempMd);
                if (File.Exists(tempJson))
                    File.Delete(tempJson);
            }
        }

        [Fact]
        public async Task DocumentParserFactory_ResolvesDotNetOss_WhenRequestedExplicitly()
        {
            var asposeParser = new AsposeDocumentParser();
            var ossParser = new DotNetOssDocumentParser();
            var plainParser = new PlainTextParser();

            var factory = new DocumentParserFactory(new IDocumentParser[] { asposeParser, ossParser, plainParser });

            var resolved = factory.GetParser("dotnet-oss");
            resolved.Should().NotBeNull();
            resolved.Should().BeSameAs(ossParser);

            var resolvedForDocx = factory.ResolveParser("document.docx", "dotnet-oss");
            resolvedForDocx.Should().NotBeNull();
            resolvedForDocx.Should().BeSameAs(ossParser);

            // Default auto-resolve picks Aspose due to higher priority (100 > 50)
            var autoResolvedDocx = factory.ResolveParser("document.docx");
            autoResolvedDocx.Should().NotBeNull();
            autoResolvedDocx.Should().BeSameAs(asposeParser);

            // Without Aspose, auto-resolve picks DotNetOss over PlainText
            var factoryWithoutAspose = new DocumentParserFactory(new IDocumentParser[] { ossParser, plainParser });
            var autoResolvedWithoutAspose = factoryWithoutAspose.ResolveParser("document.docx");
            autoResolvedWithoutAspose.Should().NotBeNull();
            autoResolvedWithoutAspose.Should().BeSameAs(ossParser);
        }
    }
}

