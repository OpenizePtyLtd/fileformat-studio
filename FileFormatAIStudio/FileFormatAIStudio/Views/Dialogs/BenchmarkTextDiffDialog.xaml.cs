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
            ViewModel.SearchMatchNavigated += OnSearchMatchNavigated;
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
                if (ViewModel.HasSearchQuery)
                {
                    HighlightCurrentMatches();
                }
            }
            else
            {
                SideBySideContainer.Visibility = Visibility.Collapsed;
                UnifiedDiffContainer.Visibility = Visibility.Visible;
                SideBySideTabButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
                UnifiedDiffTabButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }
        }

        private void OnSearchMatchNavigated()
        {
            HighlightCurrentMatches();
        }

        private void HighlightCurrentMatches()
        {
            try
            {
                _isSyncingScroll = true;

                var matchA = ViewModel.CurrentMatchA;
                if (matchA != null)
                {
                    LeftTextBox.Select(matchA.Index, matchA.Length);
                    ScrollToMatch(LeftScrollViewer, LeftTextBox, matchA);
                }
                else if (!ViewModel.HasSearchQuery)
                {
                    LeftTextBox.Select(0, 0);
                }

                var matchB = ViewModel.CurrentMatchB;
                if (matchB != null)
                {
                    RightTextBox.Select(matchB.Index, matchB.Length);
                    ScrollToMatch(RightScrollViewer, RightTextBox, matchB);
                }
                else if (!ViewModel.HasSearchQuery)
                {
                    RightTextBox.Select(0, 0);
                }
            }
            catch { }
            finally
            {
                _isSyncingScroll = false;
            }
        }

        private void ScrollToMatch(ScrollViewer scrollViewer, TextBox textBox, TextSearchMatch match)
        {
            try
            {
                string text = textBox.Text;
                if (string.IsNullOrEmpty(text)) return;

                int totalLines = 1;
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\n') totalLines++;
                }

                if (totalLines > 0 && scrollViewer.ScrollableHeight > 0)
                {
                    double lineRatio = (double)match.LineIndex / Math.Max(1, totalLines);
                    double targetOffset = (lineRatio * scrollViewer.ExtentHeight) - (scrollViewer.ViewportHeight / 3.0);
                    targetOffset = Math.Clamp(targetOffset, 0, scrollViewer.ScrollableHeight);
                    scrollViewer.ChangeView(null, targetOffset, null, false);
                }
            }
            catch { }
        }

        private void OnSearchBoxKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
                bool isShift = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

                if (isShift)
                {
                    ViewModel.NavigatePrevious();
                }
                else
                {
                    ViewModel.NavigateNext();
                }
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ViewModel.SearchQuery = string.Empty;
                e.Handled = true;
            }
        }

        private void OnPreviousMatchClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigatePrevious();
        }

        private void OnNextMatchClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigateNext();
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
