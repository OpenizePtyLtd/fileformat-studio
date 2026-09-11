using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFormatAIStudio.Data.Entities;

namespace FileFormatAIStudio.ViewModels
{
    /// <summary>
    /// Presentation model representing an individual Knowledgebase card and its associated documents.
    /// </summary>
    public partial class KnowledgebaseItemViewModel : ObservableObject
    {
        public KnowledgebaseEntity Entity { get; }

        public Guid Id => Entity.Id;
        public string Name => Entity.Name;
        public string Description => Entity.Description;
        public string ParserEngine => Entity.ParserEngine;
        public string EmbeddingProvider => Entity.EmbeddingProvider;
        public string EmbeddingModel => Entity.EmbeddingModel;
        public int VectorDimensions => Entity.VectorDimensions;
        public DateTime CreatedAt => Entity.CreatedAt;
        public DateTime UpdatedAt => Entity.UpdatedAt;

        [ObservableProperty]
        private ObservableCollection<KnowledgebaseDocumentEntity> _documents = new();

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAddDocuments))]
        private bool _isAddingDocuments;

        // --- Indexing Progress State ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAddDocuments))]
        [NotifyPropertyChangedFor(nameof(IndexingProgressVisibility))]
        private bool _isIndexing;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsStep1Active))]
        [NotifyPropertyChangedFor(nameof(IsStep1Completed))]
        [NotifyPropertyChangedFor(nameof(IsStep2Active))]
        [NotifyPropertyChangedFor(nameof(IsStep2Completed))]
        [NotifyPropertyChangedFor(nameof(IsStep3Active))]
        [NotifyPropertyChangedFor(nameof(IsStep3Completed))]
        [NotifyPropertyChangedFor(nameof(IsStep4Active))]
        [NotifyPropertyChangedFor(nameof(IsStep4Completed))]
        private int _currentStageStep;

        [ObservableProperty]
        private double _indexingPercentage;

        [ObservableProperty]
        private string _indexingMessage = string.Empty;

        [ObservableProperty]
        private string _currentDocumentName = string.Empty;

        [ObservableProperty]
        private int _processedDocuments;

        [ObservableProperty]
        private int _totalDocuments;

        [ObservableProperty]
        private int _totalChunksIndexed;

        public System.Threading.CancellationTokenSource? IndexingCts { get; set; }

        public bool CanAddDocuments => !IsAddingDocuments && !IsIndexing;

        public Microsoft.UI.Xaml.Visibility IndexingProgressVisibility =>
            IsIndexing ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        // Step indicators for 4-stage pipeline
        public bool IsStep1Active => CurrentStageStep == 1;
        public bool IsStep1Completed => CurrentStageStep > 1;

        public bool IsStep2Active => CurrentStageStep == 2;
        public bool IsStep2Completed => CurrentStageStep > 2;

        public bool IsStep3Active => CurrentStageStep == 3;
        public bool IsStep3Completed => CurrentStageStep > 3;

        public bool IsStep4Active => CurrentStageStep == 4;
        public bool IsStep4Completed => CurrentStageStep == 4 && IndexingPercentage >= 100.0;

        public Microsoft.UI.Xaml.Visibility Step1ActiveVisibility => IsStep1Active ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility Step1CompletedVisibility => IsStep1Completed ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility Step2ActiveVisibility => IsStep2Active ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility Step2CompletedVisibility => IsStep2Completed ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility Step3ActiveVisibility => IsStep3Active ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility Step3CompletedVisibility => IsStep3Completed ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public Microsoft.UI.Xaml.Visibility Step4ActiveVisibility => IsStep4Active ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility Step4CompletedVisibility => IsStep4Completed ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        [RelayCommand]
        public void CancelIndexing()
        {
            if (IndexingCts != null && !IndexingCts.IsCancellationRequested)
            {
                try
                {
                    IndexingCts.Cancel();
                    IndexingMessage = "Cancelling indexing operation...";
                }
                catch (ObjectDisposedException) { }
            }
        }

        partial void OnIsExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(ExpandIconGlyph));
            OnPropertyChanged(nameof(ExpandedVisibility));
        }

        public string ExpandIconGlyph => IsExpanded ? "\uE70E" : "\uE70D";

        public Microsoft.UI.Xaml.Visibility DescriptionVisibility =>
            string.IsNullOrWhiteSpace(Description) ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

        public Microsoft.UI.Xaml.Visibility ExpandedVisibility =>
            IsExpanded ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public int DocumentCount => Documents.Count;

        public int ChunkCount => Documents.Sum(d => d.ChunkCount);

        public string FormattedDate => CreatedAt.ToLocalTime().ToString("MMM dd, yyyy");

        public string FormattedModelInfo => string.IsNullOrWhiteSpace(EmbeddingModel)
            ? $"{VectorDimensions}d"
            : $"{EmbeddingModel} ({VectorDimensions}d)";

        public string FormattedTotalSize
        {
            get
            {
                long totalBytes = Documents.Sum(d => d.FileSize);
                return FormatBytes(totalBytes);
            }
        }

        public KnowledgebaseItemViewModel(KnowledgebaseEntity entity)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));

            if (entity.Documents != null && entity.Documents.Count > 0)
            {
                foreach (var doc in entity.Documents)
                {
                    _documents.Add(doc);
                }
            }
        }

        [RelayCommand]
        private void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
        }

        public void RefreshCounts()
        {
            OnPropertyChanged(nameof(DocumentCount));
            OnPropertyChanged(nameof(ChunkCount));
            OnPropertyChanged(nameof(FormattedTotalSize));
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}
