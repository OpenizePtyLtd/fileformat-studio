namespace FileFormatAIStudio.Services.Ocr
{
    /// <summary>
    /// Represents an individual recognized word and its bounding box coordinates within an OCR scan.
    /// </summary>
    public sealed class OcrWord
    {
        public string Text { get; }
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }

        public OcrWord(string text, double x, double y, double width, double height)
        {
            Text = text ?? string.Empty;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public override string ToString() => Text;
    }
}

