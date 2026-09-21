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
            var licenseService = new FakeAsposeLicenseService(allLicensed: false);
            var catalogService = new DocumentLibraryCatalogService(licenseService);
            var vm = new DocumentLibrariesViewModel(catalogService, licenseService);

            // Sort 0: Most Downloads
            vm.SelectedSortIndex = 0;
            var first = vm.FilteredLibraries.First();
            first.PackageId.Should().Be("DocumentFormat.OpenXml"); // Over 437M downloads

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
    }
}

