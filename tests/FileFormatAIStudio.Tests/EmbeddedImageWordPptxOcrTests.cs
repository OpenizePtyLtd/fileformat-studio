using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Ocr;
using FileFormatAIStudio.Services.Parsing.Engines.PowerPoint;
using FileFormatAIStudio.Services.Parsing.Engines.Word;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class EmbeddedImageWordPptxOcrTests
    {
        [Fact]
        public void OpenXmlWordParser_ConstructsWithOcrDependencies()
        {
            var fakeOcr = new FakeOcrEngine();
            var extractor = new ImageExtractorService();
            var parser = new OpenXmlWordParser(fakeOcr, extractor);

            parser.EngineId.Should().Be("openxml-words");
            parser.IsAvailable.Should().BeTrue();
            parser.SupportedExtensions.Should().Contain(".docx");
        }

        [Fact]
        public void AsposeWordsParser_ConstructsWithOcrDependencies()
        {
            var fakeOcr = new FakeOcrEngine();
            var extractor = new ImageExtractorService();
            var parser = new AsposeWordsParser(licenseService: null, fakeOcr, extractor);

            parser.EngineId.Should().Be("aspose-words");
            parser.IsAvailable.Should().BeTrue();
            parser.SupportedExtensions.Should().Contain(".docx");
        }

        [Fact]
        public void AsposeSlidesParser_ConstructsWithOcrDependencies()
        {
            var fakeOcr = new FakeOcrEngine();
            var extractor = new ImageExtractorService();
            var parser = new AsposeSlidesParser(licenseService: null, fakeOcr, extractor);

            parser.EngineId.Should().Be("aspose-slides");
            parser.IsAvailable.Should().BeTrue();
            parser.SupportedExtensions.Should().Contain(".pptx");
        }

        private class FakeOcrEngine : IOcrEngine
        {
            public string EngineId => "fake-ocr";
            public string DisplayName => "Fake OCR";
            public bool IsAvailable => true;
            public IReadOnlyList<string> SupportedLanguages => new[] { "en-US" };
            public string TextToReturn { get; set; } = "Sample OCR Text";

            public Task<OcrResult> RecognizeTextAsync(Stream imageStream, string? languageTag = null, CancellationToken cancellationToken = default)
                => Task.FromResult(new OcrResult(TextToReturn));

            public Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string? languageTag = null, CancellationToken cancellationToken = default)
                => Task.FromResult(new OcrResult(TextToReturn));
        }
    }
}

