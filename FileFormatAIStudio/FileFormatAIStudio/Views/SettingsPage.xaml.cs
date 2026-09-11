using System;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            InitializeComponent();
            ViewModel = ((App)Application.Current).Services.GetRequiredService<SettingsViewModel>();
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.SelectedProvider))
                {
                    UpdateApiKeyBox();
                }
            };
            this.Loaded += async (s, e) =>
            {
                await ViewModel.LoadProvidersAsync();
                UpdateApiKeyBox();
            };
        }

        private void UpdateApiKeyBox()
        {
            if (ApiKeyPasswordBox != null)
            {
                string currentKey = ViewModel.SelectedProvider?.ApiKey ?? string.Empty;
                if (ApiKeyPasswordBox.Password != currentKey)
                {
                    ApiKeyPasswordBox.Password = currentKey;
                }
            }
        }

        private void OnApiKeyPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedProvider != null && ApiKeyPasswordBox != null)
            {
                ViewModel.SelectedProvider.ApiKey = ApiKeyPasswordBox.Password;
            }
        }

        public static Visibility ToVisibility(object? obj) => obj != null ? Visibility.Visible : Visibility.Collapsed;
        public static Visibility ToInvertedVisibility(object? obj) => obj == null ? Visibility.Visible : Visibility.Collapsed;
        public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
        public static string FormatModelType(bool isEmbedding) => isEmbedding ? "Embedding" : "Chat (LLM)";
        public static string FormatModelBadge(bool isEmbedding, int? dimensions) =>
            isEmbedding
                ? (dimensions.HasValue && dimensions.Value > 0 ? $"Embedding ({dimensions.Value}d)" : "Embedding")
                : "Chat (LLM)";

        private void OnCloseSettingsClicked(object sender, RoutedEventArgs e)
        {
            if (Frame != null && Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                var mainVm = ((App)Application.Current).Services.GetRequiredService<MainViewModel>();
                if (mainVm.SelectedSession != null)
                {
                    Frame?.Navigate(typeof(ChatPage), mainVm.SelectedSession.Id);
                }
                else
                {
                    Frame?.Navigate(typeof(ChatPage));
                }
            }
        }

        private async void OnDeleteModelClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ModelConfigEntity model)
            {
                await ViewModel.DeleteModelCommand.ExecuteAsync(model);
            }
        }

        private async void OnDeleteProviderClicked(object sender, RoutedEventArgs e)
        {
            var provider = ViewModel.SelectedProvider;
            if (provider == null) return;

            var dialog = new Dialogs.DeleteProviderConfirmationDialog(provider, ViewModel.SelectedProviderModels)
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.DeleteProviderCommand.ExecuteAsync(provider);
            }
        }
    }
}

