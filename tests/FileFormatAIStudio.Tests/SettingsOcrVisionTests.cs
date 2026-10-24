using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Ocr;
using FileFormatAIStudio.Services.Parsing;
using FileFormatAIStudio.Services.Parsing.Engines.Image;
using FileFormatAIStudio.Services.Settings;
using FileFormatAIStudio.Tests.TestHelpers;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class SettingsOcrVisionTests : IDisposable
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

        public SettingsOcrVisionTests()
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
                new ImageDocumentParser(new FakeOcrEngine(true, new[] { "en-US" }))
            });

            _preferenceService = new DocumentEnginePreferenceService(_context, _categoryRegistry, _parserFactory);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public void SettingsViewModel_HasDefaultOcrAndVisionSettings()
        {
            // Arrange & Act
            var vm = new SettingsViewModel(
                _settingsService,
                _aiClientFactory,
                _preferenceService,
                _categoryRegistry,
                _parserFactory,
                _licenseService);

            // Assert
            vm.IsOcrEnabled.Should().BeTrue();
            vm.IsVisionEnabled.Should().BeFalse();
            vm.MinImageDimension.Should().Be(100.0);
            vm.OcrLanguageStatus.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void InitializeOcrSettings_WhenOcrEngineHasLanguages_UpdatesStatus()
        {
            // Arrange
            var fakeOcr = new FakeOcrEngine(true, new[] { "en-US", "de-DE" });

            var vm = new SettingsViewModel(
                _settingsService,
                _aiClientFactory,
                _preferenceService,
                _categoryRegistry,
                _parserFactory,
                _licenseService,
                fakeOcr);

            // Act
            vm.InitializeOcrSettings();

            // Assert
            vm.OcrLanguageStatus.Should().Contain("2 language pack(s)");
            vm.OcrLanguageStatus.Should().Contain("en-US");
            vm.OcrLanguageStatus.Should().Contain("de-DE");
        }

        [Fact]
        public void InitializeOcrSettings_WhenOcrEngineHasNoLanguages_ReportsNotFound()
        {
            // Arrange
            var fakeOcr = new FakeOcrEngine(false, Array.Empty<string>());

            var vm = new SettingsViewModel(
                _settingsService,
                _aiClientFactory,
                _preferenceService,
                _categoryRegistry,
                _parserFactory,
                _licenseService,
                fakeOcr);

            // Act
            vm.InitializeOcrSettings();

            // Assert
            vm.OcrLanguageStatus.Should().Contain("No Windows OCR language pack found");
        }

        [Fact]
        public async Task LoadDocumentEngineSettingsAsync_IncludesImageCategory()
        {
            // Arrange
            var fakeOcr = new FakeOcrEngine(true, new[] { "en-US" });

            var vm = new SettingsViewModel(
                _settingsService,
                _aiClientFactory,
                _preferenceService,
                _categoryRegistry,
                _parserFactory,
                _licenseService,
                fakeOcr);

            // Act
            await vm.LoadDocumentEngineSettingsAsync();

            // Assert
            var imageCategory = vm.DocumentCategories.Should().Contain(c => c.Category == DocumentCategory.Image).Which;
            imageCategory.IconGlyph.Should().Be("\uEB9F");
            imageCategory.CategoryName.Should().Be("Images & Scans");
        }

        [Fact]
        public void SettingsViewModel_CanToggleOcrAndVision()
        {
            // Arrange
            var vm = new SettingsViewModel(
                _settingsService,
                _aiClientFactory);

            // Act
            vm.IsOcrEnabled = false;
            vm.IsVisionEnabled = true;
            vm.MinImageDimension = 150;

            // Assert
            vm.IsOcrEnabled.Should().BeFalse();
            vm.IsVisionEnabled.Should().BeTrue();
            vm.MinImageDimension.Should().Be(150);
        }

        private class FakeOcrEngine : IOcrEngine
        {
            public FakeOcrEngine(bool isAvailable, IReadOnlyList<string> supportedLanguages)
            {
                IsAvailable = isAvailable;
                SupportedLanguages = supportedLanguages;
            }

            public string EngineId => "fake-ocr";
            public string DisplayName => "Fake OCR Engine";
            public bool IsAvailable { get; }
            public IReadOnlyList<string> SupportedLanguages { get; }

            public Task<OcrResult> RecognizeTextAsync(Stream imageStream, string? languageTag = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new OcrResult("Extracted text", Array.Empty<OcrLine>(), null, languageTag ?? "en-US"));
            }

            public Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string? languageTag = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new OcrResult("Extracted text", Array.Empty<OcrLine>(), null, languageTag ?? "en-US"));
            }
        }
    }
}

