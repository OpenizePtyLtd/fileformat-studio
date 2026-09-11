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

        [Fact]
        public void NewModelId_AutoDetectsOpenRouterEmbeddingModel()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);
            var aiClientFactory = new FakeAiClientFactory();
            var viewModel = new SettingsViewModel(settingsService, aiClientFactory);

            viewModel.NewModelTypeIndex = 0;
            viewModel.NewModelId = "liquid/lfm-2.5-embedding-350m:free";
            viewModel.NewModelTypeIndex.Should().Be(1);
        }

        [Fact]
        public async Task AddModelAsync_DuplicateModel_DoesNotDuplicateInDatabaseOrViewModel()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);
            var aiClientFactory = new FakeAiClientFactory();

            var provider = new ProviderConfigEntity
            {
                Name = "OpenRouter",
                ProviderType = "OpenRouter",
                EndpointUrl = "https://openrouter.ai/api/v1"
            };
            context.Providers.Add(provider);
            await context.SaveChangesAsync();

            var viewModel = new SettingsViewModel(settingsService, aiClientFactory);
            await viewModel.LoadProvidersAsync();
            viewModel.SelectedProvider = viewModel.Providers.First();

            viewModel.NewModelId = "liquid/lfm-2.5-embedding-350m:free";
            viewModel.NewModelDisplayName = "Liquid LFM 2.5 Embedding";
            viewModel.NewModelTypeIndex = 1;

            await viewModel.AddModelCommand.ExecuteAsync(null);

            // Try adding exact same model again
            viewModel.NewModelId = "liquid/lfm-2.5-embedding-350m:free";
            viewModel.NewModelDisplayName = "Liquid LFM 2.5 Embedding";
            viewModel.NewModelTypeIndex = 1;

            await viewModel.AddModelCommand.ExecuteAsync(null);

            var inDb = await context.Models.Where(m => m.ModelId == "liquid/lfm-2.5-embedding-350m:free").ToListAsync();
            inDb.Should().HaveCount(1);
            viewModel.SelectedProviderModels.Count(m => m.ModelId == "liquid/lfm-2.5-embedding-350m:free").Should().Be(1);
        }

        [Fact]
        public async Task DeleteModelAsync_RemovesAllDuplicateInstancesIfPresent()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);
            var aiClientFactory = new FakeAiClientFactory();

            var provider = new ProviderConfigEntity
            {
                Name = "OpenRouter",
                ProviderType = "OpenRouter",
                EndpointUrl = "https://openrouter.ai/api/v1"
            };
            context.Providers.Add(provider);
            await context.SaveChangesAsync();

            // Simulate legacy database with duplicate rows
            var m1 = new ModelConfigEntity { ProviderId = provider.Id, ModelId = "dup-embed", DisplayName = "Dup 1", IsEmbeddingModel = true };
            var m2 = new ModelConfigEntity { ProviderId = provider.Id, ModelId = "dup-embed", DisplayName = "Dup 2", IsEmbeddingModel = true };
            context.Models.AddRange(m1, m2);
            await context.SaveChangesAsync();

            var viewModel = new SettingsViewModel(settingsService, aiClientFactory);
            await viewModel.LoadProvidersAsync();
            viewModel.SelectedProvider = viewModel.Providers.First();

            await viewModel.DeleteModelCommand.ExecuteAsync(m1);

            var remaining = await context.Models.Where(m => m.ModelId == "dup-embed").ToListAsync();
            remaining.Should().BeEmpty();
            viewModel.SelectedProviderModels.Should().NotContain(m => m.ModelId == "dup-embed");
        }

        [Fact]
        public async Task TestConnectionAsync_MultipleModels_TestsAllModelsAndPasses()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);
            var aiClientFactory = new FakeAiClientFactory();

            var provider = new ProviderConfigEntity
            {
                Name = "OpenRouter",
                ProviderType = "OpenRouter",
                EndpointUrl = "https://openrouter.ai/api/v1"
            };
            context.Providers.Add(provider);
            await context.SaveChangesAsync();

            var chatModel = new ModelConfigEntity { ProviderId = provider.Id, ModelId = "openrouter/free", DisplayName = "OpenRouter Free", IsEmbeddingModel = false };
            var embedModel = new ModelConfigEntity { ProviderId = provider.Id, ModelId = "liquid/lfm-2.5-embedding-350m:free", DisplayName = "Liquid Embedding", IsEmbeddingModel = true };
            context.Models.AddRange(chatModel, embedModel);
            await context.SaveChangesAsync();

            var viewModel = new SettingsViewModel(settingsService, aiClientFactory);
            await viewModel.LoadProvidersAsync();
            viewModel.SelectedProvider = viewModel.Providers.First();

            await viewModel.TestConnectionCommand.ExecuteAsync(null);

            // Both models must have been tested
            aiClientFactory.TestedModelIds.Should().Contain("openrouter/free");
            aiClientFactory.TestedModelIds.Should().Contain("liquid/lfm-2.5-embedding-350m:free");
            viewModel.IsTestSuccess.Should().BeTrue();
            viewModel.TestStatusMessage.Should().Contain("All 2 models verified successfully");
        }

        [Fact]
        public async Task TestConnectionAsync_OneModelFails_ReportsWarningAndDetails()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);
            var aiClientFactory = new FakeAiClientFactory
            {
                ValidateResultFunc = (modelId) =>
                {
                    if (modelId == "bad-model")
                        return (false, "Model not found");
                    return (true, "OK");
                }
            };

            var provider = new ProviderConfigEntity
            {
                Name = "OpenRouter",
                ProviderType = "OpenRouter",
                EndpointUrl = "https://openrouter.ai/api/v1"
            };
            context.Providers.Add(provider);
            await context.SaveChangesAsync();

            var chatModel = new ModelConfigEntity { ProviderId = provider.Id, ModelId = "good-model", DisplayName = "Good Model", IsEmbeddingModel = false };
            var badModel = new ModelConfigEntity { ProviderId = provider.Id, ModelId = "bad-model", DisplayName = "Bad Model", IsEmbeddingModel = false };
            context.Models.AddRange(chatModel, badModel);
            await context.SaveChangesAsync();

            var viewModel = new SettingsViewModel(settingsService, aiClientFactory);
            await viewModel.LoadProvidersAsync();
            viewModel.SelectedProvider = viewModel.Providers.First();

            await viewModel.TestConnectionCommand.ExecuteAsync(null);

            viewModel.IsTestSuccess.Should().BeFalse();
            viewModel.SaveStatusSeverity.Should().Be(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning);
            viewModel.TestStatusMessage.Should().Contain("1 of 2 models verified");
            viewModel.TestStatusMessage.Should().Contain("Bad Model: Model not found");
        }

        [Fact]
        public async Task AddModelAsync_EmbeddingModel_AutoProbesDimensionsAndSavesModel()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);
            var aiClientFactory = new FakeAiClientFactory { EmbeddingDimensionsToReturn = 1024 };

            var provider = new ProviderConfigEntity
            {
                Name = "OpenRouter",
                ProviderType = "OpenRouter",
                EndpointUrl = "https://openrouter.ai/api/v1"
            };
            context.Providers.Add(provider);
            await context.SaveChangesAsync();

            var viewModel = new SettingsViewModel(settingsService, aiClientFactory);
            await viewModel.LoadProvidersAsync();
            viewModel.SelectedProvider = viewModel.Providers.First();

            viewModel.NewModelId = "liquid/lfm-2.5-embedding-350m:free";
            viewModel.NewModelDisplayName = "Liquid Embedding";
            viewModel.NewModelTypeIndex = 1; // Embedding

            await viewModel.AddModelCommand.ExecuteAsync(null);

            var savedModel = await context.Models.FirstOrDefaultAsync(m => m.ModelId == "liquid/lfm-2.5-embedding-350m:free");
            savedModel.Should().NotBeNull();
            savedModel!.IsEmbeddingModel.Should().BeTrue();
            savedModel.Dimensions.Should().Be(1024);
            aiClientFactory.TestedModelIds.Should().Contain("liquid/lfm-2.5-embedding-350m:free");
            viewModel.SaveStatusSeverity.Should().Be(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success);
        }

        [Fact]
        public async Task AddModelAsync_EmbeddingModel_WhenProbeFails_DoesNotAddModel()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);
            var aiClientFactory = new FakeAiClientFactory
            {
                ValidateResultFunc = (m) => (false, "Endpoint unreachable")
            };

            var provider = new ProviderConfigEntity
            {
                Name = "OpenRouter",
                ProviderType = "OpenRouter",
                EndpointUrl = "https://openrouter.ai/api/v1"
            };
            context.Providers.Add(provider);
            await context.SaveChangesAsync();

            var viewModel = new SettingsViewModel(settingsService, aiClientFactory);
            await viewModel.LoadProvidersAsync();
            viewModel.SelectedProvider = viewModel.Providers.First();

            viewModel.NewModelId = "broken-embed";
            viewModel.NewModelDisplayName = "Broken Embed";
            viewModel.NewModelTypeIndex = 1; // Embedding

            await viewModel.AddModelCommand.ExecuteAsync(null);

            var savedModel = await context.Models.FirstOrDefaultAsync(m => m.ModelId == "broken-embed");
            savedModel.Should().BeNull();
            viewModel.SelectedProviderModels.Should().NotContain(m => m.ModelId == "broken-embed");
            viewModel.SaveStatusSeverity.Should().Be(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);
            viewModel.SaveStatusMessage.Should().Contain("Connection test failed");
        }

        [Fact]
        public async Task AddModelAsync_ChatModel_WhenConnectionFails_DoesNotAddModel()
        {
            using var context = new AppDbContext(_options);
            var settingsService = new SettingsService(context);
            var aiClientFactory = new FakeAiClientFactory
            {
                ValidateResultFunc = (m) => (false, "Invalid API key")
            };

            var provider = new ProviderConfigEntity
            {
                Name = "OpenRouter",
                ProviderType = "OpenRouter",
                EndpointUrl = "https://openrouter.ai/api/v1"
            };
            context.Providers.Add(provider);
            await context.SaveChangesAsync();

            var viewModel = new SettingsViewModel(settingsService, aiClientFactory);
            await viewModel.LoadProvidersAsync();
            viewModel.SelectedProvider = viewModel.Providers.First();

            viewModel.NewModelId = "broken-chat";
            viewModel.NewModelDisplayName = "Broken Chat";
            viewModel.NewModelTypeIndex = 0; // Chat

            await viewModel.AddModelCommand.ExecuteAsync(null);

            var savedModel = await context.Models.FirstOrDefaultAsync(m => m.ModelId == "broken-chat");
            savedModel.Should().BeNull();
            viewModel.SelectedProviderModels.Should().NotContain(m => m.ModelId == "broken-chat");
            viewModel.SaveStatusSeverity.Should().Be(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);
            viewModel.SaveStatusMessage.Should().Contain("Connection test failed");
        }
    }
}

