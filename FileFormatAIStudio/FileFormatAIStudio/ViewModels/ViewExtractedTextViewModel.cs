using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Knowledgebase;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace FileFormatAIStudio.ViewModels
{
    /// <summary>
    /// ViewModel driving the in-app document extracted plain text and vector chunks inspection dialog.
    /// </summary>
    public partial class ViewExtractedTextViewModel : ObservableObject
    {
        private readonly IKnowledgebaseService _knowledgebaseService;

        [ObservableProperty]
        private Guid _documentId;

        [ObservableProperty]
        private string _documentName = string.Empty;

        [ObservableProperty]
        private string _fileType = string.Empty;

        [ObservableProperty]
        private long _fileSize;

        [ObservableProperty]
        private string _formattedFileSize = string.Empty;

        [ObservableProperty]
        private string _parserEngineUsed = "Auto";

        [ObservableProperty]
        private string _status = "Indexed";

        [ObservableProperty]
        private string _extractedText = string.Empty;

        [ObservableProperty]
        private int _characterCount;

        [ObservableProperty]
        private int _wordCount;

        [ObservableProperty]
        private int _lineCount;

        [ObservableProperty]
        private int _chunkCount;

        [ObservableProperty]
        private bool _isLoading = true;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        [ObservableProperty]
        private bool _isCopyFeedbackOpen;

        [ObservableProperty]
        private string _copyFeedbackMessage = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TextWrappingMode))]
        private bool _isWordWrap = true;

        public TextWrapping TextWrappingMode => IsWordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;

        public ObservableCollection<DocumentChunkEntity> Chunks { get; } = new();

        public bool HasExtractedText => !string.IsNullOrEmpty(ExtractedText);
        public bool HasChunks => Chunks.Count > 0;

        public ViewExtractedTextViewModel(IKnowledgebaseService knowledgebaseService)
        {
            _knowledgebaseService = knowledgebaseService ?? throw new ArgumentNullException(nameof(knowledgebaseService));
        }

        /// <summary>
        /// Loads document details, extracted text from SQLite, and ordered chunks.
        /// </summary>
        public async Task LoadDocumentAsync(Guid documentId, KnowledgebaseDocumentEntity? initialMeta = null, CancellationToken ct = default)
        {
            DocumentId = documentId;
            IsLoading = true;
            ErrorMessage = null;
            HasError = false;

            if (initialMeta != null)
            {
                DocumentName = initialMeta.FileName;
                FileType = initialMeta.FileType?.TrimStart('.').ToUpperInvariant() ?? "";
                FileSize = initialMeta.FileSize;
                FormattedFileSize = KnowledgebaseItemViewModel.FormatBytes(initialMeta.FileSize);
                ParserEngineUsed = FormatParser(initialMeta.ParserEngineUsed);
                Status = initialMeta.Status;
                ChunkCount = initialMeta.ChunkCount;
            }

            try
            {
                // If initial meta was not provided or partial, fetch doc entity
                var doc = await _knowledgebaseService.GetDocumentByIdAsync(documentId, ct);
                if (doc != null)
                {
                    DocumentName = doc.FileName;
                    FileType = doc.FileType?.TrimStart('.').ToUpperInvariant() ?? "";
                    FileSize = doc.FileSize;
                    FormattedFileSize = KnowledgebaseItemViewModel.FormatBytes(doc.FileSize);
                    ParserEngineUsed = FormatParser(doc.ParserEngineUsed);
                    Status = doc.Status;
                    ChunkCount = doc.ChunkCount;
                }

                // 1. Fetch full plain text (stored or reconstructed from chunks)
                string text = await _knowledgebaseService.GetDocumentExtractedTextAsync(documentId, ct);
                ExtractedText = text;
                ComputeTextStatistics(text);

                // 2. Fetch ordered chunks
                var chunksList = await _knowledgebaseService.GetDocumentChunksAsync(documentId, ct);
                Chunks.Clear();
                foreach (var chunk in chunksList)
                {
                    Chunks.Add(chunk);
                }

                if (ChunkCount == 0 && Chunks.Count > 0)
                {
                    ChunkCount = Chunks.Count;
                }

                OnPropertyChanged(nameof(HasExtractedText));
                OnPropertyChanged(nameof(HasChunks));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load document text: {ex.Message}";
                HasError = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ComputeTextStatistics(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                CharacterCount = 0;
                WordCount = 0;
                LineCount = 0;
                return;
            }

            CharacterCount = text.Length;

            // Count words based on whitespace separation
            var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            WordCount = words.Length;

            // Count lines
            int lines = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lines++;
                }
            }
            LineCount = lines;
        }

        [RelayCommand]
        public void CopyToClipboard()
        {
            try
            {
                string textToCopy = ExtractedText;
                if (string.IsNullOrWhiteSpace(textToCopy) && Chunks.Count > 0)
                {
                    textToCopy = string.Join(Environment.NewLine + Environment.NewLine, Chunks.Select(c => c.TextContent));
                }

                if (string.IsNullOrWhiteSpace(textToCopy))
                {
                    CopyFeedbackMessage = "No text available to copy.";
                    IsCopyFeedbackOpen = true;
                    return;
                }

                var package = new DataPackage();
                package.SetText(textToCopy);
                Clipboard.SetContent(package);

                CopyFeedbackMessage = $"Copied {CharacterCount:N0} characters to clipboard.";
                IsCopyFeedbackOpen = true;
            }
            catch (Exception ex)
            {
                CopyFeedbackMessage = $"Failed to copy to clipboard: {ex.Message}";
                IsCopyFeedbackOpen = true;
            }
        }

        public static string FormatParser(string? parser)
        {
            if (string.IsNullOrWhiteSpace(parser)) return "Auto";
            return parser.Trim().ToLowerInvariant() switch
            {
                "aspose" => "Aspose",
                "dotnet_oss" => ".NET OSS",
                "dotnetoss" => ".NET OSS",
                "officeparser" => "Node.js",
                "nodejs" => "Node.js",
                _ => parser
            };
        }
    }
}
