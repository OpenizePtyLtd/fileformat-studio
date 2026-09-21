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
                await ViewModel.LoadDocumentEngineSettingsAsync();
                UpdateApiKeyBox();
            };
        }

        public static Style GetTabButtonStyle(bool isActive)
        {
            return (Style)Application.Current.Resources[isActive ? "AccentButtonStyle" : "DefaultButtonStyle"];
        }

        private void OnAiProvidersTabClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectAiProvidersTab();
        }

        private void OnDocumentEnginesTabClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectDocumentEnginesTab();
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

        public static string GetLicenseStatusGlyph(bool isLicensed) => isLicensed ? "\uE73E" : "\uE7BA";
        public static Microsoft.UI.Xaml.Media.Brush GetLicenseStatusBrush(bool isLicensed) =>
            (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[isLicensed ? "SystemFillColorSuccessBrush" : "SystemFillColorCautionBrush"];
        public static string GetLicenseHeaderStatus(bool isLicensed) =>
            isLicensed ? "Active License Applied" : "Evaluation Mode (Unlicensed)";
        public static string GetLicenseDetailText(bool isLicensed, string? path) =>
            isLicensed
                ? (!string.IsNullOrWhiteSpace(path) ? path : "Installed in local app data")
                : "Operating with evaluation watermarks and volume limits (Priority: 20)";
        public static string FormatComponentBadge(string name, bool isLicensed) =>
            $"{name}: {(isLicensed ? "Licensed" : "Eval")}";
        public static Microsoft.UI.Xaml.Media.Brush GetComponentBadgeBrush(bool isLicensed) =>
            (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[isLicensed ? "SystemFillColorSuccessBrush" : "TextFillColorTertiaryBrush"];

        private async void OnBrowseLicenseClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();

                var hwnd = App.MainWindowInstance != null
                    ? WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance)
                    : IntPtr.Zero;

                if (hwnd != IntPtr.Zero)
                {
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }

                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".lic");

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    await ViewModel.InstallLicenseAsync(file.Path);
                }
            }
            catch (Exception ex)
            {
                ViewModel.AsposeLicenseStatusSeverity = InfoBarSeverity.Error;
                ViewModel.AsposeLicenseStatusMessage = $"Failed to open file picker: {ex.Message}";
                ViewModel.IsAsposeLicenseStatusOpen = true;
            }
        }

        private async void OnRemoveLicenseClicked(object sender, RoutedEventArgs e)
        {
            await ViewModel.RemoveLicenseAsync();
        }

        private void OnViewAllLibrariesClicked(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(DocumentLibrariesPage));
        }

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

