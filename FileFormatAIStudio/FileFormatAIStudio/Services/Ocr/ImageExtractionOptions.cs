namespace FileFormatAIStudio.Services.Ocr
{
    /// <summary>
    /// Configuration options for filtering and qualifying extracted images for OCR and AI Vision pipelines.
    /// </summary>
    public sealed class ImageExtractionOptions
    {
        public static ImageExtractionOptions Default { get; } = new();

        /// <summary>
        /// Minimum image width in pixels. Images narrower than this are treated as icons/decorations and skipped.
        /// Default: 100 pixels.
        /// </summary>
        public int MinWidth { get; set; } = 100;

        /// <summary>
        /// Minimum image height in pixels. Images shorter than this are treated as icons/bullets and skipped.
        /// Default: 100 pixels.
        /// </summary>
        public int MinHeight { get; set; } = 100;

        /// <summary>
        /// Maximum allowed aspect ratio (e.g. 15.0 for 15:1 or 1:15).
        /// Images with aspect ratios beyond this are recognized as thin horizontal/vertical line dividers or border frames.
        /// Default: 15.0.
        /// </summary>
        public double MaxAspectRatio { get; set; } = 15.0;

        /// <summary>
        /// Maximum allowed raw image payload size in bytes to prevent runaway memory usage.
        /// Default: 25 MB (26,214,400 bytes).
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;
    }
}

