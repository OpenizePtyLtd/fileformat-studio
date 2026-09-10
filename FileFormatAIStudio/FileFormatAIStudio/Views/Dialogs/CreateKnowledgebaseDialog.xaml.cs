using System;
using System.Collections.Generic;
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

            this.Loaded += OnDialogLoaded;
        }

        private async void OnDialogLoaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= OnDialogLoaded;
            await ViewModel.InitializeAsync();
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
                }
            }
            catch (Exception ex)
            {
                // In case of any picker cancellation or COM interop failure
                System.Diagnostics.Debug.WriteLine($"Error picking files: {ex.Message}");
            }
        }

        public (CreateKnowledgebaseRequest Request, List<string> FilePaths) GetResult()
        {
            return ViewModel.BuildCreateRequest();
        }
    }
}

