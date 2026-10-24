using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Ocr;
using FileFormatAIStudio.Services.Parsing.Engines.Image;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class ImageDocumentParserTests
    {
        [Fact]
        public void DocumentCategoryRegistry_ShouldContainImageCategoryAndFormats()
        {
            var registry = new DocumentCategoryRegistry();

            registry.GetAllCategories().Should().Contain(DocumentCategory.Image);
            registry.GetCategoryDisplayName(DocumentCategory.Image).Should().Be("Images & Scans");

            registry.ResolveCategory("invoice.png").Should().Be(DocumentCategory.Image);
            registry.ResolveCategory("photo.jpg").Should().Be(DocumentCategory.Image);
            registry.ResolveCategory("scan.jpeg").Should().Be(DocumentCategory.Image);
            registry.ResolveCategory("receipt.bmp").Should().Be(DocumentCategory.Image);
            registry.ResolveCategory("contract.tiff").Should().Be(DocumentCategory.Image);
            registry.ResolveCategory("fax.tif").Should().Be(DocumentCategory.Image);
            registry.ResolveCategory("chart.webp").Should().Be(DocumentCategory.Image);

            registry.IsSupported("sample.png").Should().BeTrue();
            registry.IsSupported("sample.jpg").Should().BeTrue();
        }

        [Fact]
        public void ImageDocumentParser_Properties_ShouldBeCorrect()
        {
            var fakeOcr = new FakeOcrEngine();
            var extractor = new ImageExtractorService();
            var parser = new ImageDocumentParser(fakeOcr, extractor);

            parser.Category.Should().Be(DocumentCategory.Image);
            parser.EngineId.Should().Be("windows-ocr-image");
            parser.DisplayName.Should().Be("Windows Native OCR (Image)");
            parser.Priority.Should().Be(100);
            parser.IsAvailable.Should().BeTrue();
            parser.SupportedExtensions.Should().Contain(new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".webp" });
        }

        [Fact]
        public async Task ImageDocumentParser_ExtractTextAsync_WithValidImage_ShouldReturnRecognizedText()
        {
            var fakeOcr = new FakeOcrEngine { TextToReturn = "Invoice #1234 Total: $450.00" };
            var extractor = new ImageExtractorService();
            var parser = new ImageDocumentParser(fakeOcr, extractor);

            string tempFile = Path.Combine(Path.GetTempPath(), $"FFTest_img_{Guid.NewGuid():N}.bmp");
            try
            {
                // Create a qualifying 200x200 BMP image
                File.WriteAllBytes(tempFile, CreateBmp(200, 200));

                var text = await parser.ExtractTextAsync(tempFile);
                text.Should().Be("Invoice #1234 Total: $450.00");
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ImageDocumentParser_ExtractTextAsync_WhenImageRejectedByFilter_ShouldReturnEmptyString()
        {
            var fakeOcr = new FakeOcrEngine { TextToReturn = "Should not be called" };
            var extractor = new ImageExtractorService();
            var parser = new ImageDocumentParser(fakeOcr, extractor);

            string tempFile = Path.Combine(Path.GetTempPath(), $"FFTest_tiny_{Guid.NewGuid():N}.bmp");
            try
            {
                // Create a 20x20 BMP (below 100x100 threshold)
                File.WriteAllBytes(tempFile, CreateBmp(20, 20));

                var text = await parser.ExtractTextAsync(tempFile);
                text.Should().BeEmpty();
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        private static byte[] CreateBmp(int width, int height)
        {
            int rowSize = (width * 3 + 3) & ~3;
            int pixelArraySize = rowSize * height;
            int fileSize = 54 + pixelArraySize;

            byte[] bmp = new byte[fileSize];

            // BMP Header
            bmp[0] = 0x42; // 'B'
            bmp[1] = 0x4D; // 'M'
            BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
            BitConverter.GetBytes(54).CopyTo(bmp, 10);

            // DIB Header
            BitConverter.GetBytes(40).CopyTo(bmp, 14);
            BitConverter.GetBytes(width).CopyTo(bmp, 18);
            BitConverter.GetBytes(height).CopyTo(bmp, 22);
            BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
            BitConverter.GetBytes((short)24).CopyTo(bmp, 28);
            BitConverter.GetBytes(pixelArraySize).CopyTo(bmp, 34);

            return bmp;
        }

        private class FakeOcrEngine : IOcrEngine
        {
            public string EngineId => "fake-ocr";
            public string DisplayName => "Fake OCR";
            public bool IsAvailable => true;
            public IReadOnlyList<string> SupportedLanguages => new[] { "en-US" };
            public string TextToReturn { get; set; } = string.Empty;

            public Task<OcrResult> RecognizeTextAsync(Stream imageStream, string? languageTag = null, CancellationToken cancellationToken = default)
                => Task.FromResult(new OcrResult(TextToReturn));

            public Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string? languageTag = null, CancellationToken cancellationToken = default)
                => Task.FromResult(new OcrResult(TextToReturn));
        }
    }
}

