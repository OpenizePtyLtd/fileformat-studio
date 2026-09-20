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
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.ViewModels
{
    public sealed record TextSearchMatch(int Index, int Length, int LineIndex);

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
        private int _matchCountA;

        [ObservableProperty]
        private int _matchCountB;

        [ObservableProperty]
        private int _currentMatchIndexA = -1;

        [ObservableProperty]
        private int _currentMatchIndexB = -1;

        [ObservableProperty]
        private string _searchMatchSummary = string.Empty;

        [ObservableProperty]
        private bool _hasSearchQuery;

        [ObservableProperty]
        private bool _hasAnyMatches;

        public List<TextSearchMatch> MatchesA { get; } = new();
        public List<TextSearchMatch> MatchesB { get; } = new();

        public TextSearchMatch? CurrentMatchA => (MatchesA.Count > 0 && CurrentMatchIndexA >= 0 && CurrentMatchIndexA < MatchesA.Count) ? MatchesA[CurrentMatchIndexA] : null;
        public TextSearchMatch? CurrentMatchB => (MatchesB.Count > 0 && CurrentMatchIndexB >= 0 && CurrentMatchIndexB < MatchesB.Count) ? MatchesB[CurrentMatchIndexB] : null;

        public event Action? SearchMatchNavigated;

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
        public ScrollBarVisibility HorizontalScrollBarVisibilityMode => IsWordWrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        public ScrollMode HorizontalScrollModeValue => IsWordWrap ? ScrollMode.Disabled : ScrollMode.Enabled;

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
            OnPropertyChanged(nameof(HorizontalScrollBarVisibilityMode));
            OnPropertyChanged(nameof(HorizontalScrollModeValue));
        }

        partial void OnSearchQueryChanged(string value)
        {
            ApplyFilter();
            UpdateSearchMatches(resetIndex: true);
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
            UpdateSearchMatches(resetIndex: true);
        }

        public void NavigateNext()
        {
            if (!HasAnyMatches) return;

            if (MatchCountA > 0)
            {
                CurrentMatchIndexA = (CurrentMatchIndexA + 1) % MatchCountA;
            }
            if (MatchCountB > 0)
            {
                CurrentMatchIndexB = (CurrentMatchIndexB + 1) % MatchCountB;
            }

            UpdateSearchMatchSummary();
            SearchMatchNavigated?.Invoke();
        }

        public void NavigatePrevious()
        {
            if (!HasAnyMatches) return;

            if (MatchCountA > 0)
            {
                CurrentMatchIndexA = (CurrentMatchIndexA - 1 + MatchCountA) % MatchCountA;
            }
            if (MatchCountB > 0)
            {
                CurrentMatchIndexB = (CurrentMatchIndexB - 1 + MatchCountB) % MatchCountB;
            }

            UpdateSearchMatchSummary();
            SearchMatchNavigated?.Invoke();
        }

        public void UpdateSearchMatches(bool resetIndex = true)
        {
            MatchesA.Clear();
            MatchesB.Clear();

            HasSearchQuery = !string.IsNullOrWhiteSpace(SearchQuery);

            if (HasSearchQuery)
            {
                MatchesA.AddRange(FindMatches(TextA, SearchQuery));
                MatchesB.AddRange(FindMatches(TextB, SearchQuery));
            }

            MatchCountA = MatchesA.Count;
            MatchCountB = MatchesB.Count;
            HasAnyMatches = MatchCountA > 0 || MatchCountB > 0;

            if (resetIndex)
            {
                CurrentMatchIndexA = MatchCountA > 0 ? 0 : -1;
                CurrentMatchIndexB = MatchCountB > 0 ? 0 : -1;
            }
            else
            {
                if (CurrentMatchIndexA >= MatchCountA) CurrentMatchIndexA = MatchCountA - 1;
                if (CurrentMatchIndexB >= MatchCountB) CurrentMatchIndexB = MatchCountB - 1;
                if (MatchCountA > 0 && CurrentMatchIndexA < 0) CurrentMatchIndexA = 0;
                if (MatchCountB > 0 && CurrentMatchIndexB < 0) CurrentMatchIndexB = 0;
            }

            UpdateSearchMatchSummary();
            SearchMatchNavigated?.Invoke();
        }

        private void UpdateSearchMatchSummary()
        {
            if (!HasSearchQuery)
            {
                SearchMatchSummary = string.Empty;
                return;
            }

            if (MatchCountA == 0 && MatchCountB == 0)
            {
                SearchMatchSummary = "0 matches";
                return;
            }

            string posA = MatchCountA > 0 ? $"{CurrentMatchIndexA + 1}/{MatchCountA}" : "0";
            string posB = MatchCountB > 0 ? $"{CurrentMatchIndexB + 1}/{MatchCountB}" : "0";
            SearchMatchSummary = $"A: {posA} • B: {posB}";
        }

        private static List<TextSearchMatch> FindMatches(string text, string query)
        {
            var list = new List<TextSearchMatch>();
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
                return list;

            int line = 0;
            int lastLineStart = 0;
            int index = 0;
            while ((index = text.IndexOf(query, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                for (int i = lastLineStart; i < index; i++)
                {
                    if (text[i] == '\n')
                        line++;
                }
                lastLineStart = index;

                list.Add(new TextSearchMatch(index, query.Length, line));
                index += Math.Max(1, query.Length);
            }
            return list;
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
