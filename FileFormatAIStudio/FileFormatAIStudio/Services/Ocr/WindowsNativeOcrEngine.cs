using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace FileFormatAIStudio.Services.Ocr
{
    /// <summary>
    /// Native Windows OCR engine using Windows.Media.Ocr.OcrEngine.
    /// Provides fast, 100% local, offline text recognition without external cloud dependencies.
    /// </summary>
    public class WindowsNativeOcrEngine : IOcrEngine
    {
        public const string EngineIdentifier = "windows-native-ocr";

        public string EngineId => EngineIdentifier;

        public string DisplayName => "Windows Native OCR";

        /// <summary>
        /// True if at least one OCR language recognizer is available on the current Windows installation.
        /// </summary>
        public bool IsAvailable
        {
            get
            {
                try
                {
                    return OcrEngine.AvailableRecognizerLanguages.Count > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// List of installed and supported BCP-47 language tags on this Windows machine.
        /// </summary>
        public IReadOnlyList<string> SupportedLanguages
        {
            get
            {
                try
                {
                    return OcrEngine.AvailableRecognizerLanguages
                        .Select(l => l.LanguageTag)
                        .ToList();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }
        }

        public async Task<OcrResult> RecognizeTextAsync(Stream imageStream, string? languageTag = null, CancellationToken cancellationToken = default)
        {
            if (imageStream == null)
                throw new ArgumentNullException(nameof(imageStream));

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

            return await RecognizeTextAsync(bytes, languageTag, cancellationToken);
        }

        public async Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string? languageTag = null, CancellationToken cancellationToken = default)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return OcrResult.Empty;

            cancellationToken.ThrowIfCancellationRequested();

            var ocrEngine = ResolveEngine(languageTag);
            if (ocrEngine == null)
            {
                throw new InvalidOperationException("No supported Windows OCR language pack is available on this system.");
            }

            // Convert byte array into WinRT InMemoryRandomAccessStream
            using var memStream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(memStream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(imageBytes);
                await writer.StoreAsync().AsTask(cancellationToken);
                await writer.FlushAsync().AsTask(cancellationToken);
                writer.DetachStream();
            }
            memStream.Seek(0);

            // Decode image and convert to Bgra8 SoftwareBitmap
            var decoder = await BitmapDecoder.CreateAsync(memStream).AsTask(cancellationToken);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied).AsTask(cancellationToken);

            // Execute local OCR recognition
            var nativeResult = await ocrEngine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken);

            // Map native lines and words to clean domain models
            var lines = new List<OcrLine>();
            if (nativeResult.Lines != null)
            {
                foreach (var line in nativeResult.Lines)
                {
                    var words = line.Words?
                        .Select(w => new OcrWord(
                            w.Text,
                            w.BoundingRect.X,
                            w.BoundingRect.Y,
                            w.BoundingRect.Width,
                            w.BoundingRect.Height))
                        .ToList() ?? new List<OcrWord>();

                    lines.Add(new OcrLine(line.Text, words));
                }
            }

            return new OcrResult(
                nativeResult.Text ?? string.Empty,
                lines,
                nativeResult.TextAngle,
                ocrEngine.RecognizerLanguage?.LanguageTag ?? languageTag ?? string.Empty);
        }

        private static OcrEngine? ResolveEngine(string? languageTag)
        {
            if (!string.IsNullOrWhiteSpace(languageTag))
            {
                try
                {
                    var targetLang = new Language(languageTag);
                    if (OcrEngine.IsLanguageSupported(targetLang))
                    {
                        var engine = OcrEngine.TryCreateFromLanguage(targetLang);
                        if (engine != null)
                            return engine;
                    }
                }
                catch
                {
                    // Fall back to default
                }
            }

            // Try user profile languages first
            var profileEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (profileEngine != null)
                return profileEngine;

            // Fallback to first available installed language
            if (OcrEngine.AvailableRecognizerLanguages.Count > 0)
            {
                return OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
            }

            return null;
        }
    }
}

