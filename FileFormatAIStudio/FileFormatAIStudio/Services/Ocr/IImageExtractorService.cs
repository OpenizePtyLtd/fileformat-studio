using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Ocr
{
    /// <summary>
    /// Service contract for inspecting, validating, and filtering document image streams before forwarding to OCR or Vision models.
    /// Filters out decorative noise such as small icons, bullet points, and thin border dividers.
    /// </summary>
    public interface IImageExtractorService
    {
        /// <summary>
        /// Inspects an image byte payload, extracts dimensions/format, and evaluates qualification based on filter rules.
        /// </summary>
        /// <param name="imageBytes">Raw image byte array.</param>
        /// <param name="pageNumber">Page number where the image was found.</param>
        /// <param name="imageIndex">Ordinal index of the image within the page/document.</param>
        /// <param name="options">Optional filtering options. Defaults to ImageExtractionOptions.Default if null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Extracted image metadata and qualification verdict.</returns>
        Task<ExtractedImageInfo> InspectAndFilterAsync(
            byte[] imageBytes,
            int pageNumber = 1,
            int imageIndex = 0,
            ImageExtractionOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Inspects an image stream, extracts dimensions/format, and evaluates qualification based on filter rules.
        /// </summary>
        /// <param name="imageStream">Readable stream containing encoded image binary.</param>
        /// <param name="pageNumber">Page number where the image was found.</param>
        /// <param name="imageIndex">Ordinal index of the image within the page/document.</param>
        /// <param name="options">Optional filtering options. Defaults to ImageExtractionOptions.Default if null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Extracted image metadata and qualification verdict.</returns>
        Task<ExtractedImageInfo> InspectAndFilterAsync(
            Stream imageStream,
            int pageNumber = 1,
            int imageIndex = 0,
            ImageExtractionOptions? options = null,
            CancellationToken cancellationToken = default);
    }
}

