using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing;
using FileFormatAIStudio.Services.Parsing.Engines.Word;
using FileFormatAIStudio.Services.Settings;
using FileFormatAIStudio.Tests.TestHelpers;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml.Controls;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class SettingsLicenseManagementTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly AppDbContext _context;
        private readonly ISettingsService _settingsService;
        private readonly IAIClientFactory _aiClientFactory;
        private readonly DocumentCategoryRegistry _categoryRegistry;
        private readonly FakeAsposeLicenseService _licenseService;
        private readonly DocumentEnginePreferenceService _preferenceService;
        private readonly DocumentParserFactory _parserFactory;

        public SettingsLicenseManagementTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new AppDbContext(_options);
            _context.Database.EnsureCreated();

            _settingsService = new SettingsService(_context);
            _aiClientFactory = new FakeAiClientFactory();
            _categoryRegistry = new DocumentCategoryRegistry();
            _licenseService = new FakeAsposeLicenseService(allLicensed: false);

            _parserFactory = new DocumentParserFactory(new IDocumentParser[]
            {
                new AsposeWordsParser(_licenseService),
                new OpenXmlWordParser(),
                new PlainTextParser()
            });

            _preferenceService = new DocumentEnginePreferenceService(_context, _categoryRegistry, _parserFactory);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public void InitialState_ReflectsUnlicensedAspose()
        {
            var vm = new SettingsViewModel(
                _settingsService,
                _aiClientFactory,
                _preferenceService,
                _categoryRegistry,
                _parserFactory,
                _licenseService);

            vm.RefreshAsposeLicenseState();

            vm.IsAsposeLicensed.Should().BeFalse();
            vm.CanRemoveAsposeLicense.Should().BeFalse();
            vm.IsWordsLicensed.Should().BeFalse();
            vm.IsCellsLicensed.Should().BeFalse();
            vm.IsSlidesLicensed.Should().BeFalse();
            vm.IsPdfLicensed.Should().BeFalse();
        }

        [Fact]
        public async Task InstallLicenseAsync_ValidFile_ActivatesLicenseAndUpdatesViewModel()
        {
            var vm = new SettingsViewModel(
                _settingsService,
                _aiClientFactory,
                _preferenceService,
                _categoryRegistry,
                _parserFactory,
                _licenseService);

            await vm.LoadDocumentEngineSettingsAsync();

            var tempFile = Path.Combine(Path.GetTempPath(), $"test_aspose_{Guid.NewGuid():N}.lic");
            await File.WriteAllTextAsync(tempFile, "<License></License>");

            try
            {
                await vm.InstallLicenseAsync(tempFile);

                vm.IsAsposeLicensed.Should().BeTrue();
                vm.CanRemoveAsposeLicense.Should().BeTrue();
                vm.IsWordsLicensed.Should().BeTrue();
                vm.IsAsposeLicenseStatusOpen.Should().BeTrue();
                vm.AsposeLicenseStatusSeverity.Should().Be(InfoBarSeverity.Success);
                vm.AsposeLicenseStatusMessage.Should().Contain("activated successfully");

                // Verify Word category dropdown options now show Licensed
                var wordCat = vm.DocumentCategories.FirstOrDefault(c => c.Category == DocumentCategory.Word);
                wordCat.Should().NotBeNull();
                var asposeOption = wordCat!.AvailableEngines.FirstOrDefault(e => e.EngineId == "aspose-words");
                asposeOption.Should().NotBeNull();
                asposeOption!.DisplayName.Should().Contain("(Licensed)");
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task InstallLicenseAsync_MissingFile_SetsErrorStatus()
        {
            var vm = new SettingsViewModel(
                _settingsService,
                _aiClientFactory,
                _preferenceService,
                _categoryRegistry,
                _parserFactory,
                _licenseService);

            await vm.InstallLicenseAsync(@"C:\NonExistent\Fake_" + Guid.NewGuid() + ".lic");

            vm.IsAsposeLicenseStatusOpen.Should().BeTrue();
            vm.AsposeLicenseStatusSeverity.Should().Be(InfoBarSeverity.Error);
            vm.AsposeLicenseStatusMessage.Should().Contain("could not be found");
        }

        [Fact]
        public async Task RemoveLicenseAsync_ResetsLicenseAndUpdatesStatus()
        {
            _licenseService.IsLicensed = true;
            _licenseService.IsWordsLicensed = true;

            var vm = new SettingsViewModel(
                _settingsService,
                _aiClientFactory,
                _preferenceService,
                _categoryRegistry,
                _parserFactory,
                _licenseService);

            await vm.LoadDocumentEngineSettingsAsync();
            vm.IsAsposeLicensed.Should().BeTrue();

            await vm.RemoveLicenseAsync();

            vm.IsAsposeLicensed.Should().BeFalse();
            vm.CanRemoveAsposeLicense.Should().BeFalse();
            vm.IsAsposeLicenseStatusOpen.Should().BeTrue();
            vm.AsposeLicenseStatusSeverity.Should().Be(InfoBarSeverity.Informational);
            vm.AsposeLicenseStatusMessage.Should().Contain("removed");

            // Verify Word category dropdown options now show Evaluation Mode
            var wordCat = vm.DocumentCategories.FirstOrDefault(c => c.Category == DocumentCategory.Word);
            wordCat.Should().NotBeNull();
            var asposeOption = wordCat!.AvailableEngines.FirstOrDefault(e => e.EngineId == "aspose-words");
            asposeOption.Should().NotBeNull();
            asposeOption!.DisplayName.Should().Contain("(Evaluation Mode)");
        }
    }
}
