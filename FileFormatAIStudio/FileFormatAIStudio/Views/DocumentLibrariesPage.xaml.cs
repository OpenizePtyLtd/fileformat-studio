using System;
using FileFormatAIStudio.Models;
using FileFormatAIStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.Views
{
    public sealed partial class DocumentLibrariesPage : Page
    {
        public DocumentLibrariesViewModel ViewModel { get; }

        public DocumentLibrariesPage()
        {
            InitializeComponent();
            ViewModel = ((App)Application.Current).Services.GetRequiredService<DocumentLibrariesViewModel>();
            ViewModel.NavigateToPreferencesRequested += OnNavigateToPreferencesRequested;

            this.Loaded += (s, e) =>
            {
                ViewModel.RefreshAsposeLicenseState();
            };
        }

        private void OnNavigateToPreferencesRequested()
        {
            // Navigate to SettingsPage with Document Library tab active
            var settingsVm = ((App)Application.Current).Services.GetService<SettingsViewModel>();
            settingsVm?.SelectDocumentEnginesTab();
            Frame?.Navigate(typeof(SettingsPage));
        }

        private void OnGoToPreferencesClicked(object sender, RoutedEventArgs e)
        {
            OnNavigateToPreferencesRequested();
        }

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

        private void OnRemoveLicenseClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.RemoveLicense();
        }

        private async void OnOpenPackageUrlClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is DocumentLibraryInfo lib && !string.IsNullOrWhiteSpace(lib.PackageUrl))
            {
                if (Uri.TryCreate(lib.PackageUrl, UriKind.Absolute, out var uri))
                {
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }
            }
        }

        private async void OnOpenProjectUrlClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is DocumentLibraryInfo lib && !string.IsNullOrWhiteSpace(lib.ProjectUrl))
            {
                if (Uri.TryCreate(lib.ProjectUrl, UriKind.Absolute, out var uri))
                {
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }
            }
        }

        // XAML Binding Helpers
        public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
        public static Visibility InvertedBoolToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
        public static string GetInstalledVersionText(string? version) =>
            !string.IsNullOrEmpty(version) ? $"Installed v{version}" : "Ready";
    }
}

