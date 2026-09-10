using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Settings;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class SettingsModelClassificationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public SettingsModelClassificationTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        [Fact]
        public async Task AddModelAsync_ChatModel_SetsIsEmbeddingModelFalse()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);
            var aiClientFactory = new FakeAiClientFactory();

            var provider = new ProviderConfigEntity
            {
                Name = "Local Provider",
                ProviderType = "Custom",
                EndpointUrl = "http://localhost:8000/v1"
            };
            context.Providers.Add(provider);
            await context.SaveChangesAsync();

            var viewModel = new SettingsViewModel(settingsService, aiClientFactory);
            await viewModel.LoadProvidersAsync();
            viewModel.SelectedProvider = viewModel.Providers.First();

            viewModel.NewModelId = "gptoss-120b";
            viewModel.NewModelDisplayName = "GPT OSS 120B";
            viewModel.NewModelTypeIndex = 0; // Chat

            await viewModel.AddModelCommand.ExecuteAsync(null);

            var savedModel = await context.Models.FirstOrDefaultAsync(m => m.ModelId == "gptoss-120b");
            savedModel.Should().NotBeNull();
            savedModel!.IsEmbeddingModel.Should().BeFalse();
        }

        [Fact]
        public async Task AddModelAsync_EmbeddingModel_SetsIsEmbeddingModelTrue()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);
            var aiClientFactory = new FakeAiClientFactory();

            var provider = new ProviderConfigEntity
            {
                Name = "Ollama Local",
                ProviderType = "Custom",
                EndpointUrl = "http://localhost:11434/v1"
            };
            context.Providers.Add(provider);
            await context.SaveChangesAsync();

            var viewModel = new SettingsViewModel(settingsService, aiClientFactory);
            await viewModel.LoadProvidersAsync();
            viewModel.SelectedProvider = viewModel.Providers.First();

            viewModel.NewModelId = "custom-embed";
            viewModel.NewModelDisplayName = "Custom Local Embed";
            viewModel.NewModelTypeIndex = 1; // Embedding

            await viewModel.AddModelCommand.ExecuteAsync(null);

            var savedModel = await context.Models.FirstOrDefaultAsync(m => m.ModelId == "custom-embed");
            savedModel.Should().NotBeNull();
            savedModel!.IsEmbeddingModel.Should().BeTrue();
        }

        [Fact]
        public void NewModelId_AutoDetectsKnownEmbeddingModels()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);
            var aiClientFactory = new FakeAiClientFactory();
            var viewModel = new SettingsViewModel(settingsService, aiClientFactory);

            viewModel.NewModelTypeIndex = 0;
            viewModel.NewModelId = "nomic-embed-text";
            viewModel.NewModelTypeIndex.Should().Be(1);

            viewModel.NewModelId = "gpt-4o";
            viewModel.NewModelTypeIndex = 0;

            viewModel.NewModelId = "bge-m3";
            viewModel.NewModelTypeIndex.Should().Be(1);
        }

        [Fact]
        public async Task GetAllEnabledModelsAsync_ExcludesEmbeddingModelsFromChat()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);

            var provider = new ProviderConfigEntity
            {
                Name = "MultiModel Provider",
                IsEnabled = true
            };
            context.Providers.Add(provider);
            await context.SaveChangesAsync();

            var chatModel = new ModelConfigEntity
            {
                ProviderId = provider.Id,
                ModelId = "llama-3.2",
                DisplayName = "Llama 3.2",
                IsEmbeddingModel = false
            };
            var embedModel = new ModelConfigEntity
            {
                ProviderId = provider.Id,
                ModelId = "nomic-embed-text",
                DisplayName = "Nomic Embed",
                IsEmbeddingModel = true
            };
            context.Models.AddRange(chatModel, embedModel);
            await context.SaveChangesAsync();

            var enabledModels = await settingsService.GetAllEnabledModelsAsync();

            enabledModels.Should().Contain(m => m.ModelId == "llama-3.2");
            enabledModels.Should().NotContain(m => m.ModelId == "nomic-embed-text");
        }
    }
}

