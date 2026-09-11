using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.Views.Dialogs
{
    public sealed partial class CreateKnowledgebaseDialog : ContentDialog
    {
        public CreateKnowledgebaseViewModel ViewModel { get; }

        public CreateKnowledgebaseDialog(CreateKnowledgebaseViewModel viewModel)
        {
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            this.InitializeComponent();

            UpdatePrimaryButtonState();
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;

            this.PrimaryButtonClick += OnPrimaryButtonClick;
            this.CloseButtonClick += OnCloseButtonClick;
            this.Loaded += OnDialogLoaded;
        }

        public static string FormatPercentage(double p) => $"{Math.Round(p):0}%";
        public static string FormatDocCount(int processed, int total) => $"{processed} of {total} document(s) processed";
        public static string FormatChunks(int count) => count == 1 ? "1 chunk indexed" : $"{count} chunks indexed";

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsValid) ||
                e.PropertyName == nameof(ViewModel.IsIndexing) ||
                e.PropertyName == nameof(ViewModel.IsCompleted) ||
                e.PropertyName == nameof(ViewModel.IsCancelled))
            {
                UpdatePrimaryButtonState();
            }
        }

        private void UpdatePrimaryButtonState()
        {
            if (ViewModel.IsCompleted || ViewModel.IsCancelled)
            {
                this.PrimaryButtonText = ViewModel.IsCancelled ? "Close" : "Done";
                this.IsPrimaryButtonEnabled = true;
                this.CloseButtonText = "";
                this.DefaultButton = ContentDialogButton.Primary;
                return;
            }

            if (ViewModel.IsIndexing)
            {
                this.PrimaryButtonText = "";
                this.IsPrimaryButtonEnabled = false;
                this.CloseButtonText = "";
                return;
            }

            this.PrimaryButtonText = "Create Knowledgebase";
            this.CloseButtonText = "Cancel";
            this.IsPrimaryButtonEnabled = ViewModel.IsValid;
        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ViewModel.IsCompleted || ViewModel.IsCancelled)
            {
                // Finished, allow dialog to close
                return;
            }

            if (ViewModel.IsIndexing)
            {
                args.Cancel = true;
                return;
            }

            // Defer closing and execute creation & ingestion
            var deferral = args.GetDeferral();
            args.Cancel = true;

            try
            {
                UpdatePrimaryButtonState();
                await ViewModel.CreateAndIngestAsync();
                UpdatePrimaryButtonState();
            }
            catch
            {
                UpdatePrimaryButtonState();
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ViewModel.IsIndexing)
            {
                args.Cancel = true;
                ViewModel.CancelIndexing();
            }
        }

        private async void OnDialogLoaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= OnDialogLoaded;
            await ViewModel.InitializeAsync();
            UpdatePrimaryButtonState();
        }

        private void OnNameTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                ViewModel.Name = tb.Text;
                UpdatePrimaryButtonState();
            }
        }

        private void OnDescriptionTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                ViewModel.Description = tb.Text;
            }
        }

        private async void OnAddFilesClicked(object sender, RoutedEventArgs e)
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

                picker.FileTypeFilter.Add(".docx");
                picker.FileTypeFilter.Add(".doc");
                picker.FileTypeFilter.Add(".xlsx");
                picker.FileTypeFilter.Add(".xls");
                picker.FileTypeFilter.Add(".pptx");
                picker.FileTypeFilter.Add(".ppt");
                picker.FileTypeFilter.Add(".pdf");
                picker.FileTypeFilter.Add(".txt");
                picker.FileTypeFilter.Add(".csv");
                picker.FileTypeFilter.Add(".md");

                var files = await picker.PickMultipleFilesAsync();
                if (files != null && files.Count > 0)
                {
                    var items = new List<SelectedFileItemViewModel>();
                    foreach (var file in files)
                    {
                        var props = await file.GetBasicPropertiesAsync();
                        items.Add(new SelectedFileItemViewModel(file.Path, (long)props.Size));
                    }
                    ViewModel.AddFiles(items);
                    UpdatePrimaryButtonState();
                }
            }
            catch (Exception ex)
            {
                // In case of any picker cancellation or COM interop failure
                System.Diagnostics.Debug.WriteLine($"Error picking files: {ex.Message}");
            }
        }

        private void OnRemoveFileClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SelectedFileItemViewModel item)
            {
                ViewModel.RemoveFile(item);
                UpdatePrimaryButtonState();
            }
        }

        private void OnClearAllClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearAllFiles();
            UpdatePrimaryButtonState();
        }

        public (CreateKnowledgebaseRequest Request, List<string> FilePaths) GetResult()
        {
            return ViewModel.BuildCreateRequest();
        }
    }
}

