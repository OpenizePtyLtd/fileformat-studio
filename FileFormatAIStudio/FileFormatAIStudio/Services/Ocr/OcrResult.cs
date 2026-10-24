using System;
using System.Collections.Generic;

namespace FileFormatAIStudio.Services.Ocr
{
    /// <summary>
    /// Result payload produced by an OCR engine containing full recognized text, lines, words, and metadata.
    /// </summary>
    public sealed class OcrResult
    {
        public static OcrResult Empty { get; } = new(string.Empty, Array.Empty<OcrLine>(), null, string.Empty);

        public string Text { get; }
        public IReadOnlyList<OcrLine> Lines { get; }
        public double? TextAngle { get; }
        public string LanguageTag { get; }
        public bool HasText => !string.IsNullOrWhiteSpace(Text);

        public OcrResult(
            string text,
            IReadOnlyList<OcrLine>? lines = null,
            double? textAngle = null,
            string? languageTag = null)
        {
            Text = text ?? string.Empty;
            Lines = lines ?? Array.Empty<OcrLine>();
            TextAngle = textAngle;
            LanguageTag = languageTag ?? string.Empty;
        }

        public override string ToString() => Text;
    }
}

