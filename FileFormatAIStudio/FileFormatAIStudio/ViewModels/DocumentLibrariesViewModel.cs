using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFormatAIStudio.Models;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.ViewModels
{
    public partial class DocumentLibrariesViewModel : ObservableObject
    {
        private readonly IDocumentLibraryCatalogService _catalogService;
        private readonly IAsposeLicenseService? _licenseService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoLibraries))]
        private ObservableCollection<DocumentLibraryInfo> _filteredLibraries = new();

        public bool HasNoLibraries => FilteredLibraries.Count == 0;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private int _selectedCategoryFilterIndex; // 0: All, 1: Word, 2: Excel, 3: PowerPoint, 4: PDF, 5: PlainText

        [ObservableProperty]
        private int _selectedLicenseFilterIndex; // 0: All, 1: Open Source, 2: Commercial / Paid

        [ObservableProperty]
        private int _selectedSortIndex; // 0: Downloads (Highest first), 1: Recently Updated, 2: Market Longevity, 3: Name (A-Z)

        [ObservableProperty]
        private bool _isRefreshingStats;

        [ObservableProperty]
        private string _lastRefreshedStatus = string.Empty;

        [ObservableProperty]
        private bool _isRefreshStatusOpen;

        [ObservableProperty]
        private InfoBarSeverity _refreshStatusSeverity = InfoBarSeverity.Informational;

        // Metric summaries
        [ObservableProperty]
        private int _totalLibrariesCount;

        [ObservableProperty]
        private string _totalCombinedDownloadsFormatted = string.Empty;

        [ObservableProperty]
        private int _openSourceLibrariesCount;

        [ObservableProperty]
        private int _commercialLibrariesCount;

        // Aspose Licensing
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRemoveAsposeLicense))]
        [NotifyPropertyChangedFor(nameof(AsposeLicenseHeaderStatus))]
        [NotifyPropertyChangedFor(nameof(AsposeLicenseGlyph))]
        [NotifyPropertyChangedFor(nameof(AsposeLicenseDetailText))]
        private bool _isAsposeLicensed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AsposeLicenseDetailText))]
        private string? _asposeLicensePath;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WordsBadgeText))]
        private bool _isWordsLicensed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CellsBadgeText))]
        private bool _isCellsLicensed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SlidesBadgeText))]
        private bool _isSlidesLicensed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PdfBadgeText))]
        private bool _isPdfLicensed;

        [ObservableProperty]
        private string _asposeLicenseStatusMessage = string.Empty;

        [ObservableProperty]
        private bool _isAsposeLicenseStatusOpen;

        [ObservableProperty]
        private InfoBarSeverity _asposeLicenseStatusSeverity = InfoBarSeverity.Informational;

        public bool CanRemoveAsposeLicense => IsAsposeLicensed;

        public string AsposeLicenseHeaderStatus => IsAsposeLicensed ? "Active License Applied" : "Evaluation Mode (Unlicensed)";
        public string AsposeLicenseGlyph => IsAsposeLicensed ? "\uE73E" : "\uE7BA";
        public string AsposeLicenseDetailText => IsAsposeLicensed
            ? (!string.IsNullOrWhiteSpace(AsposeLicensePath) ? AsposeLicensePath : "Installed in local app data")
            : "Operating with evaluation watermarks and volume limits (Priority: 20)";

        public string WordsBadgeText => $"Words: {(IsWordsLicensed ? "Licensed" : "Eval")}";
        public string CellsBadgeText => $"Cells: {(IsCellsLicensed ? "Licensed" : "Eval")}";
        public string SlidesBadgeText => $"Slides: {(IsSlidesLicensed ? "Licensed" : "Eval")}";
        public string PdfBadgeText => $"PDF: {(IsPdfLicensed ? "Licensed" : "Eval")}";

        public event Action? NavigateToPreferencesRequested;

        public DocumentLibrariesViewModel(
            IDocumentLibraryCatalogService catalogService,
            IAsposeLicenseService? licenseService = null)
        {
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _licenseService = licenseService;

            _catalogService.CatalogUpdated += (s, e) =>
            {
                App.MainWindowInstance?.DispatcherQueue.TryEnqueue(() =>
                {
                    ApplyFiltersAndSort();
                    RefreshAsposeLicenseState();
                });
            };

            RefreshAsposeLicenseState();
            ApplyFiltersAndSort();
        }

        partial void OnSearchQueryChanged(string value) => ApplyFiltersAndSort();
        partial void OnSelectedCategoryFilterIndexChanged(int value) => ApplyFiltersAndSort();
        partial void OnSelectedLicenseFilterIndexChanged(int value) => ApplyFiltersAndSort();
        partial void OnSelectedSortIndexChanged(int value) => ApplyFiltersAndSort();

        public void RequestNavigateToPreferences()
        {
            NavigateToPreferencesRequested?.Invoke();
        }

        public void RefreshAsposeLicenseState()
        {
            if (_licenseService == null)
            {
                IsAsposeLicensed = false;
                AsposeLicensePath = null;
                IsWordsLicensed = false;
                IsCellsLicensed = false;
                IsSlidesLicensed = false;
                IsPdfLicensed = false;
                return;
            }

            IsAsposeLicensed = _licenseService.IsLicensed;
            AsposeLicensePath = _licenseService.ActiveLicensePath;
            IsWordsLicensed = _licenseService.IsWordsLicensed;
            IsCellsLicensed = _licenseService.IsCellsLicensed;
            IsSlidesLicensed = _licenseService.IsSlidesLicensed;
            IsPdfLicensed = _licenseService.IsPdfLicensed;
        }

        [RelayCommand]
        public async Task InstallLicenseAsync(string sourceFilePath)
        {
            if (_licenseService == null) return;

            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                AsposeLicenseStatusSeverity = InfoBarSeverity.Error;
                AsposeLicenseStatusMessage = "Selected license file could not be found or read.";
                IsAsposeLicenseStatusOpen = true;
                return;
            }

            bool success = _licenseService.InstallLicense(sourceFilePath);
            RefreshAsposeLicenseState();

            if (success)
            {
                var licensedProducts = new List<string>();
                if (IsWordsLicensed) licensedProducts.Add("Words");
                if (IsCellsLicensed) licensedProducts.Add("Cells");
                if (IsSlidesLicensed) licensedProducts.Add("Slides");
                if (IsPdfLicensed) licensedProducts.Add("PDF");

                string productsSummary = licensedProducts.Count > 0 ? string.Join(", ", licensedProducts) : "Core";
                AsposeLicenseStatusSeverity = InfoBarSeverity.Success;
                AsposeLicenseStatusMessage = $"Aspose commercial license activated successfully for: {productsSummary}. Engine priorities elevated to 100.";
                IsAsposeLicenseStatusOpen = true;
            }
            else
            {
                AsposeLicenseStatusSeverity = InfoBarSeverity.Warning;
                AsposeLicenseStatusMessage = "License file was copied, but none of the integrated Aspose products accepted it. Please verify your license format and expiry.";
                IsAsposeLicenseStatusOpen = true;
            }

            ApplyFiltersAndSort();
        }

        [RelayCommand]
        public void RemoveLicense()
        {
            if (_licenseService == null) return;

            _licenseService.RemoveLicense();
            RefreshAsposeLicenseState();

            AsposeLicenseStatusSeverity = InfoBarSeverity.Informational;
            AsposeLicenseStatusMessage = "Aspose license removed. Engines reverted to evaluation mode (watermarks & limits active; Priority: 20).";
            IsAsposeLicenseStatusOpen = true;

            ApplyFiltersAndSort();
        }

        [RelayCommand]
        public async Task RefreshLiveStatsAsync()
        {
            if (IsRefreshingStats) return;

            IsRefreshingStats = true;
            RefreshStatusSeverity = InfoBarSeverity.Informational;
            LastRefreshedStatus = "Querying live download counts and release history from NuGet and npm APIs...";
            IsRefreshStatusOpen = true;

            try
            {
                await _catalogService.RefreshLiveStatsAsync();
                ApplyFiltersAndSort();

                RefreshStatusSeverity = InfoBarSeverity.Success;
                LastRefreshedStatus = $"Package statistics updated successfully at {DateTime.Now:T}.";
                IsRefreshStatusOpen = true;
            }
            catch (Exception ex)
            {
                RefreshStatusSeverity = InfoBarSeverity.Warning;
                LastRefreshedStatus = $"Notice: Live stats refresh encountered an issue ({ex.Message}). Showing cached baseline stats.";
                IsRefreshStatusOpen = true;
            }
            finally
            {
                IsRefreshingStats = false;
            }
        }

        private void ApplyFiltersAndSort()
        {
            var all = _catalogService.GetAllLibraries();

            TotalLibrariesCount = all.Count;
            long totalDl = all.Sum(l => l.TotalDownloads);
            TotalCombinedDownloadsFormatted = totalDl >= 1_000_000_000
                ? $"{(totalDl / 1_000_000_000.0):F1}B+"
                : $"{(totalDl / 1_000_000.0):F1}M+";
            OpenSourceLibrariesCount = all.Count(l => !l.IsCommercial);
            CommercialLibrariesCount = all.Count(l => l.IsCommercial);

            IEnumerable<DocumentLibraryInfo> query = all;

            // Search query filter (matches Name, PackageId, Description, or Extension)
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string q = SearchQuery.Trim();
                query = query.Where(l =>
                    l.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    l.PackageId.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    l.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    l.SupportedExtensions.Any(ext => ext.Contains(q, StringComparison.OrdinalIgnoreCase)));
            }

            // Category filter
            if (SelectedCategoryFilterIndex > 0)
            {
                DocumentCategory targetCat = SelectedCategoryFilterIndex switch
                {
                    1 => DocumentCategory.Word,
                    2 => DocumentCategory.Excel,
                    3 => DocumentCategory.PowerPoint,
                    4 => DocumentCategory.Pdf,
                    5 => DocumentCategory.PlainText,
                    _ => DocumentCategory.Word
                };

                query = query.Where(l => l.SupportedCategories.Contains(targetCat));
            }

            // License filter
            if (SelectedLicenseFilterIndex == 1) // Open Source
            {
                query = query.Where(l => !l.IsCommercial);
            }
            else if (SelectedLicenseFilterIndex == 2) // Commercial / Paid
            {
                query = query.Where(l => l.IsCommercial);
            }

            // Sorting
            query = SelectedSortIndex switch
            {
                0 => query.OrderByDescending(l => l.TotalDownloads), // Most downloads
                1 => query.OrderByDescending(l => l.LatestPublishDate ?? DateTime.MinValue), // Recently updated
                2 => query.OrderByDescending(l => l.TotalReleasesCount).ThenBy(l => l.FirstReleaseDate ?? DateTime.MaxValue), // Longevity & releases
                3 => query.OrderBy(l => l.Name), // Alphabetical
                _ => query.OrderByDescending(l => l.TotalDownloads)
            };

            FilteredLibraries = new ObservableCollection<DocumentLibraryInfo>(query);
        }
    }
}

