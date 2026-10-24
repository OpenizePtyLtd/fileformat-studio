using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Ocr
{
    /// <summary>
    /// Contract for optical character recognition (OCR) engines that transcribe text from image streams.
    /// </summary>
    public interface IOcrEngine
    {
        /// <summary>
        /// Unique engine identifier (e.g. "windows-native-ocr").
        /// </summary>
        string EngineId { get; }

        /// <summary>
        /// Human-readable display name of the OCR engine.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Indicates whether the engine is operational and has required language packs/dependencies available on the current OS.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// BCP-47 language tags supported and installed on this machine (e.g. "en-US", "es-ES", "de-DE").
        /// </summary>
        IReadOnlyList<string> SupportedLanguages { get; }

        /// <summary>
        /// Performs OCR recognition on an image binary stream.
        /// </summary>
        /// <param name="imageStream">Readable stream containing an encoded raster image (PNG, JPEG, BMP, etc.).</param>
        /// <param name="languageTag">Optional target language tag. When null, the engine selects the system default or best matching language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Structured OCR recognition result.</returns>
        Task<OcrResult> RecognizeTextAsync(Stream imageStream, string? languageTag = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs OCR recognition on raw image bytes.
        /// </summary>
        /// <param name="imageBytes">Byte array containing an encoded raster image.</param>
        /// <param name="languageTag">Optional target language tag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Structured OCR recognition result.</returns>
        Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string? languageTag = null, CancellationToken cancellationToken = default);
    }
}

