using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.AI
{
    /// <summary>
    /// Service contract for generating semantic visual descriptions of complex document diagrams, charts, and figures
    /// using multimodal AI vision models via Microsoft.Extensions.AI.
    /// </summary>
    public interface IVisionDescriptionService
    {
        /// <summary>
        /// Analyzes an image and produces a concise semantic description of its contents, data trends, or relationships.
        /// </summary>
        /// <param name="imageBytes">Raw image byte array.</param>
        /// <param name="mimeType">Mime type (e.g. "image/png", "image/jpeg").</param>
        /// <param name="contextHint">Optional contextual hint (such as slide header or section title).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Concise natural language summary of the figure, or empty string if disabled or unavailable.</returns>
        Task<string> DescribeImageAsync(
            byte[] imageBytes,
            string mimeType = "image/png",
            string? contextHint = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Indicates whether an active vision-capable model is configured and enabled in application settings.
        /// </summary>
        Task<bool> IsVisionEnabledAsync(CancellationToken cancellationToken = default);
    }
}

