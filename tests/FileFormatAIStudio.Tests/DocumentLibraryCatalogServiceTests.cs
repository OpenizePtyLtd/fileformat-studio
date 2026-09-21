using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Models;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing;
using FileFormatAIStudio.Tests.TestHelpers;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class DocumentLibraryCatalogServiceTests
    {
        [Fact]
        public void GetAllLibraries_ReturnsAll10IntegratedLibraries()
        {
            var licenseService = new FakeAsposeLicenseService(allLicensed: false);
            var catalogService = new DocumentLibraryCatalogService(licenseService);

            var libraries = catalogService.GetAllLibraries();

            libraries.Should().HaveCount(10);
            var ids = libraries.Select(l => l.Id).ToList();
            ids.Should().Contain(new[]
            {
                "aspose-words",
                "aspose-cells",
                "aspose-slides",
                "aspose-pdf",
                "openxml-words",
                "pdfpig",
                "exceldatareader",
                "csvhelper",
                "officeparser",
                "plaintext"
            });
        }

        [Fact]
        public void Catalog_HasCorrectCategoriesAndFormats()
        {
            var catalogService = new DocumentLibraryCatalogService();
            var libraries = catalogService.GetAllLibraries();

            var openXml = libraries.First(l => l.Id == "openxml-words");
            openXml.PrimaryCategory.Should().Be(DocumentCategory.Word);
            openXml.SupportedExtensions.Should().Contain(".docx");
            openXml.IsCommercial.Should().BeFalse();
            openXml.LicenseType.Should().Contain("MIT");

            var pdfPig = libraries.First(l => l.Id == "pdfpig");
            pdfPig.PrimaryCategory.Should().Be(DocumentCategory.Pdf);
            pdfPig.SupportedExtensions.Should().Contain(".pdf");
            pdfPig.IsCommercial.Should().BeFalse();

            var officeParser = libraries.First(l => l.Id == "officeparser");
            officeParser.Ecosystem.Should().Contain("npm");
            officeParser.SupportedExtensions.Should().Contain(new[] { ".docx", ".pptx", ".xlsx", ".pdf" });

            var asposeWords = libraries.First(l => l.Id == "aspose-words");
            asposeWords.IsCommercial.Should().BeTrue();
            asposeWords.SupportedExtensions.Should().Contain(new[] { ".docx", ".doc", ".rtf", ".odt" });
        }

        [Fact]
        public void Catalog_FormatHelpers_ProduceAccurateStrings()
        {
            var lib = new DocumentLibraryInfo
            {
                TotalDownloads = 437_318_993,
                LatestVersion = "3.5.1",
                LatestPublishDate = new DateTime(2026, 9, 2),
                FirstReleaseDate = new DateTime(2014, 6, 1),
                TotalReleasesCount = 65
            };

            lib.DownloadsFormatted.Should().Be("437.3M Downloads");
            lib.RecencyText.Should().Contain("Sep 2026").And.Contain("v3.5.1");
            lib.MarketLongevityText.Should().Contain("years in market").And.Contain("65 releases");
        }

        [Fact]
        public void Catalog_ReflectsAsposeLicenseStateChanges()
        {
            var licenseService = new FakeAsposeLicenseService(allLicensed: false);
            var catalogService = new DocumentLibraryCatalogService(licenseService);

            var unLicensedLibs = catalogService.GetAllLibraries();
            var asposeWord = unLicensedLibs.First(l => l.Id == "aspose-words");
            asposeWord.LicenseStatus.Should().Contain("Evaluation Mode");

            // Now activate license
            licenseService.InstallLicense("fake_license.lic");

            var licensedLibs = catalogService.GetAllLibraries();
            var licensedAsposeWord = licensedLibs.First(l => l.Id == "aspose-words");
            licensedAsposeWord.LicenseStatus.Should().Be("Active Commercial License");
        }

        [Fact]
        public void ViewModel_FiltersBySearchQuery()
        {
            var licenseService = new FakeAsposeLicenseService(allLicensed: false);
            var catalogService = new DocumentLibraryCatalogService(licenseService);
            var vm = new DocumentLibrariesViewModel(catalogService, licenseService);

            // Filter for pdf
            vm.SearchQuery = "pdf";

            vm.FilteredLibraries.Should().NotBeEmpty();
            vm.FilteredLibraries.Should().OnlyContain(l =>
                l.Name.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
                l.PackageId.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
                l.Description.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
                l.SupportedExtensions.Any(e => e.Contains("pdf", StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public void ViewModel_FiltersByCategory()
        {
            var licenseService = new FakeAsposeLicenseService(allLicensed: false);
            var catalogService = new DocumentLibraryCatalogService(licenseService);
            var vm = new DocumentLibrariesViewModel(catalogService, licenseService);

            // 4: PDF Documents
            vm.SelectedCategoryFilterIndex = 4;

            vm.FilteredLibraries.Should().NotBeEmpty();
            vm.FilteredLibraries.Should().OnlyContain(l => l.SupportedCategories.Contains(DocumentCategory.Pdf));
        }

        [Fact]
        public void ViewModel_FiltersByLicense()
        {
            var licenseService = new FakeAsposeLicenseService(allLicensed: false);
            var catalogService = new DocumentLibraryCatalogService(licenseService);
            var vm = new DocumentLibrariesViewModel(catalogService, licenseService);

            // 1: Open Source only
            vm.SelectedLicenseFilterIndex = 1;
            vm.FilteredLibraries.Should().OnlyContain(l => !l.IsCommercial);
            vm.FilteredLibraries.Count.Should().Be(6);

            // 2: Commercial only
            vm.SelectedLicenseFilterIndex = 2;
            vm.FilteredLibraries.Should().OnlyContain(l => l.IsCommercial);
            vm.FilteredLibraries.Count.Should().Be(4);
        }

        [Fact]
        public void ViewModel_SortsByDownloadsAndRecency()
        {
            string nonExistentCache = Path.Combine(Path.GetTempPath(), "FF_EmptyCache_" + Guid.NewGuid().ToString("N") + ".json");
            var licenseService = new FakeAsposeLicenseService(allLicensed: false);
            var catalogService = new DocumentLibraryCatalogService(licenseService, cacheFilePath: nonExistentCache);
            var vm = new DocumentLibrariesViewModel(catalogService, licenseService);

            // Sort 0: Most Downloads
            vm.SelectedSortIndex = 0;
            var first = vm.FilteredLibraries.First();
            first.PackageId.Should().Be("DocumentFormat.OpenXml"); // Over 437M downloads in baseline

            // Sort 3: Alphabetical
            vm.SelectedSortIndex = 3;
            var alphaFirst = vm.FilteredLibraries.First();
            alphaFirst.Name.Should().StartWith("Aspose.Cells");
        }

        [Fact]
        public async Task RefreshLiveStatsAsync_ExecutesSafelyWithoutThrowingExceptions()
        {
            var licenseService = new FakeAsposeLicenseService(allLicensed: false);
            var catalogService = new DocumentLibraryCatalogService(licenseService);

            // Refresh should handle any network timeouts/failures silently without throwing
            var result = await catalogService.RefreshLiveStatsAsync();

            result.Should().NotBeNull();
            result.Should().HaveCount(10);
        }

        [Fact]
        public async Task RefreshLiveStatsAsync_SavesStatsToCacheFile_AndRehydratesOnStartup()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FFTests_" + Guid.NewGuid().ToString("N"));
            string cachePath = Path.Combine(tempDir, "document_libraries_cache.json");

            try
            {
                var licenseService = new FakeAsposeLicenseService(allLicensed: false);
                var service1 = new DocumentLibraryCatalogService(licenseService, cacheFilePath: cachePath);

                // Initial baseline
                var initialOpenXml = service1.GetAllLibraries().First(l => l.Id == "openxml-words");
                long baselineDownloads = initialOpenXml.TotalDownloads;

                // Execute refresh which will query APIs or keep baseline and save to cache file
                await service1.RefreshLiveStatsAsync();

                File.Exists(cachePath).Should().BeTrue();
                string json = File.ReadAllText(cachePath);
                json.Should().Contain("openxml-words");

                // Now simulate manual modification / new live fetched values in cache
                string modifiedJson = json.Replace(
                    baselineDownloads.ToString(),
                    "999888777");
                File.WriteAllText(cachePath, modifiedJson);

                // Create a new service instance pointing to the same cache file (simulating app restart)
                var service2 = new DocumentLibraryCatalogService(licenseService, cacheFilePath: cachePath);
                var rehydratedOpenXml = service2.GetAllLibraries().First(l => l.Id == "openxml-words");

                rehydratedOpenXml.TotalDownloads.Should().Be(999888777);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public void DocumentLibraryCatalogService_HandlesCorruptCacheFileGracefully()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FFTests_" + Guid.NewGuid().ToString("N"));
            string cachePath = Path.Combine(tempDir, "document_libraries_cache.json");

            try
            {
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(cachePath, "{ invalid json content truncated [!] }");

                var licenseService = new FakeAsposeLicenseService(allLicensed: false);
                var service = new DocumentLibraryCatalogService(licenseService, cacheFilePath: cachePath);

                var libs = service.GetAllLibraries();
                libs.Should().HaveCount(10);

                var openXml = libs.First(l => l.Id == "openxml-words");
                openXml.TotalDownloads.Should().BeGreaterThan(0); // Baseline preserved
                service.LastStatsRefreshedUtc.Should().BeNull();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public async Task LastStatsRefreshedUtc_PersistsAndFormatsInViewModel()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FFTests_" + Guid.NewGuid().ToString("N"));
            string cachePath = Path.Combine(tempDir, "document_libraries_cache.json");

            try
            {
                var licenseService = new FakeAsposeLicenseService(allLicensed: false);
                var service1 = new DocumentLibraryCatalogService(licenseService, cacheFilePath: cachePath);
                var vm1 = new DocumentLibrariesViewModel(service1, licenseService);

                vm1.LastUpdatedDisplay.Should().Be("Using built-in baseline statistics");
                service1.LastStatsRefreshedUtc.Should().BeNull();

                // Refresh stats
                await service1.RefreshLiveStatsAsync();

                service1.LastStatsRefreshedUtc.Should().NotBeNull();
                service1.LastStatsRefreshedUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

                // Re-instantiate service pointing to same cache file (app restart)
                var service2 = new DocumentLibraryCatalogService(licenseService, cacheFilePath: cachePath);
                service2.LastStatsRefreshedUtc.Should().NotBeNull();
                service2.LastStatsRefreshedUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

                var vm2 = new DocumentLibrariesViewModel(service2, licenseService);
                vm2.LastUpdatedDisplay.Should().StartWith("Last refreshed:");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }
    }
}



