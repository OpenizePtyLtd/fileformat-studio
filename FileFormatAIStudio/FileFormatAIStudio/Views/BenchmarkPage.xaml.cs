using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace FileFormatAIStudio.Views
{
    public sealed partial class BenchmarkPage : Page
    {
        public BenchmarkViewModel ViewModel { get; }

        public BenchmarkPage()
        {
            InitializeComponent();
            ViewModel = ((App)Application.Current).Services.GetRequiredService<BenchmarkViewModel>();
            DataContext = ViewModel;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadDashboardAsync();
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Drop documents to benchmark";
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.IsContentVisible = true;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var def = e.GetDeferral();
                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    var filePaths = new List<string>();
                    foreach (var item in items)
                    {
                        if (item is StorageFile file && File.Exists(file.Path))
                        {
                            filePaths.Add(file.Path);
                        }
                    }

                    if (filePaths.Count > 0)
                    {
                        ViewModel.AddFiles(filePaths);
                    }
                }
                finally
                {
                    def.Complete();
                }
            }
        }

        private async void OnBrowseFilesClicked(object sender, RoutedEventArgs e)
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

                // Add extension filters based on currently selected category or general formats
                if (ViewModel.AvailableFormats != null && ViewModel.AvailableFormats.Count > 0)
                {
                    foreach (var format in ViewModel.AvailableFormats)
                    {
                        picker.FileTypeFilter.Add(format.Extension);
                    }
                }
                else
                {
                    picker.FileTypeFilter.Add(".docx");
                    picker.FileTypeFilter.Add(".doc");
                    picker.FileTypeFilter.Add(".xlsx");
                    picker.FileTypeFilter.Add(".xls");
                    picker.FileTypeFilter.Add(".pptx");
                    picker.FileTypeFilter.Add(".ppt");
                    picker.FileTypeFilter.Add(".pdf");
                    picker.FileTypeFilter.Add(".txt");
                    picker.FileTypeFilter.Add(".csv");
                }

                var files = await picker.PickMultipleFilesAsync();
                if (files != null && files.Count > 0)
                {
                    var filePaths = files.Select(f => f.Path).ToList();
                    ViewModel.AddFiles(filePaths);
                }
            }
            catch (Exception ex)
            {
                ViewModel.ShowStatus($"Error selecting files: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        private void OnRemoveFileItemClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BenchmarkFileItemViewModel item)
            {
                ViewModel.RemoveFile(item);
            }
        }

        private void OnClearFilesClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearFiles();
        }

        private async void OnDeleteSessionClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BenchmarkSessionEntity session)
            {
                await ViewModel.DeleteSessionCommand.ExecuteAsync(session);
            }
        }

        private async void OnViewSessionClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BenchmarkSessionEntity session)
            {
                await ViewModel.ViewSessionAsync(session);
            }
        }

        private async void OnInspectDiffClicked(object sender, RoutedEventArgs e)
        {
            var currentDoc = ViewModel.SelectedDocumentView?.DocumentResult
                ?? ViewModel.ActiveResult?.DocumentResults.FirstOrDefault();

            if (currentDoc == null || currentDoc.EngineRuns.Count == 0)
            {
                ViewModel.ShowStatus("No benchmark runs available to compare for this document.", InfoBarSeverity.Warning);
                return;
            }

            var dialog = new Views.Dialogs.BenchmarkTextDiffDialog(currentDoc)
            {
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async void OnExportCsvClicked(object sender, RoutedEventArgs e)
        {
            await ExportBenchmarkAsync("csv");
        }

        private async void OnExportJsonClicked(object sender, RoutedEventArgs e)
        {
            await ExportBenchmarkAsync("json");
        }

        private async Task ExportBenchmarkAsync(string format)
        {
            if (!ViewModel.HasActiveResult)
            {
                ViewModel.ShowStatus("No benchmark results are currently loaded to export.", InfoBarSeverity.Warning);
                return;
            }

            try
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                var window = App.MainWindowInstance;
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                string cleanTitle = string.Join("_", (ViewModel.ActiveResult?.Title ?? "Benchmark").Split(Path.GetInvalidFileNameChars()));

                if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
                {
                    savePicker.FileTypeChoices.Add("CSV Spreadsheet", new List<string> { ".csv" });
                    savePicker.SuggestedFileName = $"{cleanTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                }
                else
                {
                    savePicker.FileTypeChoices.Add("JSON File", new List<string> { ".json" });
                    savePicker.SuggestedFileName = $"{cleanTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                }

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    await ViewModel.ExportToFileAsync(file.Path, format);
                }
            }
            catch (Exception ex)
            {
                ViewModel.ShowStatus($"Error during export: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }
}

