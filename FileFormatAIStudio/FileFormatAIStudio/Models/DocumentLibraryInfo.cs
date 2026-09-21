using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Models
{
    /// <summary>
    /// Represents metadata, package metrics, and licensing information for an integrated document extraction library.
    /// </summary>
    public partial class DocumentLibraryInfo : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _packageId = string.Empty;

        [ObservableProperty]
        private string _packageUrl = string.Empty;

        [ObservableProperty]
        private string _projectUrl = string.Empty;

        [ObservableProperty]
        private string _ecosystem = string.Empty; // "NuGet (.NET 10)", "npm (Node.js)", "Built-in (.NET)"

        [ObservableProperty]
        private string _licenseType = string.Empty; // "Commercial / Proprietary", "Open Source (MIT)", "Open Source (Apache-2.0)"

        [ObservableProperty]
        private bool _isCommercial;

        [ObservableProperty]
        private string _licenseStatus = string.Empty; // "Active Commercial License", "Evaluation Mode", "Open Source (Free)", "Built-in"

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private DocumentCategory _primaryCategory;

        [ObservableProperty]
        private IReadOnlyList<DocumentCategory> _supportedCategories = Array.Empty<DocumentCategory>();

        [ObservableProperty]
        private IReadOnlyList<string> _supportedExtensions = Array.Empty<string>();

        [ObservableProperty]
        private string _installedVersion = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RecencyText))]
        private string _latestVersion = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RecencyText))]
        private DateTime? _latestPublishDate;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MarketLongevityText))]
        private DateTime? _firstReleaseDate;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MarketLongevityText))]
        private int _totalReleasesCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DownloadsFormatted))]
        private long _totalDownloads;

        [ObservableProperty]
        private string _downloadStatsSource = "nuget.org";

        /// <summary>
        /// Human-readable formatted downloads string, e.g., "437.3M Downloads".
        /// </summary>
        public string DownloadsFormatted
        {
            get
            {
                if (TotalDownloads <= 0) return "Built-in";
                if (TotalDownloads >= 1_000_000_000)
                    return $"{(TotalDownloads / 1_000_000_000.0):F1}B Downloads";
                if (TotalDownloads >= 1_000_000)
                    return $"{(TotalDownloads / 1_000_000.0):F1}M Downloads";
                if (TotalDownloads >= 1_000)
                    return $"{(TotalDownloads / 1_000.0):F1}K Downloads";
                return $"{TotalDownloads:N0} Downloads";
            }
        }

        /// <summary>
        /// Human-readable update recency string, e.g., "Updated Sep 2026 (v3.5.1)".
        /// </summary>
        public string RecencyText
        {
            get
            {
                if (LatestPublishDate == null)
                    return !string.IsNullOrEmpty(LatestVersion) ? $"v{LatestVersion}" : "Current";

                var date = LatestPublishDate.Value;
                return $"Updated {date.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)} (v{LatestVersion})";
            }
        }

        /// <summary>
        /// Human-readable market longevity and maturity string, e.g., "15+ years in market (179 releases)".
        /// </summary>
        public string MarketLongevityText
        {
            get
            {
                if (FirstReleaseDate == null)
                {
                    return TotalReleasesCount > 0
                        ? $"{TotalReleasesCount} releases"
                        : "Long-standing core library";
                }

                int years = Math.Max(1, DateTime.UtcNow.Year - FirstReleaseDate.Value.Year);
                string releasesStr = TotalReleasesCount > 0 ? $" ({TotalReleasesCount} releases)" : string.Empty;
                return $"{years}+ years in market{releasesStr}";
            }
        }

        /// <summary>
        /// Formatted comma-separated list of supported extensions for UI display.
        /// </summary>
        public string SupportedExtensionsSummary => string.Join(", ", SupportedExtensions);
    }
}
