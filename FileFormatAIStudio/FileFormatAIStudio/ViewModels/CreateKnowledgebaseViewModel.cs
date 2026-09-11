using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Settings;

namespace FileFormatAIStudio.ViewModels
{
    public sealed record ParserEngineOption(string EngineId, string DisplayName);

    public sealed record EmbeddingModelOption(string ModelId, string DisplayName, int Dimensions);

    /// <summary>
    /// ViewModel for the Knowledgebase Creation Wizard dialog, managing metadata inputs,
    /// engine and embedding model selections, and local multi-file document picking.
    /// </summary>
    public partial class CreateKnowledgebaseViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IKnowledgebaseService? _knowledgebaseService;
        private System.Threading.CancellationTokenSource? _indexingCts;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        // --- Indexing Progress State ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCancelIndexing))]
        [NotifyPropertyChangedFor(nameof(ConfigViewVisibility))]
        [NotifyPropertyChangedFor(nameof(ProgressViewVisibility))]
        [NotifyPropertyChangedFor(nameof(CancelButtonVisibility))]
        [NotifyPropertyChangedFor(nameof(DoneButtonVisibility))]
        private bool _isIndexing;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsStep1Active))]
        [NotifyPropertyChangedFor(nameof(IsStep1Completed))]
        [NotifyPropertyChangedFor(nameof(IsStep2Active))]
        [NotifyPropertyChangedFor(nameof(IsStep2Completed))]
        [NotifyPropertyChangedFor(nameof(IsStep3Active))]
        [NotifyPropertyChangedFor(nameof(IsStep3Completed))]
        [NotifyPropertyChangedFor(nameof(IsStep4Active))]
        [NotifyPropertyChangedFor(nameof(IsStep4Completed))]
        private int _currentStageStep; // 0: Pending, 1: Extracting, 2: Chunking, 3: Generating Embeddings, 4: Storing Vectors

        [ObservableProperty]
        private double _indexingPercentage;

        [ObservableProperty]
        private string _indexingMessage = string.Empty;

        [ObservableProperty]
        private string _currentDocumentName = string.Empty;

        [ObservableProperty]
        private int _currentDocumentIndex;

        [ObservableProperty]
        private int _processedDocuments;

        [ObservableProperty]
        private int _totalChunksIndexed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsStep4Completed))]
        [NotifyPropertyChangedFor(nameof(CanCancelIndexing))]
        [NotifyPropertyChangedFor(nameof(ConfigViewVisibility))]
        [NotifyPropertyChangedFor(nameof(ProgressViewVisibility))]
        [NotifyPropertyChangedFor(nameof(CancelButtonVisibility))]
        [NotifyPropertyChangedFor(nameof(DoneButtonVisibility))]
        private bool _isCompleted;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanCancelIndexing))]
        [NotifyPropertyChangedFor(nameof(ConfigViewVisibility))]
        [NotifyPropertyChangedFor(nameof(ProgressViewVisibility))]
        [NotifyPropertyChangedFor(nameof(CancelButtonVisibility))]
        [NotifyPropertyChangedFor(nameof(DoneButtonVisibility))]
        private bool _isCancelled;

        [ObservableProperty]
        private string? _indexingErrorMessage;

        [ObservableProperty]
        private KnowledgebaseEntity? _createdKnowledgebase;

        public bool CanCancelIndexing => IsIndexing && !IsCancelled && !IsCompleted;

        public Microsoft.UI.Xaml.Visibility ConfigViewVisibility =>
            (IsIndexing || IsCompleted || IsCancelled) ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

        public Microsoft.UI.Xaml.Visibility ProgressViewVisibility =>
            (IsIndexing || IsCompleted || IsCancelled) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility CancelButtonVisibility =>
            CanCancelIndexing ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility DoneButtonVisibility =>
            (IsCompleted || IsCancelled) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        // Step indicators for 4-stage pipeline
        public bool IsStep1Active => CurrentStageStep == 1;
        public bool IsStep1Completed => CurrentStageStep > 1;

        public bool IsStep2Active => CurrentStageStep == 2;
        public bool IsStep2Completed => CurrentStageStep > 2;

        public bool IsStep3Active => CurrentStageStep == 3;
        public bool IsStep3Completed => CurrentStageStep > 3;

        public bool IsStep4Active => CurrentStageStep == 4 && !IsCompleted;
        public bool IsStep4Completed => CurrentStageStep == 4 && IsCompleted;

        public Microsoft.UI.Xaml.Visibility Step1ActiveVisibility => IsStep1Active ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility Step1CompletedVisibility => IsStep1Completed ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility Step2ActiveVisibility => IsStep2Active ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility Step2CompletedVisibility => IsStep2Completed ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility Step3ActiveVisibility => IsStep3Active ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility Step3CompletedVisibility => IsStep3Completed ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility Step4ActiveVisibility => IsStep4Active ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility Step4CompletedVisibility => IsStep4Completed ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public IReadOnlyList<ParserEngineOption> AvailableParserEngines { get; } = new List<ParserEngineOption>
        {
            new("Auto", "Auto (Auto-detect best engine)"),
            new("aspose", "Aspose (.NET Enterprise: Words, Cells, Slides, PDF)"),
            new("dotnet-oss", ".NET Open-Source (OpenXML, PdfPig, ExcelDataReader)"),
            new("nodejs", "Node.js Open-Source (officeparser - Experimental)")
        };

        [ObservableProperty]
        private ParserEngineOption _selectedParserEngine;

        [ObservableProperty]
        private ObservableCollection<ProviderConfigEntity> _configuredProviders = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private ProviderConfigEntity? _selectedProvider;

        [ObservableProperty]
        private ObservableCollection<EmbeddingModelOption> _availableEmbeddingModels = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private EmbeddingModelOption? _selectedEmbeddingModel;

        [ObservableProperty]
        private string _providerWarningMessage = string.Empty;

        [ObservableProperty]
        private bool _hasProviderWarning;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedFiles))]
        [NotifyPropertyChangedFor(nameof(HasNoSelectedFiles))]
        [NotifyPropertyChangedFor(nameof(TotalFilesSummary))]
        private ObservableCollection<SelectedFileItemViewModel> _selectedFiles = new();

        public bool HasSelectedFiles => SelectedFiles.Count > 0;

        public bool HasNoSelectedFiles => SelectedFiles.Count == 0;

        public string TotalFilesSummary
        {
            get
            {
                if (SelectedFiles.Count == 0)
                {
                    return "No documents selected";
                }

                long totalBytes = SelectedFiles.Sum(f => f.FileSizeBytes);
                string formattedSize = SelectedFileItemViewModel.FormatBytes(totalBytes);
                return $"{SelectedFiles.Count} document{(SelectedFiles.Count == 1 ? "" : "s")} selected ({formattedSize})";
            }
        }

        public bool IsValid =>
            !IsIndexing &&
            !string.IsNullOrWhiteSpace(Name) &&
            SelectedProvider != null &&
            SelectedEmbeddingModel != null &&
            !string.IsNullOrWhiteSpace(SelectedEmbeddingModel.ModelId);

        public CreateKnowledgebaseViewModel(ISettingsService settingsService, IKnowledgebaseService? knowledgebaseService = null)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _knowledgebaseService = knowledgebaseService;
            _selectedParserEngine = AvailableParserEngines[0];
        }

        public async Task InitializeAsync()
        {
            var providers = await _settingsService.GetProvidersAsync();
            ConfiguredProviders.Clear();
            foreach (var p in providers)
            {
                ConfiguredProviders.Add(p);
            }

            if (ConfiguredProviders.Count > 0)
            {
                SelectedProvider = ConfiguredProviders[0];
            }
            else
            {
                SelectedProvider = null;
                UpdateModelOptions();
            }
        }

        partial void OnSelectedProviderChanged(ProviderConfigEntity? value)
        {
            UpdateModelOptions();
        }

        private void UpdateModelOptions()
        {
            AvailableEmbeddingModels.Clear();
            HasProviderWarning = false;
            ProviderWarningMessage = string.Empty;

            if (SelectedProvider == null)
            {
                ProviderWarningMessage = "No AI providers configured. Please configure an embedding provider in Settings.";
                HasProviderWarning = true;
                SelectedEmbeddingModel = null;
                OnPropertyChanged(nameof(IsValid));
                return;
            }

            // Check if cloud provider has missing API key
            bool isLocal = !string.IsNullOrWhiteSpace(SelectedProvider.EndpointUrl) &&
                           (SelectedProvider.EndpointUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                            SelectedProvider.EndpointUrl.Contains("127.0.0.1"));

            if (string.IsNullOrWhiteSpace(SelectedProvider.ApiKey) && !isLocal)
            {
                ProviderWarningMessage = $"Warning: '{SelectedProvider.Name}' does not have an API key saved in Settings. Indexing may fail.";
                HasProviderWarning = true;
            }

            // Filter models registered under this provider that are embedding models
            var embeddingModels = SelectedProvider.Models?
                .Where(m => m.IsEmbeddingModel || EmbeddingModelMetadata.IsEmbeddingModel(m.ModelId))
                .ToList() ?? new();

            if (embeddingModels.Count == 0)
            {
                ProviderWarningMessage = $"No embedding models are registered under '{SelectedProvider.Name}' in Settings. Please open Settings and add an embedding model first.";
                HasProviderWarning = true;
                SelectedEmbeddingModel = null;
            }
            else
            {
                foreach (var m in embeddingModels)
                {
                    int dims = (m.Dimensions.HasValue && m.Dimensions.Value > 0)
                        ? m.Dimensions.Value
                        : 1536;

                    string displayName = $"{m.ModelId} ({dims} dims)";
                    AvailableEmbeddingModels.Add(new EmbeddingModelOption(m.ModelId, displayName, dims));
                }

                SelectedEmbeddingModel = AvailableEmbeddingModels.FirstOrDefault();
            }

            OnPropertyChanged(nameof(IsValid));
        }

        public void AddFiles(IEnumerable<SelectedFileItemViewModel> files)
        {
            if (files == null) return;

            bool addedAny = false;
            foreach (var file in files)
            {
                if (SelectedFiles.Any(f => string.Equals(f.FilePath, file.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue; // Skip duplicates
                }

                file.RemoveRequested += OnFileRemoveRequested;
                SelectedFiles.Add(file);
                addedAny = true;
            }

            if (addedAny)
            {
                OnPropertyChanged(nameof(HasSelectedFiles));
                OnPropertyChanged(nameof(HasNoSelectedFiles));
                OnPropertyChanged(nameof(TotalFilesSummary));
            }
        }

        public void RemoveFile(SelectedFileItemViewModel file)
        {
            if (file == null) return;

            file.RemoveRequested -= OnFileRemoveRequested;
            if (SelectedFiles.Remove(file))
            {
                OnPropertyChanged(nameof(HasSelectedFiles));
                OnPropertyChanged(nameof(HasNoSelectedFiles));
                OnPropertyChanged(nameof(TotalFilesSummary));
            }
        }

        [RelayCommand]
        public void ClearAllFiles()
        {
            foreach (var file in SelectedFiles)
            {
                file.RemoveRequested -= OnFileRemoveRequested;
            }
            SelectedFiles.Clear();
            OnPropertyChanged(nameof(HasSelectedFiles));
            OnPropertyChanged(nameof(HasNoSelectedFiles));
            OnPropertyChanged(nameof(TotalFilesSummary));
        }

        private void OnFileRemoveRequested(SelectedFileItemViewModel file)
        {
            RemoveFile(file);
        }

        public (CreateKnowledgebaseRequest Request, List<string> FilePaths) BuildCreateRequest()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("Cannot build request: The Knowledgebase creation form is invalid.");
            }

            var request = new CreateKnowledgebaseRequest(
                Name: Name.Trim(),
                Description: Description.Trim(),
                ParserEngine: SelectedParserEngine.EngineId,
                EmbeddingProvider: SelectedProvider!.Name,
                EmbeddingModel: SelectedEmbeddingModel!.ModelId,
                VectorDimensions: SelectedEmbeddingModel.Dimensions);

            var paths = SelectedFiles.Select(f => f.FilePath).ToList();

            return (request, paths);
        }

        [RelayCommand]
        public void CancelIndexing()
        {
            if (_indexingCts != null && !_indexingCts.IsCancellationRequested)
            {
                try
                {
                    _indexingCts.Cancel();
                    IndexingMessage = "Cancelling indexing operation...";
                }
                catch (ObjectDisposedException) { }
            }
        }

        public async Task<bool> CreateAndIngestAsync(IKnowledgebaseService? knowledgebaseService = null)
        {
            var service = knowledgebaseService ?? _knowledgebaseService
                ?? throw new InvalidOperationException("KnowledgebaseService was not provided.");

            if (!IsValid)
            {
                throw new InvalidOperationException("Cannot create knowledgebase: Invalid parameters.");
            }

            var (request, paths) = BuildCreateRequest();

            IsIndexing = true;
            IsCompleted = false;
            IsCancelled = false;
            IndexingErrorMessage = null;
            CurrentStageStep = 0;
            IndexingPercentage = 0.0;
            ProcessedDocuments = 0;
            TotalChunksIndexed = 0;
            IndexingMessage = "Creating knowledgebase...";

            foreach (var f in SelectedFiles)
            {
                f.Status = "Pending";
                f.ErrorMessage = null;
            }

            _indexingCts = new System.Threading.CancellationTokenSource();

            try
            {
                var kb = await service.CreateKnowledgebaseAsync(request, _indexingCts.Token);
                CreatedKnowledgebase = kb;

                if (paths.Count == 0)
                {
                    IsCompleted = true;
                    IndexingPercentage = 100.0;
                    CurrentStageStep = 4;
                    IndexingMessage = "Knowledgebase created successfully.";
                    return true;
                }

                var progress = new AppProgress<IndexingProgressReport>(OnIndexingProgress);
                var ingestedDocs = await service.IngestDocumentsAsync(kb.Id, paths, null, progress, _indexingCts.Token);

                int failedCount = ingestedDocs.Count(d => d.Status == "Failed");
                int indexedCount = ingestedDocs.Count(d => d.Status == "Indexed");

                IsCompleted = true;
                CurrentStageStep = 4;
                IndexingPercentage = 100.0;

                if (failedCount > 0)
                {
                    IndexingMessage = $"Completed with issues: {indexedCount} indexed, {failedCount} failed.";
                }
                else
                {
                    IndexingMessage = $"Successfully indexed all {indexedCount} document(s) ({TotalChunksIndexed} chunks).";
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                IsCancelled = true;
                IndexingMessage = "Indexing cancelled by user.";
                return false;
            }
            catch (Exception ex)
            {
                IndexingErrorMessage = ex.Message;
                IndexingMessage = $"Indexing failed: {ex.Message}";
                return false;
            }
            finally
            {
                IsIndexing = false;
            }
        }

        private void OnIndexingProgress(IndexingProgressReport report)
        {
            IndexingPercentage = report.Percentage;
            IndexingMessage = report.Message;
            CurrentDocumentName = report.CurrentDocumentName;
            CurrentDocumentIndex = report.CurrentDocumentIndex;
            ProcessedDocuments = report.ProcessedDocuments;
            TotalChunksIndexed = report.TotalChunksIndexed;

            switch (report.Stage)
            {
                case IndexingStage.Starting:
                case IndexingStage.CopyingFiles:
                    CurrentStageStep = 0;
                    break;
                case IndexingStage.Extracting:
                    CurrentStageStep = 1;
                    break;
                case IndexingStage.Chunking:
                    CurrentStageStep = 2;
                    break;
                case IndexingStage.GeneratingEmbeddings:
                    CurrentStageStep = 3;
                    break;
                case IndexingStage.StoringVectors:
                    CurrentStageStep = 4;
                    break;
                case IndexingStage.Completed:
                    CurrentStageStep = 4;
                    IsCompleted = true;
                    break;
                case IndexingStage.Cancelled:
                    IsCancelled = true;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(report.CurrentDocumentName))
            {
                var match = SelectedFiles.FirstOrDefault(f =>
                    string.Equals(f.FileName, report.CurrentDocumentName, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    switch (report.Stage)
                    {
                        case IndexingStage.Extracting:
                            match.Status = "Extracting";
                            break;
                        case IndexingStage.Chunking:
                            match.Status = "Chunking";
                            break;
                        case IndexingStage.GeneratingEmbeddings:
                            match.Status = "Embedding";
                            break;
                        case IndexingStage.StoringVectors:
                            match.Status = "Storing";
                            break;
                        case IndexingStage.DocumentCompleted:
                            match.Status = "Indexed";
                            match.ErrorMessage = null;
                            break;
                        case IndexingStage.DocumentFailed:
                            match.Status = "Failed";
                            match.ErrorMessage = report.Message;
                            break;
                    }
                }
            }
        }
    }
}
