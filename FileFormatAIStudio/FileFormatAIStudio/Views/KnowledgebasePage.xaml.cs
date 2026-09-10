using System;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Settings;
using FileFormatAIStudio.ViewModels;
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

            var nameBox = new TextBox
            {
                Header = "Knowledgebase Name",
                PlaceholderText = "e.g. Legal Documents, Financial Reports...",
                Margin = new Thickness(0, 0, 0, 12)
            };

            var descBox = new TextBox
            {
                Header = "Description (Optional)",
                PlaceholderText = "Brief description of the documents and topics...",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 60,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var parserCombo = new ComboBox
            {
                Header = "Document Parser Engine",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 12)
            };
            parserCombo.Items.Add("Auto (Auto-detect best engine)");
            parserCombo.Items.Add("Aspose (.NET Words, Cells, Slides, PDF)");
            parserCombo.Items.Add("DotNetOss (OpenXML, PdfPig, ExcelDataReader)");
            parserCombo.SelectedIndex = 0;

            var providerCombo = new ComboBox
            {
                Header = "Embedding Provider (Configured in Settings)",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 12)
            };

            foreach (var p in configuredProviders)
            {
                providerCombo.Items.Add(p.Name);
            }
            providerCombo.SelectedIndex = 0;

            var modelCombo = new ComboBox
            {
                Header = "Embedding Model",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEditable = false,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var providerWarningText = new TextBlock
            {
                FontSize = 11,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = Visibility.Collapsed
            };

            void UpdateModelOptions()
            {
                modelCombo.Items.Clear();
                int selectedIdx = providerCombo.SelectedIndex;
                if (selectedIdx < 0 || selectedIdx >= configuredProviders.Count) return;

                var selectedProvider = configuredProviders[selectedIdx];

                // Check if API key is missing on cloud providers
                bool isLocal = !string.IsNullOrWhiteSpace(selectedProvider.EndpointUrl) &&
                               (selectedProvider.EndpointUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                                selectedProvider.EndpointUrl.Contains("127.0.0.1"));

                if (string.IsNullOrWhiteSpace(selectedProvider.ApiKey) && !isLocal)
                {
                    providerWarningText.Text = $"Warning: '{selectedProvider.Name}' does not have an API key saved in Settings. Indexing may fail.";
                    providerWarningText.Visibility = Visibility.Visible;
                }
                else
                {
                    providerWarningText.Visibility = Visibility.Collapsed;
                }

                // Populate strictly with embedding models registered under this provider in Settings
                var embeddingModels = selectedProvider.Models?
                    .Where(m => m.IsEmbeddingModel || EmbeddingModelMetadata.IsEmbeddingModel(m.ModelId))
                    .ToList() ?? new();

                if (embeddingModels.Count == 0)
                {
                    providerWarningText.Text = $"No embedding models are registered under '{selectedProvider.Name}' in Settings. Please open Settings (gear icon in sidebar) and add an embedding model (e.g. nomic-embed-text, bge-m3, text-embedding-3-small) under this provider first.";
                    providerWarningText.Visibility = Visibility.Visible;
                    modelCombo.IsEnabled = false;
                }
                else
                {
                    modelCombo.IsEnabled = true;
                    foreach (var m in embeddingModels)
                    {
                        int dims = EmbeddingModelMetadata.GetKnownDimensions(m.ModelId) ?? 1536;
                        modelCombo.Items.Add($"{m.ModelId} ({dims} dims)");
                    }
                    modelCombo.SelectedIndex = 0;
                }
            }

            providerCombo.SelectionChanged += (s, args) => UpdateModelOptions();
            UpdateModelOptions();

            var contentStack = new StackPanel
            {
                Width = 420
            };
            contentStack.Children.Add(nameBox);
            contentStack.Children.Add(descBox);
            contentStack.Children.Add(parserCombo);
            contentStack.Children.Add(providerCombo);
            contentStack.Children.Add(providerWarningText);
            contentStack.Children.Add(modelCombo);

            var dialog = new ContentDialog
            {
                Title = "Create New Knowledgebase",
                Content = contentStack,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string name = nameBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    ViewModel.ShowStatus("Knowledgebase name cannot be empty.", InfoBarSeverity.Error);
                    return;
                }

                string parserEngine = parserCombo.SelectedIndex switch
                {
                    1 => "aspose",
                    2 => "dotnet-oss",
                    _ => "Auto"
                };

                int selectedProvIdx = providerCombo.SelectedIndex >= 0 ? providerCombo.SelectedIndex : 0;
                var selectedProvider = configuredProviders[selectedProvIdx];
                string providerName = selectedProvider.Name;

                var registeredEmbeddingModels = selectedProvider.Models?
                    .Where(m => m.IsEmbeddingModel || EmbeddingModelMetadata.IsEmbeddingModel(m.ModelId))
                    .ToList() ?? new();

                int modelIdx = modelCombo.SelectedIndex;
                string? selectedItemStr = modelCombo.SelectedItem?.ToString();
                string modelId = (modelIdx >= 0 && modelIdx < registeredEmbeddingModels.Count)
                    ? registeredEmbeddingModels[modelIdx].ModelId
                    : (!string.IsNullOrWhiteSpace(selectedItemStr)
                        ? selectedItemStr.Split(' ')[0]
                        : (registeredEmbeddingModels.Count > 0 ? registeredEmbeddingModels[0].ModelId : string.Empty));

                if (string.IsNullOrWhiteSpace(modelId))
                {
                    ViewModel.ShowStatus($"Cannot create knowledgebase: Please select a valid embedding model registered under '{providerName}'.", InfoBarSeverity.Error);
                    return;
                }

                int dims = EmbeddingModelMetadata.GetKnownDimensions(modelId) ?? 1536;

                try
                {
                    var created = await _knowledgebaseService.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest(
                        Name: name,
                        Description: descBox.Text.Trim(),
                        ParserEngine: parserEngine,
                        EmbeddingProvider: providerName,
                        EmbeddingModel: modelId,
                        VectorDimensions: dims));

                    await ViewModel.LoadKnowledgebasesAsync();
                    ViewModel.ShowStatus($"Knowledgebase '{created.Name}' created successfully with provider '{providerName}'.", InfoBarSeverity.Success);
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

