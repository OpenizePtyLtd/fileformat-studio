using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing;

namespace FileFormatAIStudio.ViewModels
{
    /// <summary>
    /// Option item representing a parser engine selectable within a document category.
    /// </summary>
    public sealed record EngineOptionItem(
        string EngineId,
        string DisplayName,
        bool IsAspose = false,
        bool IsLicensed = false,
        string? LicenseStatus = null)
    {
        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// ViewModel representing a document category card in the Document Engines Settings view.
    /// </summary>
    public partial class DocumentCategorySettingItemViewModel : ObservableObject
    {
        private readonly IDocumentEnginePreferenceService _preferenceService;
        private readonly IAsposeLicenseService? _licenseService;

        public DocumentCategory Category { get; }
        public string CategoryName { get; }
        public string IconGlyph { get; }
        public string SupportedFormatsSummary { get; }
        public IReadOnlyList<EngineOptionItem> AvailableEngines { get; }

        [ObservableProperty]
        private EngineOptionItem _selectedEngine;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BenchmarkStatusText))]
        private string? _benchmarkWinnerDisplayName;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BenchmarkStatusText))]
        private bool _hasBenchmarkWinner;

        [ObservableProperty]
        private string _statusFeedback = string.Empty;

        public string BenchmarkStatusText => HasBenchmarkWinner
            ? $"Latest Benchmark Champion: {BenchmarkWinnerDisplayName}"
            : "No benchmark run yet for this category (Auto will use default engine)";

        public bool IsSelectedEngineAspose =>
            SelectedEngine != null &&
            SelectedEngine.EngineId.Contains("aspose", StringComparison.OrdinalIgnoreCase);

        public bool IsSelectedEngineLicensed =>
            _licenseService != null && _licenseService.IsEngineLicensed(SelectedEngine?.EngineId ?? "");

        public bool IsSelectedEngineInEvaluation =>
            IsSelectedEngineAspose && !IsSelectedEngineLicensed;

        public string LicenseStatusBadgeText =>
            IsSelectedEngineLicensed ? "Licensed" : "Evaluation Mode";

        public string LicenseStatusTooltip =>
            IsSelectedEngineLicensed
                ? "Active Aspose product license detected. Uncapped document extraction enabled."
                : (_licenseService?.GetEvaluationNotice(Category) ?? "Aspose is running without a license. Processing is subject to evaluation limits and watermarks.");

        public string LicenseStatusShortNote =>
            IsSelectedEngineLicensed
                ? "Licensed product"
                : (Category == DocumentCategory.Pdf ? "4-page ceiling" : "Evaluation watermarks");

        public DocumentCategorySettingItemViewModel(
            DocumentCategory category,
            string categoryName,
            string iconGlyph,
            string supportedFormatsSummary,
            IReadOnlyList<EngineOptionItem> availableEngines,
            EngineOptionItem initialSelectedEngine,
            string? benchmarkWinnerDisplayName,
            IDocumentEnginePreferenceService preferenceService,
            IAsposeLicenseService? licenseService = null)
        {
            Category = category;
            CategoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
            IconGlyph = iconGlyph ?? throw new ArgumentNullException(nameof(iconGlyph));
            SupportedFormatsSummary = supportedFormatsSummary ?? throw new ArgumentNullException(nameof(supportedFormatsSummary));
            AvailableEngines = availableEngines ?? throw new ArgumentNullException(nameof(availableEngines));
            _selectedEngine = initialSelectedEngine ?? (availableEngines.Count > 0 ? availableEngines[0] : new EngineOptionItem("Auto", "Auto"));
            _benchmarkWinnerDisplayName = benchmarkWinnerDisplayName;
            _hasBenchmarkWinner = !string.IsNullOrWhiteSpace(benchmarkWinnerDisplayName);
            _preferenceService = preferenceService ?? throw new ArgumentNullException(nameof(preferenceService));
            _licenseService = licenseService;
        }

        partial void OnSelectedEngineChanged(EngineOptionItem value)
        {
            if (value != null)
            {
                _ = _preferenceService.SetPreferredEngineIdAsync(Category, value.EngineId);
                StatusFeedback = $"Saved: Using {value.DisplayName} for {CategoryName}.";
                OnPropertyChanged(nameof(IsSelectedEngineAspose));
                OnPropertyChanged(nameof(IsSelectedEngineLicensed));
                OnPropertyChanged(nameof(IsSelectedEngineInEvaluation));
                OnPropertyChanged(nameof(LicenseStatusBadgeText));
                OnPropertyChanged(nameof(LicenseStatusTooltip));
                OnPropertyChanged(nameof(LicenseStatusShortNote));
            }
        }
    }
}

