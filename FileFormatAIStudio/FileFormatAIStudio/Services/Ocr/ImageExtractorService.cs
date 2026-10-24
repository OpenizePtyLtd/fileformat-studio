using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace FileFormatAIStudio.Services.Ocr
{
    /// <summary>
    /// Default implementation of IImageExtractorService using Windows.Graphics.Imaging for high-performance format detection and dimension evaluation.
    /// </summary>
    public class ImageExtractorService : IImageExtractorService
    {
        public async Task<ExtractedImageInfo> InspectAndFilterAsync(
            Stream imageStream,
            int pageNumber = 1,
            int imageIndex = 0,
            ImageExtractionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (imageStream == null)
            {
                return new ExtractedImageInfo(
                    Array.Empty<byte>(),
                    0,
                    0,
                    "unknown",
                    pageNumber,
                    imageIndex,
                    isQualifyingForOcr: false,
                    rejectionReason: "Image stream is null");
            }

            byte[] bytes;
            if (imageStream is MemoryStream ms)
            {
                bytes = ms.ToArray();
            }
            else
            {
                using var tempMs = new MemoryStream();
                await imageStream.CopyToAsync(tempMs, cancellationToken);
                bytes = tempMs.ToArray();
            }

            return await InspectAndFilterAsync(bytes, pageNumber, imageIndex, options, cancellationToken);
        }

        public async Task<ExtractedImageInfo> InspectAndFilterAsync(
            byte[] imageBytes,
            int pageNumber = 1,
            int imageIndex = 0,
            ImageExtractionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= ImageExtractionOptions.Default;

            if (imageBytes == null || imageBytes.Length == 0)
            {
                return new ExtractedImageInfo(
                    Array.Empty<byte>(),
                    0,
                    0,
                    "unknown",
                    pageNumber,
                    imageIndex,
                    isQualifyingForOcr: false,
                    rejectionReason: "Image payload is null or empty");
            }

            if (imageBytes.LongLength > options.MaxFileSizeBytes)
            {
                return new ExtractedImageInfo(
                    imageBytes,
                    0,
                    0,
                    DetectFormatFromMagicBytes(imageBytes),
                    pageNumber,
                    imageIndex,
                    isQualifyingForOcr: false,
                    rejectionReason: $"Image size ({imageBytes.Length} bytes) exceeds maximum allowed size ({options.MaxFileSizeBytes} bytes)");
            }

            cancellationToken.ThrowIfCancellationRequested();

            string format = DetectFormatFromMagicBytes(imageBytes);

            try
            {
                using var memStream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(memStream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(imageBytes);
                    await writer.StoreAsync().AsTask(cancellationToken);
                    await writer.FlushAsync().AsTask(cancellationToken);
                    writer.DetachStream();
                }
                memStream.Seek(0);

                var decoder = await BitmapDecoder.CreateAsync(memStream).AsTask(cancellationToken);
                int width = (int)decoder.PixelWidth;
                int height = (int)decoder.PixelHeight;

                // 1. Dimension Threshold Filter (Skip small icons/bullets)
                if (width < options.MinWidth || height < options.MinHeight)
                {
                    return new ExtractedImageInfo(
                        imageBytes,
                        width,
                        height,
                        format,
                        pageNumber,
                        imageIndex,
                        isQualifyingForOcr: false,
                        rejectionReason: $"Dimensions {width}x{height} below minimum threshold {options.MinWidth}x{options.MinHeight}");
                }

                // 2. Aspect Ratio Filter (Skip thin decorative divider lines or vertical border stripes)
                double maxDim = Math.Max(width, height);
                double minDim = Math.Max(1, Math.Min(width, height));
                double ratio = maxDim / minDim;

                if (ratio > options.MaxAspectRatio)
                {
                    return new ExtractedImageInfo(
                        imageBytes,
                        width,
                        height,
                        format,
                        pageNumber,
                        imageIndex,
                        isQualifyingForOcr: false,
                        rejectionReason: $"Extreme aspect ratio {ratio:F1}:1 exceeds decorative border threshold {options.MaxAspectRatio:F1}:1");
                }

                // Image qualifies for OCR / Vision ingestion
                return new ExtractedImageInfo(
                    imageBytes,
                    width,
                    height,
                    format,
                    pageNumber,
                    imageIndex,
                    isQualifyingForOcr: true,
                    rejectionReason: null);
            }
            catch (Exception ex)
            {
                return new ExtractedImageInfo(
                    imageBytes,
                    0,
                    0,
                    format,
                    pageNumber,
                    imageIndex,
                    isQualifyingForOcr: false,
                    rejectionReason: $"Failed to decode image metadata: {ex.Message}");
            }
        }

        public static string DetectFormatFromMagicBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4)
                return "unknown";

            // PNG: 89 50 4E 47
            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return "png";

            // JPEG: FF D8 FF
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return "jpeg";

            // BMP: 42 4D
            if (bytes[0] == 0x42 && bytes[1] == 0x4D)
                return "bmp";

            // GIF: 47 49 46
            if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
                return "gif";

            // TIFF: 49 49 2A 00 (Little-endian) or 4D 4D 00 2A (Big-endian)
            if ((bytes[0] == 0x49 && bytes[1] == 0x49 && bytes[2] == 0x2A && bytes[3] == 0x00) ||
                (bytes[0] == 0x4D && bytes[1] == 0x4D && bytes[2] == 0x00 && bytes[3] == 0x2A))
                return "tiff";

            // WebP: RIFF ... WEBP
            if (bytes.Length >= 12 &&
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
                return "webp";

            return "unknown";
        }
    }
}

