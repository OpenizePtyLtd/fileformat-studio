using System;
using System.IO;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Ocr;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class WindowsNativeOcrEngineTests
    {
        private readonly WindowsNativeOcrEngine _engine = new();

        [Fact]
        public void EngineProperties_ShouldBeCorrect()
        {
            _engine.EngineId.Should().Be("windows-native-ocr");
            _engine.DisplayName.Should().Be("Windows Native OCR");
            _engine.IsAvailable.Should().BeTrue();
            _engine.SupportedLanguages.Should().NotBeNull();
            _engine.SupportedLanguages.Should().NotBeEmpty();
        }

        [Fact]
        public async Task RecognizeTextAsync_WithEmptyBytes_ShouldReturnEmptyResult()
        {
            var result = await _engine.RecognizeTextAsync(Array.Empty<byte>());
            result.Should().NotBeNull();
            result.HasText.Should().BeFalse();
            result.Text.Should().BeEmpty();
            result.Lines.Should().BeEmpty();
        }

        [Fact]
        public async Task RecognizeTextAsync_WithNullStream_ShouldThrowArgumentNullException()
        {
            Func<Task> act = async () => await _engine.RecognizeTextAsync((Stream)null!);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task RecognizeTextAsync_WithBlankImageStream_ShouldReturnResultWithoutThrowing()
        {
            // Create a small 100x100 24-bit blank BMP image in memory
            var bmpBytes = CreateBlankBmp(100, 100);
            using var stream = new MemoryStream(bmpBytes);

            var result = await _engine.RecognizeTextAsync(stream);

            result.Should().NotBeNull();
            result.Lines.Should().NotBeNull();
            // A pure white canvas produces empty text but valid non-null result
            result.Text.Should().BeEmpty();
        }

        private static byte[] CreateBlankBmp(int width, int height)
        {
            int rowSize = (width * 3 + 3) & ~3;
            int pixelArraySize = rowSize * height;
            int fileSize = 54 + pixelArraySize;

            byte[] bmp = new byte[fileSize];

            // BMP Header
            bmp[0] = 0x42; // 'B'
            bmp[1] = 0x4D; // 'M'
            BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
            BitConverter.GetBytes(54).CopyTo(bmp, 10); // Offset to pixel data

            // DIB Header (BITMAPINFOHEADER)
            BitConverter.GetBytes(40).CopyTo(bmp, 14); // Header size
            BitConverter.GetBytes(width).CopyTo(bmp, 18);
            BitConverter.GetBytes(height).CopyTo(bmp, 22);
            BitConverter.GetBytes((short)1).CopyTo(bmp, 26); // Color planes
            BitConverter.GetBytes((short)24).CopyTo(bmp, 28); // Bits per pixel
            BitConverter.GetBytes(pixelArraySize).CopyTo(bmp, 34);

            // Fill with white pixels (0xFF)
            for (int i = 54; i < fileSize; i++)
            {
                bmp[i] = 0xFF;
            }

            return bmp;
        }
    }
}

