using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFormatAIStudio.Services.Benchmarking;
using Microsoft.UI.Xaml;

namespace FileFormatAIStudio.ViewModels
{
    public partial class BenchmarkTextDiffViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _documentName = string.Empty;

        [ObservableProperty]
        private string _formattedFileSize = string.Empty;

        [ObservableProperty]
        private ObservableCollection<BenchmarkEngineRunResult> _availableEngines = new();

        [ObservableProperty]
        private BenchmarkEngineRunResult? _selectedEngineA;

        [ObservableProperty]
        private BenchmarkEngineRunResult? _selectedEngineB;

        [ObservableProperty]
        private string _textA = string.Empty;

        [ObservableProperty]
        private string _textB = string.Empty;

        [ObservableProperty]
        private string _headerStatsA = string.Empty;

        [ObservableProperty]
        private string _headerStatsB = string.Empty;

        [ObservableProperty]
        private string _diffSummaryText = string.Empty;

        [ObservableProperty]
        private TextDiffSummary? _summary;

        [ObservableProperty]
        private ObservableCollection<TextDiffItem> _diffItems = new();

        private List<TextDiffItem> _allDiffItems = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isSyncScroll = true;

        [ObservableProperty]
        private bool _isWordWrap = true;

        [ObservableProperty]
        private bool _isSideBySideView = true;

        [ObservableProperty]
        private bool _isCopyFeedbackOpen;

        [ObservableProperty]
        private string _copyFeedbackMessage = string.Empty;

        public TextWrapping TextWrappingMode => IsWordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;

        public bool HasDiffItems => DiffItems.Count > 0;

        public BenchmarkTextDiffViewModel()
        {
        }

        public void Initialize(BenchmarkDocumentResult documentResult)
        {
            if (documentResult == null) return;

            DocumentName = documentResult.FileName;
            FormattedFileSize = documentResult.FileSizeBytes switch
            {
                >= 1024 * 1024 => $"{documentResult.FileSizeBytes / (1024.0 * 1024.0):N1} MB",
                >= 1024 => $"{documentResult.FileSizeBytes / 1024.0:N0} KB",
                _ => $"{documentResult.FileSizeBytes} bytes"
            };

            var runs = documentResult.EngineRuns.OrderBy(r => r.Rank).ToList();
            AvailableEngines = new ObservableCollection<BenchmarkEngineRunResult>(runs);

            // Set default selections: Engine A = Rank 1 (Winner), Engine B = Rank 2 (or second engine)
            if (runs.Count > 0)
            {
                SelectedEngineA = runs[0];
            }
            if (runs.Count > 1)
            {
                SelectedEngineB = runs[1];
            }
            else if (runs.Count > 0)
            {
                SelectedEngineB = runs[0];
            }

            RecalculateDiff();
        }

        partial void OnSelectedEngineAChanged(BenchmarkEngineRunResult? value)
        {
            RecalculateDiff();
        }

        partial void OnSelectedEngineBChanged(BenchmarkEngineRunResult? value)
        {
            RecalculateDiff();
        }

        partial void OnIsWordWrapChanged(bool value)
        {
            OnPropertyChanged(nameof(TextWrappingMode));
        }

        partial void OnSearchQueryChanged(string value)
        {
            ApplyFilter();
        }

        public void SetViewMode(bool sideBySide)
        {
            IsSideBySideView = sideBySide;
        }

        private void RecalculateDiff()
        {
            TextA = SelectedEngineA?.ExtractedText ?? string.Empty;
            TextB = SelectedEngineB?.ExtractedText ?? string.Empty;

            string engineAName = SelectedEngineA?.EngineDisplayName ?? "Engine A";
            string engineBName = SelectedEngineB?.EngineDisplayName ?? "Engine B";

            long charsA = SelectedEngineA?.CharacterCount ?? TextA.Length;
            long wordsA = SelectedEngineA?.WordCount ?? CountWords(TextA);
            string latencyA = SelectedEngineA != null ? $"{SelectedEngineA.ElapsedTime.TotalMilliseconds:N0} ms" : "N/A";

            long charsB = SelectedEngineB?.CharacterCount ?? TextB.Length;
            long wordsB = SelectedEngineB?.WordCount ?? CountWords(TextB);
            string latencyB = SelectedEngineB != null ? $"{SelectedEngineB.ElapsedTime.TotalMilliseconds:N0} ms" : "N/A";

            HeaderStatsA = $"{charsA:N0} chars • {wordsA:N0} words • Latency: {latencyA}";
            HeaderStatsB = $"{charsB:N0} chars • {wordsB:N0} words • Latency: {latencyB}";

            var (diffs, summary) = TextDiffEngine.Compare(TextA, TextB);
            Summary = summary;
            _allDiffItems = diffs;

            long charDiff = charsA - charsB;
            if (charDiff > 0)
            {
                double pct = charsB > 0 ? ((double)charDiff / charsB) * 100.0 : 100.0;
                DiffSummaryText = $"{engineAName} extracted +{charDiff:N0} more characters (+{pct:F1}%) than {engineBName} ({summary.UniqueLinesA} unique lines in A, {summary.UniqueLinesB} unique lines in B).";
            }
            else if (charDiff < 0)
            {
                double pct = charsA > 0 ? ((double)-charDiff / charsA) * 100.0 : 100.0;
                DiffSummaryText = $"{engineBName} extracted +{-charDiff:N0} more characters (+{pct:F1}%) than {engineAName} ({summary.UniqueLinesB} unique lines in B, {summary.UniqueLinesA} unique lines in A).";
            }
            else
            {
                DiffSummaryText = $"Both engines extracted an identical volume of {charsA:N0} characters.";
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                DiffItems = new ObservableCollection<TextDiffItem>(_allDiffItems);
            }
            else
            {
                var filtered = _allDiffItems
                    .Where(d => d.Content.IndexOf(SearchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                DiffItems = new ObservableCollection<TextDiffItem>(filtered);
            }

            OnPropertyChanged(nameof(HasDiffItems));
        }

                private static long CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public string GenerateUnifiedDiffText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- {SelectedEngineA?.EngineDisplayName ?? "Engine A"}");
            sb.AppendLine($"+++ {SelectedEngineB?.EngineDisplayName ?? "Engine B"}");
            sb.AppendLine($"@@ {DocumentName} @@");

            foreach (var item in _allDiffItems)
            {
                sb.AppendLine($"{item.Prefix}{item.Content}");
            }

            return sb.ToString();
        }

        public void ShowFeedback(string message)
        {
            CopyFeedbackMessage = message;
            IsCopyFeedbackOpen = true;
        }
    }
}
