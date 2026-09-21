using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Models;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing
{
    /// <summary>
    /// Default implementation of IDocumentLibraryCatalogService.
    /// Maintains catalog of all 10 document extraction libraries and supports live stats refresh via NuGet and npm APIs.
    /// </summary>
    public class DocumentLibraryCatalogService : IDocumentLibraryCatalogService
    {
        private readonly IAsposeLicenseService? _licenseService;
        private readonly HttpClient _httpClient;
        private readonly List<DocumentLibraryInfo> _libraries;
        private readonly object _lock = new();

        public event EventHandler? CatalogUpdated;

        public DocumentLibraryCatalogService(IAsposeLicenseService? licenseService = null, HttpClient? httpClient = null)
        {
            _licenseService = licenseService;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            _libraries = InitializeBaselineCatalog();
            SyncLicenseState();
        }

        public IReadOnlyList<DocumentLibraryInfo> GetAllLibraries()
        {
            lock (_lock)
            {
                SyncLicenseState();
                return _libraries.Select(CloneLibrary).ToList().AsReadOnly();
            }
        }

        public async Task<IReadOnlyList<DocumentLibraryInfo>> RefreshLiveStatsAsync(CancellationToken cancellationToken = default)
        {
            var updateTasks = new List<Task>();

            foreach (var lib in _libraries)
            {
                if (lib.DownloadStatsSource == "nuget.org")
                {
                    updateTasks.Add(UpdateNuGetStatsAsync(lib, cancellationToken));
                }
                else if (lib.DownloadStatsSource == "npmjs.com")
                {
                    updateTasks.Add(UpdateNpmStatsAsync(lib, cancellationToken));
                }
            }

            try
            {
                await Task.WhenAll(updateTasks);
            }
            catch
            {
                // Network errors or timeouts fall back silently to current catalog values
            }

            SyncLicenseState();
            CatalogUpdated?.Invoke(this, EventArgs.Empty);

            return GetAllLibraries();
        }

        private async Task UpdateNuGetStatsAsync(DocumentLibraryInfo lib, CancellationToken cancellationToken)
        {
            try
            {
                string url = $"https://azuresearch-usnc.nuget.org/query?q=packageid:{Uri.EscapeDataString(lib.PackageId)}&take=1";
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode) return;

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                {
                    var pkg = data[0];
                    if (pkg.TryGetProperty("totalDownloads", out var dlElem) && dlElem.TryGetInt64(out long downloads))
                    {
                        lib.TotalDownloads = downloads;
                    }

                    if (pkg.TryGetProperty("version", out var verElem))
                    {
                        string? latestVer = verElem.GetString();
                        if (!string.IsNullOrEmpty(latestVer))
                        {
                            lib.LatestVersion = latestVer;
                        }
                    }

                    if (pkg.TryGetProperty("versions", out var versionsElem) && versionsElem.ValueKind == JsonValueKind.Array)
                    {
                        lib.TotalReleasesCount = versionsElem.GetArrayLength();
                    }
                }
            }
            catch
            {
                // Preserve existing metrics on any HTTP or parsing issue
            }
        }

        private async Task UpdateNpmStatsAsync(DocumentLibraryInfo lib, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Fetch metadata and versions from npm registry
                string regUrl = $"https://registry.npmjs.org/{Uri.EscapeDataString(lib.PackageId)}";
                using var regResponse = await _httpClient.GetAsync(regUrl, cancellationToken);
                if (regResponse.IsSuccessStatusCode)
                {
                    using var stream = await regResponse.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("dist-tags", out var distTags) &&
                        distTags.TryGetProperty("latest", out var latestElem))
                    {
                        string? latestVer = latestElem.GetString();
                        if (!string.IsNullOrEmpty(latestVer))
                        {
                            lib.LatestVersion = latestVer;
                        }
                    }

                    if (doc.RootElement.TryGetProperty("versions", out var versionsElem) &&
                        versionsElem.ValueKind == JsonValueKind.Object)
                    {
                        lib.TotalReleasesCount = versionsElem.EnumerateObject().Count();
                    }
                }

                // 2. Fetch download count for last year
                string dlUrl = $"https://api.npmjs.org/downloads/point/last-year/{Uri.EscapeDataString(lib.PackageId)}";
                using var dlResponse = await _httpClient.GetAsync(dlUrl, cancellationToken);
                if (dlResponse.IsSuccessStatusCode)
                {
                    using var stream = await dlResponse.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("downloads", out var dlElem) && dlElem.TryGetInt64(out long downloads))
                    {
                        lib.TotalDownloads = downloads;
                    }
                }
            }
            catch
            {
                // Preserve existing metrics on any error
            }
        }

        private void SyncLicenseState()
        {
            foreach (var lib in _libraries)
            {
                if (lib.IsCommercial)
                {
                    bool isLicensed = false;
                    if (_licenseService != null)
                    {
                        isLicensed = lib.Id switch
                        {
                            "aspose-words" => _licenseService.IsWordsLicensed,
                            "aspose-cells" => _licenseService.IsCellsLicensed,
                            "aspose-slides" => _licenseService.IsSlidesLicensed,
                            "aspose-pdf" => _licenseService.IsPdfLicensed,
                            _ => _licenseService.IsLicensed
                        };
                    }

                    lib.LicenseStatus = isLicensed ? "Active Commercial License" : "Evaluation Mode (Unlicensed)";
                }
            }
        }

        private static DocumentLibraryInfo CloneLibrary(DocumentLibraryInfo source)
        {
            return new DocumentLibraryInfo
            {
                Id = source.Id,
                Name = source.Name,
                PackageId = source.PackageId,
                PackageUrl = source.PackageUrl,
                ProjectUrl = source.ProjectUrl,
                Ecosystem = source.Ecosystem,
                LicenseType = source.LicenseType,
                IsCommercial = source.IsCommercial,
                LicenseStatus = source.LicenseStatus,
                Description = source.Description,
                PrimaryCategory = source.PrimaryCategory,
                SupportedCategories = source.SupportedCategories.ToList(),
                SupportedExtensions = source.SupportedExtensions.ToList(),
                InstalledVersion = source.InstalledVersion,
                LatestVersion = source.LatestVersion,
                LatestPublishDate = source.LatestPublishDate,
                FirstReleaseDate = source.FirstReleaseDate,
                TotalReleasesCount = source.TotalReleasesCount,
                TotalDownloads = source.TotalDownloads,
                DownloadStatsSource = source.DownloadStatsSource
            };
        }

        private static List<DocumentLibraryInfo> InitializeBaselineCatalog()
        {
            return new List<DocumentLibraryInfo>
            {
                new()
                {
                    Id = "aspose-words",
                    Name = "Aspose.Words for .NET",
                    PackageId = "Aspose.Words",
                    PackageUrl = "https://www.nuget.org/packages/Aspose.Words",
                    ProjectUrl = "https://github.com/aspose-words/Aspose.Words-for-.NET",
                    Ecosystem = "NuGet (.NET 10)",
                    LicenseType = "Commercial / Proprietary",
                    IsCommercial = true,
                    LicenseStatus = "Evaluation Mode (Unlicensed)",
                    Description = "Enterprise-grade word processing library for Word and markup formats. Extracts styled paragraphs, structural headings, tables, headers/footers, and complex document hierarchies with full formatting preservation without requiring Microsoft Office.",
                    PrimaryCategory = DocumentCategory.Word,
                    SupportedCategories = new[] { DocumentCategory.Word, DocumentCategory.PlainText },
                    SupportedExtensions = new[] { ".docx", ".doc", ".docm", ".dotx", ".dotm", ".rtf", ".odt", ".html", ".htm", ".txt", ".mhtml" },
                    InstalledVersion = "26.9.0",
                    LatestVersion = "26.9.0",
                    LatestPublishDate = new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc),
                    FirstReleaseDate = new DateTime(2011, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalReleasesCount = 179,
                    TotalDownloads = 48_611_688,
                    DownloadStatsSource = "nuget.org"
                },
                new()
                {
                    Id = "aspose-cells",
                    Name = "Aspose.Cells for .NET",
                    PackageId = "Aspose.Cells",
                    PackageUrl = "https://www.nuget.org/packages/Aspose.Cells",
                    ProjectUrl = "https://github.com/aspose-cells/Aspose.Cells-for-.NET",
                    Ecosystem = "NuGet (.NET 10)",
                    LicenseType = "Commercial / Proprietary",
                    IsCommercial = true,
                    LicenseStatus = "Evaluation Mode (Unlicensed)",
                    Description = "Comprehensive spreadsheet manipulation engine capable of processing massive workbooks, evaluating complex formulas, and extracting structured cell grids, worksheets, and charts across all major spreadsheet formats.",
                    PrimaryCategory = DocumentCategory.Excel,
                    SupportedCategories = new[] { DocumentCategory.Excel },
                    SupportedExtensions = new[] { ".xlsx", ".xls", ".xlsm", ".xlsb", ".csv", ".tsv", ".ods" },
                    InstalledVersion = "26.8.0",
                    LatestVersion = "26.8.0",
                    LatestPublishDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                    FirstReleaseDate = new DateTime(2011, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalReleasesCount = 185,
                    TotalDownloads = 39_240_000,
                    DownloadStatsSource = "nuget.org"
                },
                new()
                {
                    Id = "aspose-slides",
                    Name = "Aspose.Slides for .NET",
                    PackageId = "Aspose.Slides.NET",
                    PackageUrl = "https://www.nuget.org/packages/Aspose.Slides.NET",
                    ProjectUrl = "https://github.com/aspose-slides/Aspose.Slides-for-.NET",
                    Ecosystem = "NuGet (.NET 10)",
                    LicenseType = "Commercial / Proprietary",
                    IsCommercial = true,
                    LicenseStatus = "Evaluation Mode (Unlicensed)",
                    Description = "Advanced presentation processing library for PowerPoint slide decks. Parses slide structures, slide notes, embedded shapes, tables, and multimedia hierarchies into cleanly structured text.",
                    PrimaryCategory = DocumentCategory.PowerPoint,
                    SupportedCategories = new[] { DocumentCategory.PowerPoint },
                    SupportedExtensions = new[] { ".pptx", ".ppt", ".pptm", ".potx", ".potm", ".odp" },
                    InstalledVersion = "26.9.0",
                    LatestVersion = "26.9.0",
                    LatestPublishDate = new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc),
                    FirstReleaseDate = new DateTime(2011, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalReleasesCount = 172,
                    TotalDownloads = 28_150_000,
                    DownloadStatsSource = "nuget.org"
                },
                new()
                {
                    Id = "aspose-pdf",
                    Name = "Aspose.PDF for .NET",
                    PackageId = "Aspose.PDF",
                    PackageUrl = "https://www.nuget.org/packages/Aspose.PDF",
                    ProjectUrl = "https://github.com/aspose-pdf/Aspose.PDF-for-.NET",
                    Ecosystem = "NuGet (.NET 10)",
                    LicenseType = "Commercial / Proprietary",
                    IsCommercial = true,
                    LicenseStatus = "Evaluation Mode (Unlicensed)",
                    Description = "Enterprise-grade PDF parsing and layout analysis engine. Accurately extracts text, flow segments, tables, form fields, and metadata from complex tagged and untagged PDF documents.",
                    PrimaryCategory = DocumentCategory.Pdf,
                    SupportedCategories = new[] { DocumentCategory.Pdf },
                    SupportedExtensions = new[] { ".pdf" },
                    InstalledVersion = "26.8.0",
                    LatestVersion = "26.8.0",
                    LatestPublishDate = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
                    FirstReleaseDate = new DateTime(2011, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalReleasesCount = 190,
                    TotalDownloads = 42_800_000,
                    DownloadStatsSource = "nuget.org"
                },
                new()
                {
                    Id = "openxml-words",
                    Name = "DocumentFormat.OpenXml",
                    PackageId = "DocumentFormat.OpenXml",
                    PackageUrl = "https://www.nuget.org/packages/DocumentFormat.OpenXml",
                    ProjectUrl = "https://github.com/dotnet/Open-XML-SDK",
                    Ecosystem = "NuGet (.NET 10)",
                    LicenseType = "Open Source (MIT)",
                    IsCommercial = false,
                    LicenseStatus = "Open Source (Free)",
                    Description = "Official Microsoft .NET Open XML SDK for manipulating Office Open XML documents (DOCX, XLSX, PPTX). Extremely popular, lightweight, and reliable for high-speed streaming extraction of Word documents without Office interop.",
                    PrimaryCategory = DocumentCategory.Word,
                    SupportedCategories = new[] { DocumentCategory.Word },
                    SupportedExtensions = new[] { ".docx" },
                    InstalledVersion = "3.5.1",
                    LatestVersion = "3.5.1",
                    LatestPublishDate = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
                    FirstReleaseDate = new DateTime(2014, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalReleasesCount = 65,
                    TotalDownloads = 437_318_993,
                    DownloadStatsSource = "nuget.org"
                },
                new()
                {
                    Id = "pdfpig",
                    Name = "PdfPig",
                    PackageId = "PdfPig",
                    PackageUrl = "https://www.nuget.org/packages/PdfPig",
                    ProjectUrl = "https://github.com/UglyToad/PdfPig",
                    Ecosystem = "NuGet (.NET 10)",
                    LicenseType = "Open Source (Apache-2.0)",
                    IsCommercial = false,
                    LicenseStatus = "Open Source (Free)",
                    Description = "Pure C# port of Apache PDFBox providing comprehensive extraction of text, words, letters, fonts, and bounding boxes from PDF documents with zero external native C++ dependencies or unmanaged wrappers.",
                    PrimaryCategory = DocumentCategory.Pdf,
                    SupportedCategories = new[] { DocumentCategory.Pdf },
                    SupportedExtensions = new[] { ".pdf" },
                    InstalledVersion = "0.1.16",
                    LatestVersion = "0.1.16",
                    LatestPublishDate = new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc),
                    FirstReleaseDate = new DateTime(2018, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalReleasesCount = 42,
                    TotalDownloads = 33_029_572,
                    DownloadStatsSource = "nuget.org"
                },
                new()
                {
                    Id = "exceldatareader",
                    Name = "ExcelDataReader",
                    PackageId = "ExcelDataReader",
                    PackageUrl = "https://www.nuget.org/packages/ExcelDataReader",
                    ProjectUrl = "https://github.com/ExcelDataReader/ExcelDataReader",
                    Ecosystem = "NuGet (.NET 10)",
                    LicenseType = "Open Source (MIT)",
                    IsCommercial = false,
                    LicenseStatus = "Open Source (Free)",
                    Description = "High-speed, memory-efficient C# reader for Excel files (.xlsx, .xls) supporting streaming iteration over large workbooks without loading entire datasets into memory simultaneously.",
                    PrimaryCategory = DocumentCategory.Excel,
                    SupportedCategories = new[] { DocumentCategory.Excel },
                    SupportedExtensions = new[] { ".xlsx", ".xls", ".csv" },
                    InstalledVersion = "3.9.0",
                    LatestVersion = "3.9.0",
                    LatestPublishDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                    FirstReleaseDate = new DateTime(2013, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalReleasesCount = 58,
                    TotalDownloads = 142_500_000,
                    DownloadStatsSource = "nuget.org"
                },
                new()
                {
                    Id = "csvhelper",
                    Name = "CsvHelper",
                    PackageId = "CsvHelper",
                    PackageUrl = "https://www.nuget.org/packages/CsvHelper",
                    ProjectUrl = "https://github.com/JoshClose/CsvHelper",
                    Ecosystem = "NuGet (.NET 10)",
                    LicenseType = "Open Source (MS-PL / Apache-2.0)",
                    IsCommercial = false,
                    LicenseStatus = "Open Source (Free)",
                    Description = "The premier .NET library for reading and parsing delimiter-separated files (CSV, TSV). Features lightning-fast streaming, robust edge-case handling for malformed delimiters and escaping, and negligible memory overhead.",
                    PrimaryCategory = DocumentCategory.Excel,
                    SupportedCategories = new[] { DocumentCategory.Excel },
                    SupportedExtensions = new[] { ".csv", ".tsv" },
                    InstalledVersion = "33.1.0",
                    LatestVersion = "33.1.0",
                    LatestPublishDate = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                    FirstReleaseDate = new DateTime(2011, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalReleasesCount = 198,
                    TotalDownloads = 318_200_000,
                    DownloadStatsSource = "nuget.org"
                },
                new()
                {
                    Id = "officeparser",
                    Name = "officeparser",
                    PackageId = "officeparser",
                    PackageUrl = "https://www.npmjs.com/package/officeparser",
                    ProjectUrl = "https://github.com/harshankur/officeParser",
                    Ecosystem = "npm (Node.js)",
                    LicenseType = "Open Source (MIT)",
                    IsCommercial = false,
                    LicenseStatus = "Open Source (Free)",
                    Description = "High-performance JavaScript / TypeScript office file parser for Node.js. Operated via FileFormat AI Studio's background IPC daemon subsystem with process pooling, extracting text from DOCX, PPTX, XLSX, and PDF using fast AST and XML decompression.",
                    PrimaryCategory = DocumentCategory.Word,
                    SupportedCategories = new[] { DocumentCategory.Word, DocumentCategory.Excel, DocumentCategory.PowerPoint, DocumentCategory.Pdf },
                    SupportedExtensions = new[] { ".docx", ".pptx", ".xlsx", ".pdf", ".odt", ".odp", ".ods" },
                    InstalledVersion = "6.0.4",
                    LatestVersion = "6.0.4",
                    LatestPublishDate = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc),
                    FirstReleaseDate = new DateTime(2019, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalReleasesCount = 35,
                    TotalDownloads = 17_199_668,
                    DownloadStatsSource = "npmjs.com"
                },
                new()
                {
                    Id = "plaintext",
                    Name = "System Text Reader",
                    PackageId = "System.IO.BuiltIn",
                    PackageUrl = "https://dotnet.microsoft.com/",
                    ProjectUrl = "https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream",
                    Ecosystem = "Built-in (.NET 10)",
                    LicenseType = "Built-in / Core System",
                    IsCommercial = false,
                    LicenseStatus = "Built-in",
                    Description = "Native high-throughput streaming text extractor utilizing .NET 10 System.IO and System.Text. Decodes raw text, Markdown, configuration, logs, code, and serialized structures with automatic UTF-8/UTF-16 BOM detection.",
                    PrimaryCategory = DocumentCategory.PlainText,
                    SupportedCategories = new[] { DocumentCategory.PlainText },
                    SupportedExtensions = new[] { ".txt", ".md", ".csv", ".log", ".json", ".xml", ".yaml", ".yml" },
                    InstalledVersion = "10.0.0",
                    LatestVersion = "10.0.0",
                    LatestPublishDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    FirstReleaseDate = new DateTime(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    TotalReleasesCount = 10,
                    TotalDownloads = 0,
                    DownloadStatsSource = "System"
                }
            };
        }
    }
}

