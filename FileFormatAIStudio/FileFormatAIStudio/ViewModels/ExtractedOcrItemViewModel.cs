using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.ApplicationModel.DataTransfer;

namespace FileFormatAIStudio.ViewModels
{
    /// <summary>
    /// ViewModel representing a recognized image, scanned page, or diagram extracted via OCR / AI Vision.
    /// </summary>
    public partial class ExtractedOcrItemViewModel : ObservableObject
    {
        public string Title { get; init; } = string.Empty;
        public string SourceContext { get; init; } = string.Empty;
        public string OcrText { get; init; } = string.Empty;
        public string Glyph { get; init; } = "\uEB9F";
        public int CharacterCount => OcrText?.Length ?? 0;
        public string FormattedStats => $"{CharacterCount:N0} chars";

        [ObservableProperty]
        private bool _isCopied;

        [RelayCommand]
        public void CopyText()
        {
            if (string.IsNullOrWhiteSpace(OcrText)) return;
            try
            {
                var pkg = new DataPackage();
                pkg.SetText(OcrText);
                Clipboard.SetContent(pkg);
                IsCopied = true;
            }
            catch
            {
                // Soft-fail for clipboard
            }
        }
    }
}

