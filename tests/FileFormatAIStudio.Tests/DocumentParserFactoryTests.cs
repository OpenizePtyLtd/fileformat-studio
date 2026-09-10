using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Parsing;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class DocumentParserFactoryTests
    {
        private class MockParser : IDocumentParser
        {
            public string EngineId { get; init; } = string.Empty;
            public string DisplayName { get; init; } = string.Empty;
            public int Priority { get; init; } = 50;
            public bool IsAvailable { get; init; } = true;
            public IReadOnlySet<string> SupportedExtensions { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Func<string, string>? ExtractTextFunc { get; init; }

            public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
            {
                if (ExtractTextFunc != null)
                {
                    return Task.FromResult(ExtractTextFunc(filePath));
                }
                return Task.FromResult($"Extracted by {EngineId} from {Path.GetFileName(filePath)}");
            }
        }

        [Fact]
        public void GetAllParsers_ReturnsAllRegisteredParsers()
        {
            var p1 = new MockParser { EngineId = "engine1" };
            var p2 = new MockParser { EngineId = "engine2" };
            var factory = new DocumentParserFactory(new[] { p1, p2 });

            factory.GetAllParsers().Should().HaveCount(2).And.Contain(new[] { p1, p2 });
        }

        [Theory]
        [InlineData("aspose", "aspose")]
        [InlineData("ASPOSE", "aspose")]
        [InlineData("OfficeParser", "officeparser")]
        public void GetParser_WithValidEngineId_ReturnsParserCaseInsensitively(string queryId, string expectedId)
        {
            var p1 = new MockParser { EngineId = "aspose" };
            var p2 = new MockParser { EngineId = "officeparser" };
            var factory = new DocumentParserFactory(new[] { p1, p2 });

            var result = factory.GetParser(queryId);

            result.Should().NotBeNull();
            result!.EngineId.Should().Be(expectedId);
        }

        [Fact]
        public void GetParser_WithUnknownEngineId_ReturnsNull()
        {
            var factory = new DocumentParserFactory(new[] { new MockParser { EngineId = "aspose" } });

            var result = factory.GetParser("nonexistent");

            result.Should().BeNull();
        }

        [Fact]
        public void ResolveParser_WithMultipleParsers_SelectsHighestPriorityAvailableParser()
        {
            var pLow = new MockParser
            {
                EngineId = "low",
                Priority = 10,
                IsAvailable = true,
                SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" }
            };
            var pHigh = new MockParser
            {
                EngineId = "high",
                Priority = 100,
                IsAvailable = true,
                SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" }
            };

            var factory = new DocumentParserFactory(new[] { pLow, pHigh });

            var result = factory.ResolveParser("test.docx");

            result.Should().NotBeNull();
            result!.EngineId.Should().Be("high");
        }

        [Fact]
        public void ResolveParser_WithUnavailableParser_SkipsToNextAvailable()
        {
            var pHighUnavailable = new MockParser
            {
                EngineId = "high-unavailable",
                Priority = 100,
                IsAvailable = false,
                SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" }
            };
            var pLowAvailable = new MockParser
            {
                EngineId = "low-available",
                Priority = 20,
                IsAvailable = true,
                SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" }
            };

            var factory = new DocumentParserFactory(new[] { pHighUnavailable, pLowAvailable });

            var result = factory.ResolveParser("document.docx");

            result.Should().NotBeNull();
            result!.EngineId.Should().Be("low-available");
        }

        [Fact]
        public void ResolveParser_WithExplicitEngineId_SelectsThatSpecificEngine()
        {
            var p1 = new MockParser
            {
                EngineId = "aspose",
                Priority = 100,
                SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" }
            };
            var p2 = new MockParser
            {
                EngineId = "officeparser",
                Priority = 50,
                SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" }
            };

            var factory = new DocumentParserFactory(new[] { p1, p2 });

            // Explicitly request officeparser despite lower priority
            var result = factory.ResolveParser("doc.docx", engineId: "officeparser");

            result.Should().NotBeNull();
            result!.EngineId.Should().Be("officeparser");
        }

        [Fact]
        public void ResolveParser_WithExplicitEngineId_ReturnsNullIfFormatNotSupportedByEngine()
        {
            var p = new MockParser
            {
                EngineId = "officeparser",
                SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" }
            };

            var factory = new DocumentParserFactory(new[] { p });

            var result = factory.ResolveParser("table.xlsx", engineId: "officeparser");

            result.Should().BeNull();
        }

        [Fact]
        public void GetSupportedExtensions_AggregatesAllExtensionsFromAvailableParsers()
        {
            var p1 = new MockParser
            {
                EngineId = "p1",
                IsAvailable = true,
                SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx", ".pdf" }
            };
            var p2 = new MockParser
            {
                EngineId = "p2",
                IsAvailable = true,
                SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".xlsx" }
            };
            var p3Unavailable = new MockParser
            {
                EngineId = "p3",
                IsAvailable = false,
                SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pptx" }
            };

            var factory = new DocumentParserFactory(new[] { p1, p2, p3Unavailable });

            var extensions = factory.GetSupportedExtensions();

            extensions.Should().BeEquivalentTo(new[] { ".docx", ".pdf", ".xlsx" });
            extensions.Should().NotContain(".pptx");
        }

        [Fact]
        public async Task ExtractTextAsync_AutoSelectsAndExtractsText()
        {
            var tempFile = Path.GetTempFileName();
            var docxFile = Path.ChangeExtension(tempFile, ".docx");
            File.Move(tempFile, docxFile);

            try
            {
                var parser = new MockParser
                {
                    EngineId = "aspose",
                    SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" },
                    ExtractTextFunc = _ => "Sample document content extracted."
                };

                var factory = new DocumentParserFactory(new[] { parser });

                var text = await factory.ExtractTextAsync(docxFile);

                text.Should().Be("Sample document content extracted.");
            }
            finally
            {
                if (File.Exists(docxFile)) File.Delete(docxFile);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_WithSpecificEngine_UsesSpecifiedEngine()
        {
            var tempFile = Path.GetTempFileName();
            var docxFile = Path.ChangeExtension(tempFile, ".docx");
            File.Move(tempFile, docxFile);

            try
            {
                var p1 = new MockParser
                {
                    EngineId = "aspose",
                    Priority = 100,
                    SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" },
                    ExtractTextFunc = _ => "From Aspose"
                };
                var p2 = new MockParser
                {
                    EngineId = "officeparser",
                    Priority = 50,
                    SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" },
                    ExtractTextFunc = _ => "From OfficeParser"
                };

                var factory = new DocumentParserFactory(new[] { p1, p2 });

                var text = await factory.ExtractTextAsync(docxFile, engineId: "officeparser");

                text.Should().Be("From OfficeParser");
            }
            finally
            {
                if (File.Exists(docxFile)) File.Delete(docxFile);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_ThrowsFileNotFoundException_WhenFileDoesNotExist()
        {
            var factory = new DocumentParserFactory(Array.Empty<IDocumentParser>());

            var act = async () => await factory.ExtractTextAsync(@"C:\nonexistent_file_xyz_123.docx");

            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task ExtractTextAsync_ThrowsNotSupportedException_WhenNoParserSupportsFormat()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var factory = new DocumentParserFactory(Array.Empty<IDocumentParser>());

                var act = async () => await factory.ExtractTextAsync(tempFile);

                await act.Should().ThrowAsync<NotSupportedException>();
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_ThrowsInvalidOperationException_WhenRequestedEngineDoesNotExist()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var factory = new DocumentParserFactory(Array.Empty<IDocumentParser>());

                var act = async () => await factory.ExtractTextAsync(tempFile, engineId: "unknown-engine");

                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("*not registered*");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Theory]
        [InlineData(".docx", ".docx")]
        [InlineData("docx", ".docx")]
        [InlineData("DOCX", ".docx")]
        [InlineData("C:\\docs\\report.PDF", ".pdf")]
        [InlineData("docs/sub/readme.MD", ".md")]
        public void NormalizeExtension_HandlesVariousFormats(string input, string expected)
        {
            var result = DocumentParserFactory.NormalizeExtension(input);
            result.Should().Be(expected);
        }
    }
}

