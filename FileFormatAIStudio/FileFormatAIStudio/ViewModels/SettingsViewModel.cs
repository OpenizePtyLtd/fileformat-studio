using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Settings;

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

        public SettingsViewModel(ISettingsService settingsService, IAIClientFactory aiClientFactory)
        {
            _settingsService = settingsService;
            _aiClientFactory = aiClientFactory;
        }

        [RelayCommand]
        public async Task LoadProvidersAsync()
        {
            var list = await _settingsService.GetProvidersAsync();
            Providers.Clear();
            foreach (var p in list)
            {
                Providers.Add(p);
            }

            if (SelectedProvider == null && Providers.Count > 0)
            {
                SelectedProvider = Providers[0];
            }
        }

        partial void OnSelectedProviderChanged(ProviderConfigEntity? value)
        {
            SelectedProviderModels.Clear();
            TestStatusMessage = string.Empty;
            if (value != null && value.Models != null)
            {
                foreach (var model in value.Models)
                {
                    SelectedProviderModels.Add(model);
                }
            }
        }

        [RelayCommand]
        public async Task SaveProviderAsync()
        {
            if (SelectedProvider == null) return;
            await _settingsService.SaveProviderAsync(SelectedProvider);
            TestStatusMessage = "Settings saved successfully!";
            IsTestSuccess = true;
        }

        [RelayCommand]
        public async Task AddProviderAsync()
        {
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
        }

        [RelayCommand]
        public async Task AddModelAsync()
        {
            if (SelectedProvider == null || string.IsNullOrWhiteSpace(NewModelId)) return;

            var model = new ModelConfigEntity
            {
                ProviderId = SelectedProvider.Id,
                ModelId = NewModelId.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(NewModelDisplayName) ? NewModelId.Trim() : NewModelDisplayName.Trim(),
                IsDefault = SelectedProviderModels.Count == 0
            };

            await _settingsService.AddModelAsync(model);
            SelectedProviderModels.Add(model);
            SelectedProvider.Models.Add(model);

            NewModelId = string.Empty;
            NewModelDisplayName = string.Empty;
        }

        [RelayCommand]
        public async Task DeleteModelAsync(ModelConfigEntity? model)
        {
            if (model == null || SelectedProvider == null) return;

            await _settingsService.DeleteModelAsync(model.Id);
            SelectedProviderModels.Remove(model);
            SelectedProvider.Models.Remove(model);
        }

        [RelayCommand]
        public async Task TestConnectionAsync()
        {
            if (SelectedProvider == null) return;

            if (SelectedProviderModels.Count == 0)
            {
                TestStatusMessage = "Please add at least one model before testing connection.";
                IsTestSuccess = false;
                return;
            }

            IsTestingConnection = true;
            TestStatusMessage = "Testing connection...";

            string testModelId = SelectedProviderModels[0].ModelId;
            var result = await _aiClientFactory.TestConnectionAsync(SelectedProvider, testModelId);

            IsTestingConnection = false;
            IsTestSuccess = result.Success;
            TestStatusMessage = result.Message;
        }
    }
}

