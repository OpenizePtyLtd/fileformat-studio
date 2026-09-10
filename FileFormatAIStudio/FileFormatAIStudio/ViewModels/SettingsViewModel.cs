using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Settings;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IAIClientFactory _aiClientFactory;

        [ObservableProperty]
        private ObservableCollection<ProviderConfigEntity> _providers = new();

        [ObservableProperty]
        private ProviderConfigEntity? _selectedProvider;

        public bool HasSelectedProvider => SelectedProvider != null;

        public bool HasNoProviders => Providers.Count == 0;

        public bool CanAddProvider => !Providers.Any(p => p.Name.Equals("New Provider", StringComparison.OrdinalIgnoreCase));

        [ObservableProperty]
        private bool _isSavingProvider;

        public bool CanSaveProvider => !IsSavingProvider;

        partial void OnIsSavingProviderChanged(bool value)
        {
            OnPropertyChanged(nameof(CanSaveProvider));
            SaveProviderCommand.NotifyCanExecuteChanged();
        }

        [ObservableProperty]
        private string _saveStatusMessage = string.Empty;

        [ObservableProperty]
        private bool _isSaveStatusOpen;

        [ObservableProperty]
        private InfoBarSeverity _saveStatusSeverity = InfoBarSeverity.Informational;

        [ObservableProperty]
        private ObservableCollection<ModelConfigEntity> _selectedProviderModels = new();

        [ObservableProperty]
        private string _testStatusMessage = string.Empty;

        [ObservableProperty]
        private bool _isTestingConnection;

        [ObservableProperty]
        private bool _isTestSuccess;

        [ObservableProperty]
        private string _newModelId = string.Empty;

        [ObservableProperty]
        private string _newModelDisplayName = string.Empty;

        [ObservableProperty]
        private int _newModelTypeIndex;

        partial void OnNewModelIdChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && EmbeddingModelMetadata.IsEmbeddingModel(value))
            {
                NewModelTypeIndex = 1;
            }
        }

        public SettingsViewModel(ISettingsService settingsService, IAIClientFactory aiClientFactory)
        {
            _settingsService = settingsService;
            _aiClientFactory = aiClientFactory;
        }

        public void UpdateCanAddProvider()
        {
            OnPropertyChanged(nameof(CanAddProvider));
            OnPropertyChanged(nameof(HasNoProviders));
            AddProviderCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        public async Task LoadProvidersAsync()
        {
            var list = await _settingsService.GetProvidersAsync();
            var currentSelectedId = SelectedProvider?.Id;

            Providers.Clear();
            foreach (var p in list)
            {
                Providers.Add(p);
            }

            if (currentSelectedId.HasValue)
            {
                SelectedProvider = Providers.FirstOrDefault(p => p.Id == currentSelectedId.Value)
                                   ?? (Providers.Count > 0 ? Providers[0] : null);
            }
            else if (Providers.Count > 0)
            {
                SelectedProvider = Providers[0];
            }
            UpdateCanAddProvider();
        }

        partial void OnSelectedProviderChanged(ProviderConfigEntity? value)
        {
            SelectedProviderModels.Clear();
            TestStatusMessage = string.Empty;
            SaveStatusMessage = string.Empty;
            IsSaveStatusOpen = false;

            if (value != null && value.Models != null)
            {
                var uniqueModels = value.Models
                    .GroupBy(m => m.ModelId.Trim().ToLowerInvariant())
                    .Select(g => g.First());

                foreach (var model in uniqueModels)
                {
                    SelectedProviderModels.Add(model);
                }
            }
            OnPropertyChanged(nameof(HasSelectedProvider));
            UpdateCanAddProvider();
        }

        [RelayCommand(CanExecute = nameof(CanSaveProvider))]
        public async Task SaveProviderAsync()
        {
            if (SelectedProvider == null) return;

            // 1. Basic input validation
            if (string.IsNullOrWhiteSpace(SelectedProvider.Name))
            {
                SaveStatusSeverity = InfoBarSeverity.Error;
                SaveStatusMessage = "Provider Name cannot be empty.";
                IsSaveStatusOpen = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedProvider.EndpointUrl) ||
                !Uri.TryCreate(SelectedProvider.EndpointUrl.Trim(), UriKind.Absolute, out var endpointUri) ||
                (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
            {
                SaveStatusSeverity = InfoBarSeverity.Error;
                SaveStatusMessage = "Endpoint URL must be a valid HTTP or HTTPS address (e.g., https://api.openai.com/v1).";
                IsSaveStatusOpen = true;
                return;
            }

            IsSavingProvider = true;
            SaveStatusSeverity = InfoBarSeverity.Informational;
            SaveStatusMessage = "Validating endpoint connectivity and credentials...";
            IsSaveStatusOpen = true;

            try
            {
                (bool Success, string Message) validationResult;
                try
                {
                    string? firstModelId = SelectedProviderModels.FirstOrDefault()?.ModelId;
                    validationResult = await _aiClientFactory.ValidateProviderAsync(SelectedProvider, firstModelId);
                }
                catch (Exception valEx)
                {
                    validationResult = (false, valEx.Message);
                }

                await _settingsService.SaveProviderAsync(SelectedProvider);
                UpdateCanAddProvider();

                if (!validationResult.Success)
                {
                    SaveStatusSeverity = InfoBarSeverity.Warning;
                    SaveStatusMessage = $"Provider settings saved, but live validation failed: {validationResult.Message}";
                    IsSaveStatusOpen = true;
                    TestStatusMessage = validationResult.Message;
                    IsTestSuccess = false;
                }
                else
                {
                    SaveStatusSeverity = InfoBarSeverity.Success;
                    SaveStatusMessage = $"Provider validated and saved successfully! {validationResult.Message}";
                    IsSaveStatusOpen = true;
                    TestStatusMessage = validationResult.Message;
                    IsTestSuccess = true;
                }
            }
            catch (Exception ex)
            {
                SaveStatusSeverity = InfoBarSeverity.Error;
                SaveStatusMessage = $"An unexpected error occurred while saving: {ex.Message}";
                IsSaveStatusOpen = true;
                TestStatusMessage = ex.Message;
                IsTestSuccess = false;
            }
            finally
            {
                IsSavingProvider = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanAddProvider))]
        public async Task AddProviderAsync()
        {
            var existing = Providers.FirstOrDefault(p => p.Name.Equals("New Provider", StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                SelectedProvider = existing;
                UpdateCanAddProvider();
                return;
            }

            var newProvider = new ProviderConfigEntity
            {
                Name = "New Provider",
                ProviderType = "Custom",
                EndpointUrl = "http://localhost:8000/v1",
                ApiKey = string.Empty,
                IsEnabled = true
            };

            await _settingsService.SaveProviderAsync(newProvider);
            Providers.Add(newProvider);
            SelectedProvider = newProvider;
            UpdateCanAddProvider();
        }

        [RelayCommand]
        public async Task DeleteProviderAsync(ProviderConfigEntity? provider)
        {
            var target = provider ?? SelectedProvider;
            if (target == null) return;

            await _settingsService.DeleteProviderAsync(target.Id);
            Providers.Remove(target);
            if (SelectedProvider == target)
            {
                SelectedProvider = Providers.Count > 0 ? Providers[0] : null;
            }
            UpdateCanAddProvider();
        }

        [RelayCommand]
        public async Task AddModelAsync()
        {
            if (SelectedProvider == null || string.IsNullOrWhiteSpace(NewModelId)) return;

            string cleanModelId = NewModelId.Trim();

            // Prevent adding duplicate model ID under same provider
            if (SelectedProviderModels.Any(m => m.ModelId.Equals(cleanModelId, StringComparison.OrdinalIgnoreCase)))
            {
                SaveStatusSeverity = InfoBarSeverity.Warning;
                SaveStatusMessage = $"Model '{cleanModelId}' is already added to this provider.";
                IsSaveStatusOpen = true;
                return;
            }

            bool isEmbedding = NewModelTypeIndex == 1;

            var model = new ModelConfigEntity
            {
                ProviderId = SelectedProvider.Id,
                ModelId = cleanModelId,
                DisplayName = string.IsNullOrWhiteSpace(NewModelDisplayName) ? cleanModelId : NewModelDisplayName.Trim(),
                IsDefault = !isEmbedding && SelectedProviderModels.Count(m => !m.IsEmbeddingModel) == 0,
                IsEmbeddingModel = isEmbedding
            };

            await _settingsService.AddModelAsync(model);
            SelectedProviderModels.Add(model);

            if (!SelectedProvider.Models.Any(m => m.Id == model.Id || m.ModelId.Equals(cleanModelId, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedProvider.Models.Add(model);
            }

            NewModelId = string.Empty;
            NewModelDisplayName = string.Empty;
            NewModelTypeIndex = 0;
        }

        [RelayCommand]
        public async Task DeleteModelAsync(ModelConfigEntity? model)
        {
            if (model == null || SelectedProvider == null) return;

            await _settingsService.DeleteModelAsync(model.Id);

            var toRemove = SelectedProviderModels
                .Where(m => m.Id == model.Id || m.ModelId.Equals(model.ModelId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var item in toRemove)
            {
                SelectedProviderModels.Remove(item);
                SelectedProvider.Models.Remove(item);
            }
        }

        [RelayCommand]
        public async Task TestConnectionAsync()
        {
            if (SelectedProvider == null) return;

            IsTestingConnection = true;
            TestStatusMessage = "Testing connection...";

            try
            {
                if (SelectedProviderModels == null || SelectedProviderModels.Count == 0)
                {
                    var result = await _aiClientFactory.ValidateProviderAsync(SelectedProvider, null);
                    IsTestingConnection = false;
                    IsTestSuccess = result.Success;
                    TestStatusMessage = result.Message;
                    SaveStatusSeverity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
                    SaveStatusMessage = result.Message;
                    IsSaveStatusOpen = true;
                    return;
                }

                int totalCount = SelectedProviderModels.Count;
                var passedModels = new List<string>();
                var failedModels = new List<(string ModelName, string Error)>();

                for (int i = 0; i < totalCount; i++)
                {
                    var model = SelectedProviderModels[i];
                    string modelLabel = !string.IsNullOrWhiteSpace(model.DisplayName) ? model.DisplayName : model.ModelId;
                    TestStatusMessage = $"Testing model {i + 1} of {totalCount} ({modelLabel})...";

                    var (success, message) = await _aiClientFactory.ValidateProviderAsync(SelectedProvider, model.ModelId);
                    if (success)
                    {
                        passedModels.Add(modelLabel);
                    }
                    else
                    {
                        failedModels.Add((modelLabel, message));
                    }
                }

                IsTestingConnection = false;

                if (failedModels.Count == 0)
                {
                    IsTestSuccess = true;
                    string summary = totalCount == 1
                        ? $"Connection successful! Model '{passedModels[0]}' verified."
                        : $"All {totalCount} models verified successfully ({string.Join(", ", passedModels)}).";
                    TestStatusMessage = summary;
                    SaveStatusSeverity = InfoBarSeverity.Success;
                    SaveStatusMessage = summary;
                }
                else if (passedModels.Count == 0)
                {
                    IsTestSuccess = false;
                    string failureDetails = string.Join("; ", failedModels.Select(f => $"{f.ModelName}: {f.Error}"));
                    string summary = totalCount == 1
                        ? $"Model test failed: {failureDetails}"
                        : $"All {totalCount} models failed connection test: {failureDetails}";
                    TestStatusMessage = summary;
                    SaveStatusSeverity = InfoBarSeverity.Error;
                    SaveStatusMessage = summary;
                }
                else
                {
                    IsTestSuccess = false;
                    string failureDetails = string.Join("; ", failedModels.Select(f => $"{f.ModelName}: {f.Error}"));
                    string summary = $"{passedModels.Count} of {totalCount} models verified. Failed: {failureDetails}";
                    TestStatusMessage = summary;
                    SaveStatusSeverity = InfoBarSeverity.Warning;
                    SaveStatusMessage = summary;
                }

                IsSaveStatusOpen = true;
            }
            catch (Exception ex)
            {
                IsTestingConnection = false;
                IsTestSuccess = false;
                TestStatusMessage = $"Unexpected error during test: {ex.Message}";
                SaveStatusSeverity = InfoBarSeverity.Error;
                SaveStatusMessage = TestStatusMessage;
                IsSaveStatusOpen = true;
            }
        }

        [RelayCommand]
        public async Task RestoreDefaultProvidersAsync()
        {
            try
            {
                await _settingsService.RestoreDefaultProvidersAsync();
                await LoadProvidersAsync();
                SaveStatusSeverity = InfoBarSeverity.Success;
                SaveStatusMessage = "Default providers restored successfully.";
                IsSaveStatusOpen = true;
            }
            catch (Exception ex)
            {
                SaveStatusSeverity = InfoBarSeverity.Error;
                SaveStatusMessage = $"Failed to restore default providers: {ex.Message}";
                IsSaveStatusOpen = true;
            }
        }
    }
}

