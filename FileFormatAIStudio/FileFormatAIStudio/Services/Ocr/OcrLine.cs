using System;
using System.Collections.Generic;

namespace FileFormatAIStudio.Services.Ocr
{
    /// <summary>
    /// Represents a single recognized line of text composed of individual words within an OCR scan.
    /// </summary>
    public sealed class OcrLine
    {
        public string Text { get; }
        public IReadOnlyList<OcrWord> Words { get; }

        public OcrLine(string text, IReadOnlyList<OcrWord>? words = null)
        {
            Text = text ?? string.Empty;
            Words = words ?? Array.Empty<OcrWord>();
        }

        public override string ToString() => Text;
    }
}

