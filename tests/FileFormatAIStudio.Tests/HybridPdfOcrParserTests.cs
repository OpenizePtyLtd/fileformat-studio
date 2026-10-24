using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Ocr;
using FileFormatAIStudio.Services.Parsing.Engines.Pdf;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class HybridPdfOcrParserTests
    {
        [Fact]
        public void PdfPigParser_ConstructsWithOcrDependencies()
        {
            var fakeOcr = new FakeOcrEngine();
            var extractor = new ImageExtractorService();
            var parser = new PdfPigParser(fakeOcr, extractor);

            parser.EngineId.Should().Be("pdfpig");
            parser.IsAvailable.Should().BeTrue();
            parser.SupportedExtensions.Should().Contain(".pdf");
        }

        [Fact]
        public void AsposePdfParser_ConstructsWithOcrDependencies()
        {
            var fakeOcr = new FakeOcrEngine();
            var extractor = new ImageExtractorService();
            var parser = new AsposePdfParser(licenseService: null, fakeOcr, extractor);

            parser.EngineId.Should().Be("aspose-pdf");
            parser.IsAvailable.Should().BeTrue();
            parser.SupportedExtensions.Should().Contain(".pdf");
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

