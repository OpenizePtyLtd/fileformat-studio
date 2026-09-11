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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

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
            !string.IsNullOrWhiteSpace(Name) &&
            SelectedProvider != null &&
            SelectedEmbeddingModel != null &&
            !string.IsNullOrWhiteSpace(SelectedEmbeddingModel.ModelId);

        public CreateKnowledgebaseViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
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
    }
}
