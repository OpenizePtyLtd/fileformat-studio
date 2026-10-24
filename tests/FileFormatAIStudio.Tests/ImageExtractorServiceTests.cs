using System;
using System.IO;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Ocr;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class ImageExtractorServiceTests
    {
        private readonly ImageExtractorService _service = new();

        [Fact]
        public async Task InspectAndFilterAsync_WithNullBytes_ShouldRejectCleanly()
        {
            var result = await _service.InspectAndFilterAsync((byte[])null!);
            result.Should().NotBeNull();
            result.IsQualifyingForOcr.Should().BeFalse();
            result.RejectionReason.Should().Contain("null or empty");
        }

        [Fact]
        public async Task InspectAndFilterAsync_WithNullStream_ShouldRejectCleanly()
        {
            var result = await _service.InspectAndFilterAsync((Stream)null!);
            result.Should().NotBeNull();
            result.IsQualifyingForOcr.Should().BeFalse();
            result.RejectionReason.Should().Contain("null");
        }

        [Fact]
        public void DetectFormatFromMagicBytes_ShouldDetectFormatsAccurately()
        {
            byte[] png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            byte[] jpeg = { 0xFF, 0xD8, 0xFF, 0xE0 };
            byte[] bmp = { 0x42, 0x4D, 0x00, 0x00 };
            byte[] gif = { 0x47, 0x49, 0x46, 0x38 };
            byte[] tiffLe = { 0x49, 0x49, 0x2A, 0x00 };
            byte[] tiffBe = { 0x4D, 0x4D, 0x00, 0x2A };
            byte[] webp = { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
            byte[] unknown = { 0x00, 0x01, 0x02, 0x03 };

            ImageExtractorService.DetectFormatFromMagicBytes(png).Should().Be("png");
            ImageExtractorService.DetectFormatFromMagicBytes(jpeg).Should().Be("jpeg");
            ImageExtractorService.DetectFormatFromMagicBytes(bmp).Should().Be("bmp");
            ImageExtractorService.DetectFormatFromMagicBytes(gif).Should().Be("gif");
            ImageExtractorService.DetectFormatFromMagicBytes(tiffLe).Should().Be("tiff");
            ImageExtractorService.DetectFormatFromMagicBytes(tiffBe).Should().Be("tiff");
            ImageExtractorService.DetectFormatFromMagicBytes(webp).Should().Be("webp");
            ImageExtractorService.DetectFormatFromMagicBytes(unknown).Should().Be("unknown");
        }

        [Fact]
        public async Task InspectAndFilterAsync_WhenDimensionsBelowThreshold_ShouldReject()
        {
            // 40x40 pixel BMP (smaller than default 100x100 min threshold)
            byte[] smallBmp = CreateBmp(40, 40);

            var result = await _service.InspectAndFilterAsync(smallBmp);

            result.IsQualifyingForOcr.Should().BeFalse();
            result.Width.Should().Be(40);
            result.Height.Should().Be(40);
            result.Format.Should().Be("bmp");
            result.RejectionReason.Should().Contain("below minimum threshold");
        }

        [Fact]
        public async Task InspectAndFilterAsync_WhenDimensionsExceedThreshold_ShouldQualify()
        {
            // 200x150 pixel BMP (satisfies dimensions)
            byte[] validBmp = CreateBmp(200, 150);

            var result = await _service.InspectAndFilterAsync(validBmp, pageNumber: 3, imageIndex: 1);

            result.IsQualifyingForOcr.Should().BeTrue();
            result.Width.Should().Be(200);
            result.Height.Should().Be(150);
            result.Format.Should().Be("bmp");
            result.PageNumber.Should().Be(3);
            result.ImageIndex.Should().Be(1);
            result.RejectionReason.Should().BeEmpty();
        }

        [Fact]
        public async Task InspectAndFilterAsync_WhenAspectRatioExtreme_ShouldRejectAsDecorativeBorder()
        {
            // 1600x100 pixel banner divider (both dims satisfy 100x100 min threshold, but aspect ratio 16:1 exceeds 15:1 limit)
            byte[] dividerBmp = CreateBmp(1600, 100);

            var result = await _service.InspectAndFilterAsync(dividerBmp);

            result.IsQualifyingForOcr.Should().BeFalse();
            result.RejectionReason.Should().Contain("aspect ratio");
        }

        [Fact]
        public async Task InspectAndFilterAsync_WhenFileSizeExceedsLimit_ShouldReject()
        {
            byte[] bmp = CreateBmp(120, 120);
            var customOptions = new ImageExtractionOptions
            {
                MaxFileSizeBytes = 100 // artificially low threshold
            };

            var result = await _service.InspectAndFilterAsync(bmp, options: customOptions);

            result.IsQualifyingForOcr.Should().BeFalse();
            result.RejectionReason.Should().Contain("exceeds maximum allowed size");
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
            BitConverter.GetBytes(54).CopyTo(bmp, 10); // Offset

            // DIB Header
            BitConverter.GetBytes(40).CopyTo(bmp, 14);
            BitConverter.GetBytes(width).CopyTo(bmp, 18);
            BitConverter.GetBytes(height).CopyTo(bmp, 22);
            BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
            BitConverter.GetBytes((short)24).CopyTo(bmp, 28);
            BitConverter.GetBytes(pixelArraySize).CopyTo(bmp, 34);

            return bmp;
        }
    }
}

