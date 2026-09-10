using System;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Knowledgebase;
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

        public KnowledgebasePage()
        {
            InitializeComponent();
            var services = ((App)Application.Current).Services;
            ViewModel = services.GetRequiredService<KnowledgebaseViewModel>();
            _knowledgebaseService = services.GetRequiredService<IKnowledgebaseService>();

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
                Header = "Embedding Provider",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 12)
            };
            providerCombo.Items.Add("OpenAI");
            providerCombo.Items.Add("Ollama (Local)");
            providerCombo.SelectedIndex = 0;

            var modelCombo = new ComboBox
            {
                Header = "Embedding Model",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 12)
            };

            void UpdateModelOptions()
            {
                modelCombo.Items.Clear();
                if (providerCombo.SelectedIndex == 0)
                {
                    modelCombo.Items.Add("text-embedding-3-small (1536 dims)");
                    modelCombo.Items.Add("text-embedding-3-large (3072 dims)");
                    modelCombo.Items.Add("text-embedding-ada-002 (1536 dims)");
                }
                else
                {
                    modelCombo.Items.Add("nomic-embed-text (768 dims)");
                    modelCombo.Items.Add("bge-m3 (1024 dims)");
                    modelCombo.Items.Add("all-minilm (384 dims)");
                }
                modelCombo.SelectedIndex = 0;
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

                string provider = providerCombo.SelectedIndex == 1 ? "Ollama" : "OpenAI";
                string selectedModelStr = modelCombo.SelectedItem?.ToString() ?? "text-embedding-3-small";
                string modelId = selectedModelStr.Split(' ')[0];
                int dims = EmbeddingModelMetadata.GetKnownDimensions(modelId) ?? 1536;

                try
                {
                    var created = await _knowledgebaseService.CreateKnowledgebaseAsync(new CreateKnowledgebaseRequest(
                        Name: name,
                        Description: descBox.Text.Trim(),
                        ParserEngine: parserEngine,
                        EmbeddingProvider: provider,
                        EmbeddingModel: modelId,
                        VectorDimensions: dims));

                    await ViewModel.LoadKnowledgebasesAsync();
                    ViewModel.ShowStatus($"Knowledgebase '{created.Name}' created successfully.", InfoBarSeverity.Success);
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

