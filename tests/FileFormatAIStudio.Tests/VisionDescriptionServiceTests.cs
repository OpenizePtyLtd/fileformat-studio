using System;
using System.IO;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Security;
using FileFormatAIStudio.Services.Settings;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class VisionDescriptionServiceTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly AppDbContext _context;
        private readonly SettingsService _settingsService;
        private readonly AIClientFactory _clientFactory;
        private readonly VisionDescriptionService _service;

        public VisionDescriptionServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"FFTest_Vision_{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_dbPath}")
                .Options;

            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();

            _settingsService = new SettingsService(_context);
            _clientFactory = new AIClientFactory();

            _service = new VisionDescriptionService(_context, _settingsService, _clientFactory);
        }

        public void Dispose()
        {
            _context.Dispose();
            if (File.Exists(_dbPath))
            {
                try { File.Delete(_dbPath); } catch { }
            }
        }

        [Fact]
        public async Task IsVisionEnabledAsync_DefaultsToFalse()
        {
            var isEnabled = await _service.IsVisionEnabledAsync();
            isEnabled.Should().BeFalse();
        }

        [Fact]
        public async Task DescribeImageAsync_WhenDisabled_ShouldReturnEmptyString()
        {
            byte[] dummyImage = new byte[] { 1, 2, 3, 4 };
            var result = await _service.DescribeImageAsync(dummyImage);
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task DescribeImageAsync_WithNullOrEmptyBytes_ShouldReturnEmptyString()
        {
            var resultNull = await _service.DescribeImageAsync(null!);
            resultNull.Should().BeEmpty();

            var resultEmpty = await _service.DescribeImageAsync(Array.Empty<byte>());
            resultEmpty.Should().BeEmpty();
        }

        [Fact]
        public async Task IsVisionEnabledAsync_WhenEnabledInSettings_ShouldReturnTrue()
        {
            _context.AppSettings.Add(new AppSettingEntity
            {
                Key = VisionDescriptionService.VisionSettingKey,
                Value = "true",
                UpdatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var isEnabled = await _service.IsVisionEnabledAsync();
            isEnabled.Should().BeTrue();
        }
    }
}

