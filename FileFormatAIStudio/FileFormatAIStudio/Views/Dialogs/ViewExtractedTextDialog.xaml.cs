using System;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FileFormatAIStudio.Views.Dialogs
{
    public sealed partial class ViewExtractedTextDialog : ContentDialog
    {
        public ViewExtractedTextViewModel ViewModel { get; }

        public ViewExtractedTextDialog(ViewExtractedTextViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            this.MaxWidth = 960;
            UpdateTabStyles();
        }

        public async Task InitializeAsync(Guid documentId, KnowledgebaseDocumentEntity? meta = null)
        {
            await ViewModel.LoadDocumentAsync(documentId, meta);
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Do not close the dialog when copying to clipboard so user can continue reading
            args.Cancel = true;
            ViewModel.CopyToClipboard();
        }

        private void OnFullTextTabClicked(object sender, RoutedEventArgs e)
        {
            FullTextContainer.Visibility = Visibility.Visible;
            ChunksContainer.Visibility = Visibility.Collapsed;
            OcrContainer.Visibility = Visibility.Collapsed;
            UpdateTabStyles();
        }

        private void OnChunksTabClicked(object sender, RoutedEventArgs e)
        {
            FullTextContainer.Visibility = Visibility.Collapsed;
            ChunksContainer.Visibility = Visibility.Visible;
            OcrContainer.Visibility = Visibility.Collapsed;
            UpdateTabStyles();
        }

        private void OnOcrTabClicked(object sender, RoutedEventArgs e)
        {
            FullTextContainer.Visibility = Visibility.Collapsed;
            ChunksContainer.Visibility = Visibility.Collapsed;
            OcrContainer.Visibility = Visibility.Visible;
            UpdateTabStyles();
        }

        private void UpdateTabStyles()
        {
            bool isFullText = FullTextContainer?.Visibility == Visibility.Visible;
            bool isChunks = ChunksContainer?.Visibility == Visibility.Visible;
            bool isOcr = OcrContainer?.Visibility == Visibility.Visible;

            var accentStyle = (Style)Application.Current.Resources["AccentButtonStyle"];
            var defaultStyle = (Style)Application.Current.Resources["DefaultButtonStyle"];

            if (FullTextTabButton != null)
                FullTextTabButton.Style = isFullText ? accentStyle : defaultStyle;

            if (ChunksTabButton != null)
                ChunksTabButton.Style = isChunks ? accentStyle : defaultStyle;

            if (OcrTabButton != null)
                OcrTabButton.Style = isOcr ? accentStyle : defaultStyle;
        }

        public static Visibility InvertBoolToVisibility(bool value)
        {
            return value ? Visibility.Collapsed : Visibility.Visible;
        }

        public static string FormatChunkTitle(int chunkIndex)
        {
            return $"Chunk #{chunkIndex + 1}";
        }

        public static string FormatTokens(int tokens)
        {
            return tokens == 1 ? "1 token" : $"{tokens:N0} tokens";
        }
    }
}
