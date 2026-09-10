using System;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Settings;
using FileFormatAIStudio.ViewModels;
using FileFormatAIStudio.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.Views
{
    public sealed partial class KnowledgebasePage : Page
    {
        public KnowledgebaseViewModel ViewModel { get; }
        private readonly IKnowledgebaseService _knowledgebaseService;
        private readonly ISettingsService _settingsService;

        public KnowledgebasePage()
        {
            InitializeComponent();
            var services = ((App)Application.Current).Services;
            ViewModel = services.GetRequiredService<KnowledgebaseViewModel>();
            _knowledgebaseService = services.GetRequiredService<IKnowledgebaseService>();
            _settingsService = services.GetRequiredService<ISettingsService>();

            this.Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= OnPageLoaded;
            await ViewModel.LoadKnowledgebasesAsync();
        }

        private async void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadKnowledgebasesAsync();
        }

        private async void OnNewKnowledgebaseClicked(object sender, RoutedEventArgs e)
        {
            await ShowCreateKnowledgebaseDialogAsync();
        }

        private async Task ShowCreateKnowledgebaseDialogAsync()
        {
            var configuredProviders = await _settingsService.GetProvidersAsync();
            if (configuredProviders.Count == 0)
            {
                var noProviderDialog = new ContentDialog
                {
                    Title = "No AI Providers Configured",
                    Content = "You do not have any AI providers configured yet. Please open Settings (gear icon in sidebar) to configure an embedding provider (such as Ollama for local embeddings or OpenAI for cloud embeddings).",
                    CloseButtonText = "OK",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };
                await noProviderDialog.ShowAsync();
                return;
            }

            var services = ((App)Application.Current).Services;
            var createVm = services.GetRequiredService<CreateKnowledgebaseViewModel>();
            var dialog = new CreateKnowledgebaseDialog(createVm)
            {
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    var (request, filePaths) = dialog.GetResult();

                    var created = await _knowledgebaseService.CreateKnowledgebaseAsync(request);

                    if (filePaths != null && filePaths.Count > 0)
                    {
                        ViewModel.ShowStatus($"Knowledgebase '{created.Name}' created. Indexing {filePaths.Count} document(s)...", InfoBarSeverity.Informational);
                        await _knowledgebaseService.IngestDocumentsAsync(created.Id, filePaths);
                    }

                    await ViewModel.LoadKnowledgebasesAsync();

                    string successMsg = (filePaths != null && filePaths.Count > 0)
                        ? $"Knowledgebase '{created.Name}' created and {filePaths.Count} document(s) indexed successfully."
                        : $"Knowledgebase '{created.Name}' created successfully.";

                    ViewModel.ShowStatus(successMsg, InfoBarSeverity.Success);
                }
                catch (Exception ex)
                {
                    ViewModel.ShowStatus($"Failed to create knowledgebase: {ex.Message}", InfoBarSeverity.Error);
                }
            }
        }

        private async void OnDeleteKnowledgebaseClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is KnowledgebaseItemViewModel item)
            {
                var dialog = new ContentDialog
                {
                    Title = "Delete Knowledgebase?",
                    Content = $"Are you sure you want to delete '{item.Name}'? This will permanently delete the repository, all stored documents, and all vector embeddings.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await ViewModel.DeleteKnowledgebaseCommand.ExecuteAsync(item);
                }
            }
        }

        private async void OnDeleteDocumentClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is KnowledgebaseDocumentEntity doc)
            {
                var parentKb = ViewModel.Knowledgebases.FirstOrDefault(k => k.Documents.Contains(doc));
                if (parentKb == null) return;

                var dialog = new ContentDialog
                {
                    Title = "Delete Document?",
                    Content = $"Are you sure you want to remove '{doc.FileName}' from '{parentKb.Name}'? All its vector chunks will be deleted.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await ViewModel.DeleteDocumentAsync(parentKb, doc);
                }
            }
        }
    }
}

