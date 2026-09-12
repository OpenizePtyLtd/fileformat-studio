using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Benchmarking;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.ViewModels
{
    public class BenchmarkCategoryItem
    {
        public DocumentCategory Category { get; }
        public string DisplayName { get; }
        public string IconGlyph { get; }
        public string FormatsSummary { get; }

        public BenchmarkCategoryItem(DocumentCategory category, string displayName, string iconGlyph, string formatsSummary)
        {
            Category = category;
            DisplayName = displayName;
            IconGlyph = iconGlyph;
            FormatsSummary = formatsSummary;
        }
    }

    /// <summary>
    /// Executive summary of the overall high-scoring library across all benchmarks.
    /// </summary>
    public class BenchmarkOverallChampionSummary
    {
        public string ChampionEngineName { get; set; } = string.Empty;
        public string FormattedAvgScore { get; set; } = string.Empty;
        public int TotalSessionsEvaluated { get; set; }
        public int TotalDocumentsEvaluated { get; set; }
        public string FormattedTotalChars { get; set; } = string.Empty;
        public int CategoriesWonCount { get; set; }
        public string WinRatePercentage { get; set; } = string.Empty;
        public string HighlightBadge { get; set; } = string.Empty;
    }

    /// <summary>
    /// Summary of the high-scoring library and latest benchmark result for an individual category.
    /// </summary>
    public partial class BenchmarkCategoryWinnerSummary : ObservableObject
    {
        public DocumentCategory Category { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = string.Empty;
        public string FormatsSummary { get; set; } = string.Empty;
        public string WinningEngineName { get; set; } = string.Empty;
        public string WinningScore { get; set; } = string.Empty;
        public string WinningLatency { get; set; } = string.Empty;
        public string FormattedTotalChars { get; set; } = string.Empty;
        public string FormattedDate { get; set; } = string.Empty;
        public bool HasBenchmark { get; set; }
        public Guid? LatestSessionId { get; set; }

        [ObservableProperty]
        private bool _isSelected;
    }

    /// <summary>
    /// Represents a document selected for benchmarking.
    /// </summary>
    public partial class BenchmarkFileItemViewModel : ObservableObject
    {
        public string FilePath { get; }
        public string FileName { get; }
        public string Extension { get; }
        public DocumentCategory Category { get; }
        public long FileSizeBytes { get; }
        public string FormattedFileSize { get; }

        public BenchmarkFileItemViewModel(string filePath, DocumentCategory category)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            Extension = Path.GetExtension(filePath).ToLowerInvariant();
            Category = category;

            long size = 0;
            try
            {
                if (File.Exists(filePath))
                {
                    size = new FileInfo(filePath).Length;
                }
            }
            catch { }
            FileSizeBytes = size;

            FormattedFileSize = size switch
            {
                >= 1024 * 1024 => $"{size / (1024.0 * 1024.0):N1} MB",
                >= 1024 => $"{size / 1024.0:N0} KB",
                _ => $"{size} bytes"
            };
        }
    }

    /// <summary>
    /// Item for document selector dropdown when inspecting benchmark results.
    /// </summary>
    public class BenchmarkDocumentSelectionItem
    {
        public string DisplayName { get; }
        public string FileName { get; }
        public string Extension { get; }
        public long FileSizeBytes { get; }
        public BenchmarkDocumentResult DocumentResult { get; }

        public BenchmarkDocumentSelectionItem(BenchmarkDocumentResult documentResult)
        {
            DocumentResult = documentResult ?? throw new ArgumentNullException(nameof(documentResult));
            FileName = documentResult.FileName;
            Extension = documentResult.Extension;
            FileSizeBytes = documentResult.FileSizeBytes;

            var sizeStr = FileSizeBytes switch
            {
                >= 1024 * 1024 => $"{FileSizeBytes / (1024.0 * 1024.0):N1} MB",
                >= 1024 => $"{FileSizeBytes / 1024.0:N0} KB",
                _ => $"{FileSizeBytes} bytes"
            };
            DisplayName = $"{FileName} ({sizeStr})";
        }
    }

    /// <summary>
    /// Scorecard summary for a parser engine evaluated on a document.
    /// </summary>
    public class BenchmarkScorecardItemViewModel
    {
        public string EngineId { get; set; } = string.Empty;
        public string EngineDisplayName { get; set; } = string.Empty;
        public int Rank { get; set; } = 1;
        public string RankBadgeText { get; set; } = string.Empty;
        public bool IsWinner { get; set; }
        public double OverallScore { get; set; }
        public string FormattedScore { get; set; } = string.Empty;
        public long CharacterCount { get; set; }
        public string FormattedCharacterCount { get; set; } = string.Empty;
        public long WordCount { get; set; }
        public string FormattedWordCount { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
        public string FormattedLatency { get; set; } = string.Empty;
        public long AllocatedBytes { get; set; }
        public string FormattedMemory { get; set; } = string.Empty;
        public string CleanlinessScore { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// An individual metric cell in the comparison matrix.
    /// </summary>
    public class BenchmarkMatrixCellViewModel
    {
        public string EngineId { get; set; } = string.Empty;
        public string EngineDisplayName { get; set; } = string.Empty;
        public string FormattedValue { get; set; } = string.Empty;
        public double RawValue { get; set; }
        public bool IsBest { get; set; }
        public int Rank { get; set; }
    }

    /// <summary>
    /// A row in the comparison matrix table representing a metric across all engines.
    /// </summary>
    public class BenchmarkMatrixRowViewModel
    {
        public string MetricName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public ObservableCollection<BenchmarkMatrixCellViewModel> Cells { get; set; } = new();
    }

    /// <summary>
    /// ViewModel driving the BenchmarkPage UI: category selection, document uploading,
    /// benchmark execution, real-time progress reporting, comparison matrix, and scorecard.
    /// </summary>
    public partial class BenchmarkViewModel : ObservableObject
    {
        private readonly IBenchmarkRunnerService _benchmarkRunner;
        private readonly IDocumentCategoryRegistry _categoryRegistry;
        private readonly IBenchmarkExportService _exportService;
        private CancellationTokenSource? _runCts;

        [ObservableProperty]
        private ObservableCollection<BenchmarkCategoryItem> _categories = new();

        [ObservableProperty]
        private BenchmarkCategoryItem? _selectedCategory;

        [ObservableProperty]
        private ObservableCollection<DocumentFormatDescriptor> _availableFormats = new();

        [ObservableProperty]
        private DocumentFormatDescriptor? _selectedFormatFilter;

        [ObservableProperty]
        private ObservableCollection<BenchmarkFileItemViewModel> _selectedFiles = new();

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private double _progressPercentage;

        [ObservableProperty]
        private string _progressMessage = string.Empty;

        [ObservableProperty]
        private string _progressStage = string.Empty;

        [ObservableProperty]
        private BenchmarkSessionResult? _latestResult;

        [ObservableProperty]
        private BenchmarkSessionResult? _activeResult;

        [ObservableProperty]
        private BenchmarkScorecardItemViewModel? _winnerScorecard;

        [ObservableProperty]
        private string _winnerBannerTitle = string.Empty;

        [ObservableProperty]
        private string _winnerBannerSubtitle = string.Empty;

        [ObservableProperty]
        private ObservableCollection<BenchmarkScorecardItemViewModel> _engineScorecards = new();

        [ObservableProperty]
        private ObservableCollection<string> _matrixEngineHeaders = new();

        [ObservableProperty]
        private ObservableCollection<BenchmarkMatrixRowViewModel> _matrixRows = new();

        [ObservableProperty]
        private ObservableCollection<BenchmarkDocumentSelectionItem> _documentSelections = new();

        [ObservableProperty]
        private BenchmarkDocumentSelectionItem? _selectedDocumentView;

        [ObservableProperty]
        private ObservableCollection<BenchmarkSessionEntity> _recentSessions = new();

        [ObservableProperty]
        private BenchmarkSessionEntity? _selectedRecentSession;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isStatusOpen;

        [ObservableProperty]
        private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;

        [ObservableProperty]
        private bool _isCreateBenchmarkMode;

        public bool IsDashboardMode => !IsCreateBenchmarkMode;

        [ObservableProperty]
        private bool _hasAnyBenchmarkHistory;

        public bool HasNoBenchmarkHistory => !HasAnyBenchmarkHistory;

        [ObservableProperty]
        private BenchmarkOverallChampionSummary? _overallChampion;

        public bool HasOverallChampion => OverallChampion != null;

        [ObservableProperty]
        private ObservableCollection<BenchmarkCategoryWinnerSummary> _categoryWinners = new();

        [ObservableProperty]
        private BenchmarkCategoryWinnerSummary? _selectedCategoryWinner;

        public bool HasSelectedFiles => SelectedFiles.Count > 0;

        public bool CanRunBenchmark => HasSelectedFiles && !IsRunning;

        public bool HasLatestResult => LatestResult != null;

        public bool HasActiveResult => ActiveResult != null;

        public bool HasMultipleDocuments => DocumentSelections.Count > 1;

        public BenchmarkViewModel(
            IBenchmarkRunnerService benchmarkRunner,
            IDocumentCategoryRegistry categoryRegistry,
            IBenchmarkExportService? exportService = null)
        {
            _benchmarkRunner = benchmarkRunner ?? throw new ArgumentNullException(nameof(benchmarkRunner));
            _categoryRegistry = categoryRegistry ?? throw new ArgumentNullException(nameof(categoryRegistry));
            _exportService = exportService ?? new BenchmarkExportService();

            InitializeCategories();
            InitializeEmptyCategoryWinners();
        }

        private void InitializeCategories()
        {
            Categories = new ObservableCollection<BenchmarkCategoryItem>
            {
                new(DocumentCategory.Word, "Word Documents", "\uE8A5", ".docx, .doc, .rtf, .odt"),
                new(DocumentCategory.Excel, "Spreadsheets", "\uE80A", ".xlsx, .xls, .ods, .csv"),
                new(DocumentCategory.PowerPoint, "Presentations", "\uE8B7", ".pptx, .ppt, .odp"),
                new(DocumentCategory.Pdf, "PDF Documents", "\uEA90", ".pdf"),
                new(DocumentCategory.PlainText, "Plain & Structured Text", "\uE8C4", ".txt, .md, .json, .xml")
            };

            SelectedCategory = Categories.FirstOrDefault();
        }

        private void InitializeEmptyCategoryWinners()
        {
            var categories = new[]
            {
                (DocumentCategory.Word, "Word Documents", "\uE8A5", ".docx, .doc, .rtf, .odt"),
                (DocumentCategory.Excel, "Spreadsheets", "\uE80A", ".xlsx, .xls, .ods, .csv"),
                (DocumentCategory.PowerPoint, "Presentations", "\uE8B7", ".pptx, .ppt, .odp"),
                (DocumentCategory.Pdf, "PDF Documents", "\uEA90", ".pdf"),
                (DocumentCategory.PlainText, "Plain & Structured Text", "\uE8C4", ".txt, .md, .json, .xml")
            };

            var list = categories.Select(c => new BenchmarkCategoryWinnerSummary
            {
                Category = c.Item1,
                DisplayName = c.Item2,
                IconGlyph = c.Item3,
                FormatsSummary = c.Item4,
                WinningEngineName = "No benchmarks yet",
                WinningScore = "—",
                WinningLatency = "—",
                FormattedTotalChars = "—",
                FormattedDate = "Not yet evaluated",
                HasBenchmark = false,
                LatestSessionId = null
            }).ToList();

            CategoryWinners = new ObservableCollection<BenchmarkCategoryWinnerSummary>(list);
            SelectedCategoryWinner = CategoryWinners.FirstOrDefault();
        }

        partial void OnIsCreateBenchmarkModeChanged(bool value)
        {
            OnPropertyChanged(nameof(IsDashboardMode));
        }

        partial void OnHasAnyBenchmarkHistoryChanged(bool value)
        {
            OnPropertyChanged(nameof(HasNoBenchmarkHistory));
        }

        partial void OnOverallChampionChanged(BenchmarkOverallChampionSummary? value)
        {
            OnPropertyChanged(nameof(HasOverallChampion));
        }

        partial void OnSelectedCategoryWinnerChanged(BenchmarkCategoryWinnerSummary? value)
        {
            if (value == null) return;

            foreach (var c in CategoryWinners)
            {
                c.IsSelected = (c == value);
            }

            if (value.HasBenchmark && value.LatestSessionId.HasValue)
            {
                _ = ViewSessionByIdAsync(value.LatestSessionId.Value);
            }
            else
            {
                ActiveResult = null;
            }
        }

        [RelayCommand]
        public void ShowCreateBenchmarkView()
        {
            IsCreateBenchmarkMode = true;
        }

        [RelayCommand]
        public void CloseCreateBenchmarkView()
        {
            IsCreateBenchmarkMode = false;
        }

        [RelayCommand]
        public void SelectCategoryWinner(BenchmarkCategoryWinnerSummary? summary)
        {
            if (summary == null) return;
            SelectedCategoryWinner = summary;
        }

        partial void OnSelectedCategoryChanged(BenchmarkCategoryItem? value)
        {
            if (value == null) return;

            var formats = _categoryRegistry.GetFormatsByCategory(value.Category);
            AvailableFormats = new ObservableCollection<DocumentFormatDescriptor>(formats);
            SelectedFormatFilter = null;
        }

        partial void OnIsRunningChanged(bool value)
        {
            OnPropertyChanged(nameof(CanRunBenchmark));
        }

        partial void OnActiveResultChanged(BenchmarkSessionResult? value)
        {
            OnPropertyChanged(nameof(HasActiveResult));

            DocumentSelections.Clear();
            if (value != null && value.DocumentResults.Count > 0)
            {
                foreach (var doc in value.DocumentResults)
                {
                    DocumentSelections.Add(new BenchmarkDocumentSelectionItem(doc));
                }

                SelectedDocumentView = DocumentSelections.FirstOrDefault();
            }
            else
            {
                SelectedDocumentView = null;
                RefreshPresentationForDocument(null);
            }

            OnPropertyChanged(nameof(HasMultipleDocuments));
        }

        partial void OnSelectedDocumentViewChanged(BenchmarkDocumentSelectionItem? value)
        {
            RefreshPresentationForDocument(value?.DocumentResult);
        }

        private void RefreshPresentationForDocument(BenchmarkDocumentResult? docResult)
        {
            EngineScorecards.Clear();
            MatrixEngineHeaders.Clear();
            MatrixRows.Clear();

            if (docResult == null || docResult.EngineRuns.Count == 0)
            {
                WinnerScorecard = null;
                WinnerBannerTitle = string.Empty;
                WinnerBannerSubtitle = string.Empty;
                return;
            }

            var sortedRuns = docResult.EngineRuns.OrderBy(r => r.Rank).ToList();

            foreach (var run in sortedRuns)
            {
                string rankBadge = run.Rank switch
                {
                    1 => "🥇 Rank #1 (Winner)",
                    2 => "🥈 Rank #2",
                    3 => "🥉 Rank #3",
                    _ => $"Rank #{run.Rank}"
                };

                string latency = run.ElapsedTime.TotalMilliseconds < 1000
                    ? $"{run.ElapsedTime.TotalMilliseconds:N0} ms"
                    : $"{run.ElapsedTime.TotalSeconds:N2} s";

                string memory = run.AllocatedBytes switch
                {
                    >= 1024 * 1024 => $"{run.AllocatedBytes / (1024.0 * 1024.0):N1} MB",
                    >= 1024 => $"{run.AllocatedBytes / 1024.0:N0} KB",
                    _ => $"{run.AllocatedBytes} bytes"
                };

                var cleanMetric = run.MetricScores.FirstOrDefault(m => m.MetricId == "cleanliness_score");
                string cleanliness = cleanMetric != null ? $"{cleanMetric.RawValue:F1}%" : "100.0%";

                var scorecard = new BenchmarkScorecardItemViewModel
                {
                    EngineId = run.EngineId,
                    EngineDisplayName = run.EngineDisplayName,
                    Rank = run.Rank,
                    RankBadgeText = rankBadge,
                    IsWinner = run.Rank == 1,
                    OverallScore = run.OverallScore,
                    FormattedScore = $"{run.OverallScore:F1} / 100",
                    CharacterCount = run.CharacterCount,
                    FormattedCharacterCount = $"{run.CharacterCount:N0} chars",
                    WordCount = run.WordCount,
                    FormattedWordCount = $"{run.WordCount:N0} words",
                    ElapsedTime = run.ElapsedTime,
                    FormattedLatency = latency,
                    AllocatedBytes = run.AllocatedBytes,
                    FormattedMemory = memory,
                    CleanlinessScore = cleanliness,
                    IsSuccess = run.IsSuccess,
                    ErrorMessage = run.ErrorMessage
                };

                EngineScorecards.Add(scorecard);
                MatrixEngineHeaders.Add(run.EngineDisplayName);
            }

            // Determine winner scorecard
            WinnerScorecard = EngineScorecards.FirstOrDefault(s => s.Rank == 1) ?? EngineScorecards.FirstOrDefault();
            if (WinnerScorecard != null)
            {
                WinnerBannerTitle = $"Winner: {WinnerScorecard.EngineDisplayName} (Rank #1)";
                WinnerBannerSubtitle = $"Extracted {WinnerScorecard.FormattedCharacterCount} in {WinnerScorecard.FormattedLatency} with {WinnerScorecard.CleanlinessScore} text cleanliness • Composite Score: {WinnerScorecard.FormattedScore}";
            }

            // Build Matrix Rows
            BuildMatrixRows(sortedRuns);
        }

        private void BuildMatrixRows(IReadOnlyList<BenchmarkEngineRunResult> engines)
        {
            if (engines.Count == 0) return;

            long maxChars = engines.Max(e => e.CharacterCount);
            long maxWords = engines.Max(e => e.WordCount);
            var successfulEngines = engines.Where(e => e.IsSuccess).ToList();
            TimeSpan minLatency = successfulEngines.Count > 0 ? successfulEngines.Min(e => e.ElapsedTime) : TimeSpan.MaxValue;
            long minMemory = successfulEngines.Count > 0 ? successfulEngines.Min(e => e.AllocatedBytes) : long.MaxValue;

            // 1. Total Characters (Baseline)
            var rowChars = new BenchmarkMatrixRowViewModel
            {
                MetricName = "Total Characters Extracted",
                Description = "Primary extraction baseline (highest volume = highest score)",
                Category = "Extraction Volume"
            };
            foreach (var e in engines)
            {
                rowChars.Cells.Add(new BenchmarkMatrixCellViewModel
                {
                    EngineId = e.EngineId,
                    EngineDisplayName = e.EngineDisplayName,
                    FormattedValue = $"{e.CharacterCount:N0} chars",
                    RawValue = e.CharacterCount,
                    IsBest = e.CharacterCount == maxChars && maxChars > 0,
                    Rank = e.Rank
                });
            }
            MatrixRows.Add(rowChars);

            // 2. Content Characters (Density)
            var rowContentChars = new BenchmarkMatrixRowViewModel
            {
                MetricName = "Content Characters (Density)",
                Description = "Total non-whitespace character volume",
                Category = "Extraction Volume"
            };
            double maxContentChars = engines.Max(e => e.MetricScores.FirstOrDefault(m => m.MetricId == "content_char_count")?.RawValue ?? 0);
            foreach (var e in engines)
            {
                var m = e.MetricScores.FirstOrDefault(x => x.MetricId == "content_char_count");
                double val = m?.RawValue ?? 0;
                rowContentChars.Cells.Add(new BenchmarkMatrixCellViewModel
                {
                    EngineId = e.EngineId,
                    EngineDisplayName = e.EngineDisplayName,
                    FormattedValue = m?.FormattedValue ?? $"{val:N0} chars",
                    RawValue = val,
                    IsBest = val == maxContentChars && maxContentChars > 0,
                    Rank = e.Rank
                });
            }
            MatrixRows.Add(rowContentChars);

            // 3. Word Count
            var rowWords = new BenchmarkMatrixRowViewModel
            {
                MetricName = "Extracted Words",
                Description = "Total words detected in extracted document content",
                Category = "Extraction Volume"
            };
            foreach (var e in engines)
            {
                rowWords.Cells.Add(new BenchmarkMatrixCellViewModel
                {
                    EngineId = e.EngineId,
                    EngineDisplayName = e.EngineDisplayName,
                    FormattedValue = $"{e.WordCount:N0} words",
                    RawValue = e.WordCount,
                    IsBest = e.WordCount == maxWords && maxWords > 0,
                    Rank = e.Rank
                });
            }
            MatrixRows.Add(rowWords);

            // 4. Latency
            var rowLatency = new BenchmarkMatrixRowViewModel
            {
                MetricName = "Execution Latency",
                Description = "Elapsed parsing time (lower is better)",
                Category = "Performance"
            };
            foreach (var e in engines)
            {
                var latencyMetric = e.MetricScores.FirstOrDefault(m => m.MetricId == "latency_ms");
                string formatted = latencyMetric?.FormattedValue ?? (e.ElapsedTime.TotalMilliseconds < 1000
                    ? $"{e.ElapsedTime.TotalMilliseconds:N0} ms"
                    : $"{e.ElapsedTime.TotalSeconds:N2} s");
                rowLatency.Cells.Add(new BenchmarkMatrixCellViewModel
                {
                    EngineId = e.EngineId,
                    EngineDisplayName = e.EngineDisplayName,
                    FormattedValue = formatted,
                    RawValue = e.ElapsedTime.TotalMilliseconds,
                    IsBest = e.ElapsedTime == minLatency && e.IsSuccess,
                    Rank = e.Rank
                });
            }
            MatrixRows.Add(rowLatency);

            // 5. Memory
            var rowMemory = new BenchmarkMatrixRowViewModel
            {
                MetricName = "Memory Allocated",
                Description = "Thread memory allocated during parsing (lower is better)",
                Category = "Performance"
            };
            foreach (var e in engines)
            {
                var memMetric = e.MetricScores.FirstOrDefault(m => m.MetricId == "memory_allocated_bytes");
                string formatted = memMetric?.FormattedValue ?? (e.AllocatedBytes switch
                {
                    >= 1024 * 1024 => $"{e.AllocatedBytes / (1024.0 * 1024.0):N1} MB",
                    >= 1024 => $"{e.AllocatedBytes / 1024.0:N0} KB",
                    _ => $"{e.AllocatedBytes} bytes"
                });
                rowMemory.Cells.Add(new BenchmarkMatrixCellViewModel
                {
                    EngineId = e.EngineId,
                    EngineDisplayName = e.EngineDisplayName,
                    FormattedValue = formatted,
                    RawValue = e.AllocatedBytes,
                    IsBest = e.AllocatedBytes == minMemory && e.IsSuccess,
                    Rank = e.Rank
                });
            }
            MatrixRows.Add(rowMemory);

            // 6. Text Cleanliness
            var rowClean = new BenchmarkMatrixRowViewModel
            {
                MetricName = "Text Cleanliness Score",
                Description = "Penalizes replacement artifacts, CID codes, and unprintable characters",
                Category = "Quality"
            };
            double maxClean = engines.Max(e => e.MetricScores.FirstOrDefault(m => m.MetricId == "cleanliness_score")?.RawValue ?? 100.0);
            foreach (var e in engines)
            {
                var cleanM = e.MetricScores.FirstOrDefault(m => m.MetricId == "cleanliness_score");
                double val = cleanM?.RawValue ?? 100.0;
                rowClean.Cells.Add(new BenchmarkMatrixCellViewModel
                {
                    EngineId = e.EngineId,
                    EngineDisplayName = e.EngineDisplayName,
                    FormattedValue = $"{val:F1}%",
                    RawValue = val,
                    IsBest = val == maxClean && val >= 95.0,
                    Rank = e.Rank
                });
            }
            MatrixRows.Add(rowClean);

            // 7. Overall Composite Score
            var rowScore = new BenchmarkMatrixRowViewModel
            {
                MetricName = "Overall Composite Score",
                Description = "Weighted aggregate score across all evaluated metrics (0-100)",
                Category = "Final Result"
            };
            foreach (var e in engines)
            {
                rowScore.Cells.Add(new BenchmarkMatrixCellViewModel
                {
                    EngineId = e.EngineId,
                    EngineDisplayName = e.EngineDisplayName,
                    FormattedValue = $"{e.OverallScore:F1} / 100",
                    RawValue = e.OverallScore,
                    IsBest = e.Rank == 1,
                    Rank = e.Rank
                });
            }
            MatrixRows.Add(rowScore);

            // 8. Overall Rank
            var rowRank = new BenchmarkMatrixRowViewModel
            {
                MetricName = "Overall Rank",
                Description = "Final benchmark position",
                Category = "Final Result"
            };
            foreach (var e in engines)
            {
                string rankText = e.Rank switch
                {
                    1 => "🥇 Rank #1 (Winner)",
                    2 => "🥈 Rank #2",
                    3 => "🥉 Rank #3",
                    _ => $"Rank #{e.Rank}"
                };

                rowRank.Cells.Add(new BenchmarkMatrixCellViewModel
                {
                    EngineId = e.EngineId,
                    EngineDisplayName = e.EngineDisplayName,
                    FormattedValue = rankText,
                    RawValue = e.Rank,
                    IsBest = e.Rank == 1,
                    Rank = e.Rank
                });
            }
            MatrixRows.Add(rowRank);
        }

        public void AddFiles(IEnumerable<string> filePaths)
        {
            if (filePaths == null) return;

            bool addedAny = false;
            foreach (var path in filePaths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    continue;

                // Avoid duplicate files
                if (SelectedFiles.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var cat = _categoryRegistry.ResolveCategory(path);
                SelectedFiles.Add(new BenchmarkFileItemViewModel(path, cat));
                addedAny = true;
            }

            if (addedAny)
            {
                OnPropertyChanged(nameof(HasSelectedFiles));
                OnPropertyChanged(nameof(CanRunBenchmark));
            }
        }

        public void RemoveFile(BenchmarkFileItemViewModel file)
        {
            if (SelectedFiles.Remove(file))
            {
                OnPropertyChanged(nameof(HasSelectedFiles));
                OnPropertyChanged(nameof(CanRunBenchmark));
            }
        }

        public void ClearFiles()
        {
            SelectedFiles.Clear();
            OnPropertyChanged(nameof(HasSelectedFiles));
            OnPropertyChanged(nameof(CanRunBenchmark));
        }

        [RelayCommand]
        public async Task LoadHistoryAsync()
        {
            try
            {
                var history = await _benchmarkRunner.GetBenchmarkHistoryAsync();
                RecentSessions = new ObservableCollection<BenchmarkSessionEntity>(history);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading benchmark history: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        [RelayCommand]
        public async Task LoadDashboardAsync()
        {
            try
            {
                var history = await _benchmarkRunner.GetBenchmarkHistoryAsync();
                RecentSessions = new ObservableCollection<BenchmarkSessionEntity>(history);

                if (history == null || history.Count == 0)
                {
                    HasAnyBenchmarkHistory = false;
                    OverallChampion = null;
                    InitializeEmptyCategoryWinners();
                    ActiveResult = null;
                    return;
                }

                HasAnyBenchmarkHistory = true;

                var categoryList = new List<BenchmarkCategoryWinnerSummary>();
                var categories = new[]
                {
                    (DocumentCategory.Word, "Word Documents", "\uE8A5", ".docx, .doc, .rtf, .odt"),
                    (DocumentCategory.Excel, "Spreadsheets", "\uE80A", ".xlsx, .xls, .ods, .csv"),
                    (DocumentCategory.PowerPoint, "Presentations", "\uE8B7", ".pptx, .ppt, .odp"),
                    (DocumentCategory.Pdf, "PDF Documents", "\uEA90", ".pdf"),
                    (DocumentCategory.PlainText, "Plain & Structured Text", "\uE8C4", ".txt, .md, .json, .xml")
                };

                var allRuns = new List<BenchmarkRunResultEntity>();
                var categoryWins = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var (cat, displayName, icon, formats) in categories)
                {
                    var catSessions = history
                        .Where(s => string.Equals(s.Category, cat.ToString(), StringComparison.OrdinalIgnoreCase) ||
                                    s.Documents.Any(d => string.Equals(d.Category, cat.ToString(), StringComparison.OrdinalIgnoreCase)))
                        .OrderByDescending(s => s.CreatedAt)
                        .ToList();

                    if (catSessions.Count > 0)
                    {
                        var latestSession = catSessions.First();
                        var sessionResult = _exportService.MapEntityToResult(latestSession);

                        var winningDocEngine = sessionResult.DocumentResults
                            .Select(d => d.WinnerEngine)
                            .Where(w => w != null)
                            .GroupBy(w => w!.EngineDisplayName)
                            .OrderByDescending(g => g.Count())
                            .ThenByDescending(g => g.Average(x => x!.OverallScore))
                            .FirstOrDefault();

                        var winningRun = winningDocEngine?.FirstOrDefault();
                        string winnerName = winningRun?.EngineDisplayName ?? sessionResult.OverallWinnerDisplayName ?? "N/A";
                        double score = winningRun?.OverallScore ?? 0;
                        string latencyStr = winningRun != null
                            ? (winningRun.ElapsedTime.TotalMilliseconds < 1000
                                ? $"{winningRun.ElapsedTime.TotalMilliseconds:N0} ms"
                                : $"{winningRun.ElapsedTime.TotalSeconds:N2} s")
                            : "—";

                        long chars = winningRun?.CharacterCount ?? 0;

                        categoryList.Add(new BenchmarkCategoryWinnerSummary
                        {
                            Category = cat,
                            DisplayName = displayName,
                            IconGlyph = icon,
                            FormatsSummary = formats,
                            WinningEngineName = winnerName,
                            WinningScore = score > 0 ? $"{score:F1} / 100" : "—",
                            WinningLatency = latencyStr,
                            FormattedTotalChars = chars > 0 ? $"{chars:N0} chars" : "—",
                            FormattedDate = latestSession.FormattedDate,
                            HasBenchmark = true,
                            LatestSessionId = latestSession.Id
                        });

                        if (!string.IsNullOrEmpty(winnerName) && winnerName != "N/A")
                        {
                            categoryWins[winnerName] = categoryWins.GetValueOrDefault(winnerName, 0) + 1;
                        }
                    }
                    else
                    {
                        categoryList.Add(new BenchmarkCategoryWinnerSummary
                        {
                            Category = cat,
                            DisplayName = displayName,
                            IconGlyph = icon,
                            FormatsSummary = formats,
                            WinningEngineName = "No benchmarks yet",
                            WinningScore = "—",
                            WinningLatency = "—",
                            FormattedTotalChars = "—",
                            FormattedDate = "Not yet evaluated",
                            HasBenchmark = false,
                            LatestSessionId = null
                        });
                    }
                }

                CategoryWinners = new ObservableCollection<BenchmarkCategoryWinnerSummary>(categoryList);

                // Compute Overall Champion across all history
                foreach (var s in history)
                {
                    foreach (var d in s.Documents)
                    {
                        allRuns.AddRange(d.RunResults.Where(r => string.Equals(r.Status, "Success", StringComparison.OrdinalIgnoreCase)));
                    }
                }

                if (allRuns.Count > 0)
                {
                    var engineStats = allRuns
                        .GroupBy(r => r.EngineDisplayName)
                        .Select(g => new
                        {
                            EngineName = g.Key,
                            TotalRuns = g.Count(),
                            WinsCount = g.Count(r => r.Rank == 1),
                            AverageScore = g.Average(r => r.OverallScore),
                            TotalCharacters = g.Sum(r => r.CharacterCount),
                            CategoriesWon = categoryWins.GetValueOrDefault(g.Key, 0)
                        })
                        .OrderByDescending(s => s.WinsCount)
                        .ThenByDescending(s => s.AverageScore)
                        .ToList();

                    var champion = engineStats.FirstOrDefault();
                    if (champion != null)
                    {
                        double winRate = champion.TotalRuns > 0
                            ? ((double)champion.WinsCount / champion.TotalRuns) * 100.0
                            : 0;

                        int totalDocs = history.Sum(s => s.Documents.Count);

                        OverallChampion = new BenchmarkOverallChampionSummary
                        {
                            ChampionEngineName = champion.EngineName,
                            FormattedAvgScore = $"{champion.AverageScore:F1} / 100",
                            TotalSessionsEvaluated = history.Count,
                            TotalDocumentsEvaluated = totalDocs,
                            FormattedTotalChars = $"{champion.TotalCharacters:N0} chars",
                            CategoriesWonCount = champion.CategoriesWon,
                            WinRatePercentage = $"{winRate:F0}%",
                            HighlightBadge = "👑 OVERALL HIGH SCORER"
                        };
                    }
                }

                // Default selection: select previously selected category if available, otherwise first with benchmark, otherwise first
                var targetSelection = (SelectedCategoryWinner != null
                    ? CategoryWinners.FirstOrDefault(c => c.Category == SelectedCategoryWinner.Category)
                    : null)
                    ?? CategoryWinners.FirstOrDefault(c => c.HasBenchmark)
                    ?? CategoryWinners.FirstOrDefault();

                if (targetSelection != null)
                {
                    SelectedCategoryWinner = targetSelection;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading benchmark dashboard: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        [RelayCommand]
        public async Task RunBenchmarkAsync()
        {
            if (!HasSelectedFiles || IsRunning) return;

            IsRunning = true;
            ProgressPercentage = 0;
            ProgressMessage = "Initializing benchmark runner...";
            ProgressStage = "Initializing";
            _runCts = new CancellationTokenSource();

            var progress = new Progress<BenchmarkProgressReport>(report =>
            {
                ProgressPercentage = report.PercentComplete;
                ProgressStage = report.Stage;
                ProgressMessage = report.Message;
            });

            try
            {
                var filePaths = SelectedFiles.Select(f => f.FilePath).ToList();
                var result = await _benchmarkRunner.RunBatchBenchmarkAsync(
                    filePaths: filePaths,
                    options: new BenchmarkOptions(SaveToDatabase: true),
                    progress: progress,
                    cancellationToken: _runCts.Token);

                LatestResult = result;
                ActiveResult = result;
                OnPropertyChanged(nameof(HasLatestResult));

                ShowStatus($"Benchmark completed! Winner: {result.OverallWinnerDisplayName ?? "N/A"}", InfoBarSeverity.Success);
                IsCreateBenchmarkMode = false;
                await LoadDashboardAsync();

                if (SelectedCategory != null)
                {
                    var matching = CategoryWinners.FirstOrDefault(c => c.Category == SelectedCategory.Category);
                    if (matching != null)
                    {
                        SelectedCategoryWinner = matching;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ShowStatus("Benchmark run was cancelled.", InfoBarSeverity.Warning);
            }
            catch (Exception ex)
            {
                ShowStatus($"Benchmark execution failed: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                IsRunning = false;
                _runCts?.Dispose();
                _runCts = null;
            }
        }

        [RelayCommand]
        public void CancelBenchmark()
        {
            if (IsRunning && _runCts != null)
            {
                _runCts.Cancel();
                ProgressMessage = "Cancelling benchmark...";
            }
        }

        [RelayCommand]
        public async Task ViewSessionAsync(BenchmarkSessionEntity? session)
        {
            if (session == null) return;
            await ViewSessionByIdAsync(session.Id);
        }

        public async Task ViewSessionByIdAsync(Guid sessionId)
        {
            try
            {
                var fullSession = await _benchmarkRunner.GetBenchmarkSessionAsync(sessionId);
                if (fullSession == null)
                {
                    ShowStatus("Benchmark session not found in database.", InfoBarSeverity.Error);
                    return;
                }

                var result = _exportService.MapEntityToResult(fullSession);
                ActiveResult = result;
                ShowStatus($"Loaded benchmark results for '{result.Title}'. Winner: {result.OverallWinnerDisplayName ?? "N/A"}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load benchmark session: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        [RelayCommand]
        public async Task DeleteSessionAsync(BenchmarkSessionEntity? session)
        {
            if (session == null) return;

            try
            {
                await _benchmarkRunner.DeleteBenchmarkSessionAsync(session.Id);
                RecentSessions.Remove(session);
                if (SelectedRecentSession?.Id == session.Id)
                {
                    SelectedRecentSession = null;
                }
                if (ActiveResult?.SessionId == session.Id)
                {
                    ActiveResult = null;
                }
                ShowStatus("Benchmark session deleted.", InfoBarSeverity.Informational);
                await LoadDashboardAsync();
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to delete session: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        public string ExportCsvContent()
        {
            if (ActiveResult == null) return string.Empty;
            return _exportService.ExportToCsv(ActiveResult);
        }

        public string ExportJsonContent()
        {
            if (ActiveResult == null) return string.Empty;
            return _exportService.ExportToJson(ActiveResult);
        }

        public async Task<bool> ExportToFileAsync(string filePath, string format)
        {
            if (ActiveResult == null || string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                string content = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
                    ? ExportCsvContent()
                    : ExportJsonContent();

                var dir = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                await System.IO.File.WriteAllTextAsync(filePath, content, System.Text.Encoding.UTF8);
                ShowStatus($"Successfully exported benchmark results to {System.IO.Path.GetFileName(filePath)}", InfoBarSeverity.Success);
                return true;
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to export benchmark results: {ex.Message}", InfoBarSeverity.Error);
                return false;
            }
        }

        public void ShowStatus(string message, InfoBarSeverity severity)
        {
            StatusMessage = message;
            StatusSeverity = severity;
            IsStatusOpen = true;
        }
    }
}

