using System;

namespace FileFormatAIStudio.Services.Ocr
{
    /// <summary>
    /// Metadata and payload of an image extracted from a document or standalone image upload.
    /// </summary>
    public sealed class ExtractedImageInfo
    {
        public byte[] RawBytes { get; }
        public int Width { get; }
        public int Height { get; }
        public string Format { get; }
        public int PageNumber { get; }
        public int ImageIndex { get; }
        public bool IsQualifyingForOcr { get; }
        public string RejectionReason { get; }

        public ExtractedImageInfo(
            byte[] rawBytes,
            int width,
            int height,
            string format,
            int pageNumber = 1,
            int imageIndex = 0,
            bool isQualifyingForOcr = true,
            string? rejectionReason = null)
        {
            RawBytes = rawBytes ?? Array.Empty<byte>();
            Width = width;
            Height = height;
            Format = format ?? "unknown";
            PageNumber = pageNumber;
            ImageIndex = imageIndex;
            IsQualifyingForOcr = isQualifyingForOcr;
            RejectionReason = rejectionReason ?? string.Empty;
        }

        public override string ToString() =>
            $"[Page {PageNumber}, Image {ImageIndex}] {Width}x{Height} {Format} (Qualifies: {IsQualifyingForOcr})";
    }
}

