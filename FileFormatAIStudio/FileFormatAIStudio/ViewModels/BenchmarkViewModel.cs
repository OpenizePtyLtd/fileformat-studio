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
    /// ViewModel driving the BenchmarkPage UI: category selection, document uploading,
    /// benchmark execution, real-time progress reporting, and historical results retrieval.
    /// </summary>
    public partial class BenchmarkViewModel : ObservableObject
    {
        private readonly IBenchmarkRunnerService _benchmarkRunner;
        private readonly IDocumentCategoryRegistry _categoryRegistry;
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
        private ObservableCollection<BenchmarkSessionEntity> _recentSessions = new();

        [ObservableProperty]
        private BenchmarkSessionEntity? _selectedRecentSession;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isStatusOpen;

        [ObservableProperty]
        private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;

        public bool HasSelectedFiles => SelectedFiles.Count > 0;

        public bool CanRunBenchmark => HasSelectedFiles && !IsRunning;

        public bool HasLatestResult => LatestResult != null;

        public BenchmarkViewModel(
            IBenchmarkRunnerService benchmarkRunner,
            IDocumentCategoryRegistry categoryRegistry)
        {
            _benchmarkRunner = benchmarkRunner ?? throw new ArgumentNullException(nameof(benchmarkRunner));
            _categoryRegistry = categoryRegistry ?? throw new ArgumentNullException(nameof(categoryRegistry));

            InitializeCategories();
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
                OnPropertyChanged(nameof(HasLatestResult));

                ShowStatus($"Benchmark completed! Winner: {result.OverallWinnerDisplayName ?? "N/A"}", InfoBarSeverity.Success);
                await LoadHistoryAsync();
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
                ShowStatus("Benchmark session deleted.", InfoBarSeverity.Informational);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to delete session: {ex.Message}", InfoBarSeverity.Error);
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

