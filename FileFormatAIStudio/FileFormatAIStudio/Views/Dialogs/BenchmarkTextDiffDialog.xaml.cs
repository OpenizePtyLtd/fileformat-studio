using System;
using System.Collections.Generic;
using Windows.ApplicationModel.DataTransfer;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

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
                HighlightCurrentMatches();
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

                // 1. Text Highlighters
                UpdateTextHighlighters(LeftTextBlock, ViewModel.MatchesA, ViewModel.CurrentMatchA);
                UpdateTextHighlighters(RightTextBlock, ViewModel.MatchesB, ViewModel.CurrentMatchB);

                // 2. Scrollbar Markers
                UpdateScrollMarkers(LeftMarkerCanvas, ViewModel.MatchesA, ViewModel.CurrentMatchIndexA, ViewModel.TextA);
                UpdateScrollMarkers(RightMarkerCanvas, ViewModel.MatchesB, ViewModel.CurrentMatchIndexB, ViewModel.TextB);

                // 3. Scroll active matches into view
                var matchA = ViewModel.CurrentMatchA;
                if (matchA != null)
                {
                    ScrollToMatch(LeftScrollViewer, LeftTextBlock, matchA);
                }

                var matchB = ViewModel.CurrentMatchB;
                if (matchB != null)
                {
                    ScrollToMatch(RightScrollViewer, RightTextBlock, matchB);
                }
            }
            catch { }
            finally
            {
                _isSyncingScroll = false;
            }
        }

        private void UpdateTextHighlighters(TextBlock textBlock, List<TextSearchMatch> matches, TextSearchMatch? currentMatch)
        {
            textBlock.TextHighlighters.Clear();

            if (matches == null || matches.Count == 0)
                return;

            // Yellow/Amber highlight for all occurrences
            var allMatchesHighlighter = new TextHighlighter
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(210, 255, 235, 59)), // Soft Amber Yellow
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
            };

            foreach (var match in matches)
            {
                if (currentMatch != null && match.Index == currentMatch.Index && match.Length == currentMatch.Length)
                    continue; // current active match gets distinct styling below

                allMatchesHighlighter.Ranges.Add(new TextRange { StartIndex = match.Index, Length = match.Length });
            }

            if (allMatchesHighlighter.Ranges.Count > 0)
            {
                textBlock.TextHighlighters.Add(allMatchesHighlighter);
            }

            // Bright Orange/Accent highlight for currently active match
            if (currentMatch != null)
            {
                var activeHighlighter = new TextHighlighter
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 112, 67)), // Vivid Coral Orange
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
                };
                activeHighlighter.Ranges.Add(new TextRange { StartIndex = currentMatch.Index, Length = currentMatch.Length });
                textBlock.TextHighlighters.Add(activeHighlighter);
            }
        }

        private void UpdateScrollMarkers(Canvas canvas, List<TextSearchMatch> matches, int currentMatchIndex, string text)
        {
            canvas.Children.Clear();

            if (matches == null || matches.Count == 0 || string.IsNullOrEmpty(text) || canvas.ActualHeight <= 0)
                return;

            int totalLines = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') totalLines++;
            }

            double trackHeight = canvas.ActualHeight;
            var yellowBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(220, 255, 193, 7)); // Amber Gold
            var orangeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 87, 34)); // Vivid Orange
            var borderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(100, 0, 0, 0));

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                bool isActive = (i == currentMatchIndex);

                double ratio = (double)match.LineIndex / Math.Max(1, totalLines);
                double top = ratio * (trackHeight - (isActive ? 6 : 3));
                top = Math.Clamp(top, 0, Math.Max(0, trackHeight - 6));

                var rect = new Rectangle
                {
                    Width = isActive ? 11 : 8,
                    Height = isActive ? 5 : 3,
                    RadiusX = 1.5,
                    RadiusY = 1.5,
                    Fill = isActive ? orangeBrush : yellowBrush,
                    Stroke = borderBrush,
                    StrokeThickness = 0.5
                };

                Canvas.SetLeft(rect, isActive ? 0.5 : 2);
                Canvas.SetTop(rect, top);
                canvas.Children.Add(rect);
            }
        }

        private void OnMarkerCanvasSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScrollMarkers(LeftMarkerCanvas, ViewModel.MatchesA, ViewModel.CurrentMatchIndexA, ViewModel.TextA);
            UpdateScrollMarkers(RightMarkerCanvas, ViewModel.MatchesB, ViewModel.CurrentMatchIndexB, ViewModel.TextB);
        }

        private void ScrollToMatch(ScrollViewer scrollViewer, TextBlock textBlock, TextSearchMatch match)
        {
            try
            {
                string text = textBlock.Text;
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
