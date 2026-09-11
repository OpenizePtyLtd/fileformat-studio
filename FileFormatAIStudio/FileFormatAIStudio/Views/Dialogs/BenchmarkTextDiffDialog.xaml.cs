using System;
using Windows.ApplicationModel.DataTransfer;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.Views.Dialogs
{
    public sealed partial class BenchmarkTextDiffDialog : ContentDialog
    {
        public BenchmarkTextDiffViewModel ViewModel { get; }

        private bool _isSyncingScroll = false;

        public BenchmarkTextDiffDialog(BenchmarkDocumentResult documentResult)
        {
            this.InitializeComponent();

            ViewModel = new BenchmarkTextDiffViewModel();
            ViewModel.Initialize(documentResult);

            UpdateViewModeUI();
        }

        private void OnSideBySideTabClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.SetViewMode(true);
            UpdateViewModeUI();
        }

        private void OnUnifiedDiffTabClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.SetViewMode(false);
            UpdateViewModeUI();
        }

        private void UpdateViewModeUI()
        {
            if (ViewModel.IsSideBySideView)
            {
                SideBySideContainer.Visibility = Visibility.Visible;
                UnifiedDiffContainer.Visibility = Visibility.Collapsed;
                SideBySideTabButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                UnifiedDiffTabButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
            }
            else
            {
                SideBySideContainer.Visibility = Visibility.Collapsed;
                UnifiedDiffContainer.Visibility = Visibility.Visible;
                SideBySideTabButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
                UnifiedDiffTabButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }
        }

        private void OnLeftScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (!ViewModel.IsSyncScroll || _isSyncingScroll) return;

            try
            {
                _isSyncingScroll = true;
                RightScrollViewer.ChangeView(null, LeftScrollViewer.VerticalOffset, null, true);
            }
            catch { }
            finally
            {
                _isSyncingScroll = false;
            }
        }

        private void OnRightScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (!ViewModel.IsSyncScroll || _isSyncingScroll) return;

            try
            {
                _isSyncingScroll = true;
                LeftScrollViewer.ChangeView(null, RightScrollViewer.VerticalOffset, null, true);
            }
            catch { }
            finally
            {
                _isSyncingScroll = false;
            }
        }

        private void OnCopyDiffClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true; // Keep dialog open
            string diff = ViewModel.GenerateUnifiedDiffText();
            CopyToClipboard(diff, "Unified Diff report");
        }

        private void OnCopyLeftClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true; // Keep dialog open
            string name = ViewModel.SelectedEngineA?.EngineDisplayName ?? "Left Engine";
            CopyToClipboard(ViewModel.TextA, $"{name} extracted text");
        }

        private void OnCopyRightClicked(object sender, RoutedEventArgs e)
        {
            string name = ViewModel.SelectedEngineB?.EngineDisplayName ?? "Right Engine";
            CopyToClipboard(ViewModel.TextB, $"{name} extracted text");
        }

        private void CopyToClipboard(string text, string description)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    ViewModel.ShowFeedback("Nothing to copy — content is empty.");
                    return;
                }

                var package = new DataPackage();
                package.SetText(text);
                Clipboard.SetContent(package);

                ViewModel.ShowFeedback($"Successfully copied {description} to clipboard ({text.Length:N0} characters).");
            }
            catch (Exception ex)
            {
                ViewModel.ShowFeedback($"Failed to copy to clipboard: {ex.Message}");
            }
        }
    }
}
