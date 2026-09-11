using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Knowledgebase;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.ViewModels
{
    /// <summary>
    /// ViewModel managing the collection of Knowledgebases, loading, deletion, and user interactions.
    /// </summary>
    public partial class KnowledgebaseViewModel : ObservableObject
    {
        private readonly IKnowledgebaseService _knowledgebaseService;

        [ObservableProperty]
        private ObservableCollection<KnowledgebaseItemViewModel> _knowledgebases = new();

        [ObservableProperty]
        private KnowledgebaseItemViewModel? _selectedKnowledgebase;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isStatusOpen;

        [ObservableProperty]
        private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;

        public bool HasKnowledgebases => Knowledgebases.Count > 0;

        public bool HasNoKnowledgebases => !IsLoading && Knowledgebases.Count == 0;

        public event Action? CreateKnowledgebaseRequested;

        public KnowledgebaseViewModel(IKnowledgebaseService knowledgebaseService)
        {
            _knowledgebaseService = knowledgebaseService ?? throw new ArgumentNullException(nameof(knowledgebaseService));
        }

        partial void OnIsLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(HasNoKnowledgebases));
        }

        [RelayCommand]
        public async Task LoadKnowledgebasesAsync()
        {
            IsLoading = true;
            try
            {
                var entities = await _knowledgebaseService.GetKnowledgebasesAsync();
                Knowledgebases.Clear();

                foreach (var entity in entities)
                {
                    Knowledgebases.Add(new KnowledgebaseItemViewModel(entity));
                }

                OnPropertyChanged(nameof(HasKnowledgebases));
                OnPropertyChanged(nameof(HasNoKnowledgebases));
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load knowledgebases: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoKnowledgebases));
            }
        }

        [RelayCommand]
        public async Task DeleteKnowledgebaseAsync(KnowledgebaseItemViewModel? item)
        {
            if (item == null) return;

            try
            {
                await _knowledgebaseService.DeleteKnowledgebaseAsync(item.Id);
                Knowledgebases.Remove(item);

                if (SelectedKnowledgebase == item)
                {
                    SelectedKnowledgebase = null;
                }

                OnPropertyChanged(nameof(HasKnowledgebases));
                OnPropertyChanged(nameof(HasNoKnowledgebases));

                ShowStatus($"Knowledgebase '{item.Name}' and its indexed vectors were deleted.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to delete knowledgebase: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        public async Task DeleteDocumentAsync(KnowledgebaseItemViewModel kb, KnowledgebaseDocumentEntity doc)
        {
            if (kb == null || doc == null) return;

            try
            {
                await _knowledgebaseService.DeleteDocumentAsync(doc.Id);
                kb.Documents.Remove(doc);
                kb.RefreshCounts();

                ShowStatus($"Document '{doc.FileName}' deleted from '{kb.Name}'.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to delete document: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        public async Task AddDocumentsAsync(KnowledgebaseItemViewModel kb, IEnumerable<string> filePaths)
        {
            if (kb == null) return;
            var fileList = filePaths?.ToList() ?? new List<string>();
            if (fileList.Count == 0) return;

            kb.IsAddingDocuments = true;
            ShowStatus($"Indexing {fileList.Count} document(s) into '{kb.Name}'...", InfoBarSeverity.Informational);

            try
            {
                var addedDocs = await _knowledgebaseService.IngestDocumentsAsync(kb.Id, fileList);
                foreach (var doc in addedDocs)
                {
                    kb.Documents.Add(doc);
                }
                kb.RefreshCounts();

                int indexedCount = addedDocs.Count(d => d.Status == "Indexed");
                int failedCount = addedDocs.Count(d => d.Status == "Failed");

                if (failedCount > 0)
                {
                    ShowStatus($"Indexed {indexedCount} document(s) into '{kb.Name}'. {failedCount} document(s) failed.", InfoBarSeverity.Warning);
                }
                else
                {
                    ShowStatus($"Successfully indexed {addedDocs.Count} document(s) into '{kb.Name}'.", InfoBarSeverity.Success);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to index documents into '{kb.Name}': {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                kb.IsAddingDocuments = false;
            }
        }

        [RelayCommand]
        public void RequestCreateKnowledgebase()
        {
            CreateKnowledgebaseRequested?.Invoke();
        }

        [RelayCommand]
        public void DismissStatus()
        {
            IsStatusOpen = false;
        }

        public void ShowStatus(string message, InfoBarSeverity severity)
        {
            StatusMessage = message;
            StatusSeverity = severity;
            IsStatusOpen = true;
        }
    }
}

