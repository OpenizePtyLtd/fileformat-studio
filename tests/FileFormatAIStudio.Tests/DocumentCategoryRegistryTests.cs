using System;
using System.Collections.Generic;
using System.Linq;
using FileFormatAIStudio.Services.Benchmarking;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class DocumentCategoryRegistryTests
    {
        private readonly DocumentCategoryRegistry _registry = new();

        [Fact]
        public void GetAllCategories_ReturnsAllFiveDocumentCategories()
        {
            var categories = _registry.GetAllCategories();

            categories.Should().HaveCount(5)
                .And.Contain(new[]
                {
                    DocumentCategory.Word,
                    DocumentCategory.Excel,
                    DocumentCategory.PowerPoint,
                    DocumentCategory.Pdf,
                    DocumentCategory.PlainText
                });
        }

        [Theory]
        [InlineData(DocumentCategory.Word, "Word Document")]
        [InlineData(DocumentCategory.Excel, "Excel Spreadsheet")]
        [InlineData(DocumentCategory.PowerPoint, "PowerPoint Presentation")]
        [InlineData(DocumentCategory.Pdf, "PDF Document")]
        [InlineData(DocumentCategory.PlainText, "Plain / Structured Text")]
        public void GetCategoryDisplayName_ReturnsExpectedDisplayName(DocumentCategory category, string expectedName)
        {
            var displayName = _registry.GetCategoryDisplayName(category);
            displayName.Should().Be(expectedName);
        }

        [Theory]
        [InlineData("report.docx", DocumentCategory.Word)]
        [InlineData("legacy.doc", DocumentCategory.Word)]
        [InlineData("document.rtf", DocumentCategory.Word)]
        [InlineData("open.odt", DocumentCategory.Word)]
        [InlineData("sheet.xlsx", DocumentCategory.Excel)]
        [InlineData("legacy.xls", DocumentCategory.Excel)]
        [InlineData("data.csv", DocumentCategory.Excel)]
        [InlineData("data.tsv", DocumentCategory.Excel)]
        [InlineData("slides.pptx", DocumentCategory.PowerPoint)]
        [InlineData("legacy.ppt", DocumentCategory.PowerPoint)]
        [InlineData("slides.odp", DocumentCategory.PowerPoint)]
        [InlineData("document.pdf", DocumentCategory.Pdf)]
        [InlineData("notes.txt", DocumentCategory.PlainText)]
        [InlineData("README.md", DocumentCategory.PlainText)]
        [InlineData("config.json", DocumentCategory.PlainText)]
        public void ResolveCategory_WithValidFileName_ResolvesCorrectCategory(string fileName, DocumentCategory expectedCategory)
        {
            var category = _registry.ResolveCategory(fileName);
            category.Should().Be(expectedCategory);
        }

        [Theory]
        [InlineData(".DOCX", DocumentCategory.Word)]
        [InlineData("XLSX", DocumentCategory.Excel)]
        [InlineData(".Pdf", DocumentCategory.Pdf)]
        [InlineData("pptx", DocumentCategory.PowerPoint)]
        public void ResolveCategory_CaseAndDotNormalization_WorksProperly(string input, DocumentCategory expectedCategory)
        {
            var category = _registry.ResolveCategory(input);
            category.Should().Be(expectedCategory);
        }

        [Fact]
        public void GetFormatDescriptor_ReturnsAccurateMetadata()
        {
            var descriptor = _registry.GetFormatDescriptor("contract.docx");

            descriptor.Should().NotBeNull();
            descriptor!.Extension.Should().Be(".docx");
            descriptor.Category.Should().Be(DocumentCategory.Word);
            descriptor.TypicalSourceApp.Should().Be("Microsoft Word");
            descriptor.MimeType.Should().Contain("wordprocessingml");
        }

        [Fact]
        public void GetFormatsByCategory_ReturnsOnlyFormatsForThatCategory()
        {
            var wordFormats = _registry.GetFormatsByCategory(DocumentCategory.Word);
            wordFormats.Should().NotBeEmpty();
            wordFormats.Should().OnlyContain(f => f.Category == DocumentCategory.Word);
            wordFormats.Select(f => f.Extension).Should().Contain(new[] { ".docx", ".doc", ".rtf", ".odt" });

            var excelFormats = _registry.GetFormatsByCategory(DocumentCategory.Excel);
            excelFormats.Should().NotBeEmpty();
            excelFormats.Should().OnlyContain(f => f.Category == DocumentCategory.Excel);
            excelFormats.Select(f => f.Extension).Should().Contain(new[] { ".xlsx", ".xls", ".csv" });

            var pptFormats = _registry.GetFormatsByCategory(DocumentCategory.PowerPoint);
            pptFormats.Should().NotBeEmpty();
            pptFormats.Should().OnlyContain(f => f.Category == DocumentCategory.PowerPoint);
            pptFormats.Select(f => f.Extension).Should().Contain(new[] { ".pptx", ".ppt" });

            var pdfFormats = _registry.GetFormatsByCategory(DocumentCategory.Pdf);
            pdfFormats.Should().NotBeEmpty();
            pdfFormats.Should().OnlyContain(f => f.Category == DocumentCategory.Pdf);
            pdfFormats.Select(f => f.Extension).Should().Contain(".pdf");
        }

        [Theory]
        [InlineData("document.docx", true)]
        [InlineData("sheet.xlsx", true)]
        [InlineData("unknown.xyz123", false)]
        [InlineData("binary.exe", false)]
        public void IsSupported_ReturnsTrueOnlyForRegisteredFormats(string fileName, bool expectedSupported)
        {
            var supported = _registry.IsSupported(fileName);
            supported.Should().Be(expectedSupported);
        }

        [Fact]
        public void GetSupportedExtensions_FilteredByCategory_ReturnsCorrectExtensions()
        {
            var pdfExtensions = _registry.GetSupportedExtensions(DocumentCategory.Pdf);
            pdfExtensions.Should().ContainSingle().Which.Should().Be(".pdf");

            var allExtensions = _registry.GetSupportedExtensions();
            allExtensions.Should().Contain(new[] { ".docx", ".xlsx", ".pptx", ".pdf", ".txt", ".csv" });
        }

        [Fact]
        public void BenchmarkExecutionContext_RecordValuesAreProperlyInitialized()
        {
            var context = new BenchmarkExecutionContext(
                FilePath: @"C:\docs\sample.docx",
                FileName: "sample.docx",
                Extension: ".docx",
                Category: DocumentCategory.Word,
                FileSizeBytes: 10240,
                EngineId: "aspose",
                EngineDisplayName: "Aspose Words Engine",
                ExtractedText: "Hello world extracted text",
                ElapsedTime: TimeSpan.FromMilliseconds(45),
                AllocatedBytes: 1048576,
                IsSuccess: true
            );

            context.EngineId.Should().Be("aspose");
            context.CharacterCount.Should().Be(26);
            context.Category.Should().Be(DocumentCategory.Word);
            context.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void MetricScoreResult_CanBeInstantiatedWithScoresAndRanks()
        {
            var result = new MetricScoreResult(
                MetricId: "char_count",
                DisplayName: "Character Count",
                Unit: "chars",
                RawValue: 25000,
                FormattedValue: "25,000 chars",
                NormalizedScore: 100.0,
                Rank: 1,
                HigherIsBetter: true,
                Weight: 1.0,
                Notes: "Highest volume"
            );

            result.MetricId.Should().Be("char_count");
            result.Rank.Should().Be(1);
            result.NormalizedScore.Should().Be(100.0);
            result.HigherIsBetter.Should().BeTrue();
        }
    }
}

