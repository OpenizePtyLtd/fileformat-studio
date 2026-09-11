using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Settings;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class CreateKnowledgebaseViewModelTests
    {
        private readonly FakeSettingsService _settingsService;
        private readonly CreateKnowledgebaseViewModel _viewModel;

        public CreateKnowledgebaseViewModelTests()
        {
            _settingsService = new FakeSettingsService();
            _viewModel = new CreateKnowledgebaseViewModel(_settingsService);
        }

        [Fact]
        public async Task InitializeAsync_PopulatesProvidersAndDefaults()
        {
            var provider = new ProviderConfigEntity
            {
                Id = Guid.NewGuid(),
                Name = "Ollama Local",
                EndpointUrl = "http://localhost:11434/v1",
                Models = new List<ModelConfigEntity>
                {
                    new() { ModelId = "llama3.2:latest", DisplayName = "Llama 3.2", IsEmbeddingModel = false },
                    new() { ModelId = "nomic-embed-text", DisplayName = "Nomic Embed", IsEmbeddingModel = true, Dimensions = 768 }
                }
            };
            _settingsService.Providers.Add(provider);

            await _viewModel.InitializeAsync();

            _viewModel.ConfiguredProviders.Should().HaveCount(1);
            _viewModel.SelectedProvider.Should().Be(provider);
            _viewModel.AvailableEmbeddingModels.Should().HaveCount(1);
            _viewModel.SelectedEmbeddingModel.Should().NotBeNull();
            _viewModel.SelectedEmbeddingModel!.ModelId.Should().Be("nomic-embed-text");
            _viewModel.SelectedEmbeddingModel.Dimensions.Should().Be(768);
            _viewModel.SelectedParserEngine.EngineId.Should().Be("Auto");
            _viewModel.HasProviderWarning.Should().BeFalse();
        }

        [Fact]
        public void Validation_RequiresValidNameAndProviderAndModel()
        {
            _viewModel.IsValid.Should().BeFalse();

            _viewModel.Name = "   ";
            _viewModel.IsValid.Should().BeFalse();

            _viewModel.Name = "Legal Knowledgebase";
            _viewModel.IsValid.Should().BeFalse(); // Provider and Model still null

            var provider = new ProviderConfigEntity { Id = Guid.NewGuid(), Name = "Local Ollama" };
            _viewModel.SelectedProvider = provider;
            _viewModel.SelectedEmbeddingModel = new EmbeddingModelOption("nomic-embed-text", "Nomic Embed", 768);

            _viewModel.IsValid.Should().BeTrue();

            _viewModel.Name = "";
            _viewModel.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ProviderSwitching_MissingApiKeyOnCloudProvider_SetsWarning()
        {
            var cloudProvider = new ProviderConfigEntity
            {
                Id = Guid.NewGuid(),
                Name = "OpenAI Cloud",
                EndpointUrl = "https://api.openai.com/v1",
                ApiKey = "", // Missing key!
                Models = new List<ModelConfigEntity>
                {
                    new() { ModelId = "text-embedding-3-small", IsEmbeddingModel = true }
                }
            };

            _viewModel.SelectedProvider = cloudProvider;

            _viewModel.HasProviderWarning.Should().BeTrue();
            _viewModel.ProviderWarningMessage.Should().Contain("API key saved in Settings");
            _viewModel.AvailableEmbeddingModels.Should().HaveCount(1);
        }

        [Fact]
        public void ProviderSwitching_NoEmbeddingModels_SetsWarningAndNullsSelection()
        {
            var providerNoEmbeddings = new ProviderConfigEntity
            {
                Id = Guid.NewGuid(),
                Name = "Chat Only Provider",
                EndpointUrl = "http://localhost:11434/v1",
                Models = new List<ModelConfigEntity>
                {
                    new() { ModelId = "qwen2.5:7b", DisplayName = "Qwen 2.5", IsEmbeddingModel = false }
                }
            };

            _viewModel.SelectedProvider = providerNoEmbeddings;

            _viewModel.HasProviderWarning.Should().BeTrue();
            _viewModel.ProviderWarningMessage.Should().Contain("No embedding models are registered");
            _viewModel.AvailableEmbeddingModels.Should().BeEmpty();
            _viewModel.SelectedEmbeddingModel.Should().BeNull();
            _viewModel.IsValid.Should().BeFalse();
        }

        [Fact]
        public void AddFiles_AddsFilesAndPreventsDuplicates()
        {
            var file1 = new SelectedFileItemViewModel("C:\\docs\\contract.docx", 1024 * 50);
            var file2 = new SelectedFileItemViewModel("C:\\docs\\report.pdf", 1024 * 200);
            var duplicateFile1 = new SelectedFileItemViewModel("c:\\docs\\CONTRACT.DOCX", 1024 * 50);

            _viewModel.HasSelectedFiles.Should().BeFalse();
            _viewModel.HasNoSelectedFiles.Should().BeTrue();

            _viewModel.AddFiles(new[] { file1, file2, duplicateFile1 });

            _viewModel.SelectedFiles.Should().HaveCount(2);
            _viewModel.HasSelectedFiles.Should().BeTrue();
            _viewModel.HasNoSelectedFiles.Should().BeFalse();
            _viewModel.TotalFilesSummary.Should().Contain("2 documents selected");
        }

        [Fact]
        public void RemoveFile_RemovesFileAndUpdatesSummary()
        {
            var file1 = new SelectedFileItemViewModel("C:\\docs\\sheet.xlsx", 1024 * 10);
            var file2 = new SelectedFileItemViewModel("C:\\docs\\data.csv", 1024 * 20);

            _viewModel.AddFiles(new[] { file1, file2 });
            _viewModel.SelectedFiles.Should().HaveCount(2);

            _viewModel.RemoveFile(file1);

            _viewModel.SelectedFiles.Should().HaveCount(1);
            _viewModel.SelectedFiles.Should().NotContain(file1);
            _viewModel.TotalFilesSummary.Should().Contain("1 document selected");
        }

        [Fact]
        public void ClearAllFiles_ResetsFileList()
        {
            var file1 = new SelectedFileItemViewModel("C:\\docs\\1.txt", 100);
            var file2 = new SelectedFileItemViewModel("C:\\docs\\2.txt", 200);

            _viewModel.AddFiles(new[] { file1, file2 });
            _viewModel.ClearAllFiles();

            _viewModel.SelectedFiles.Should().BeEmpty();
            _viewModel.HasSelectedFiles.Should().BeFalse();
            _viewModel.HasNoSelectedFiles.Should().BeTrue();
            _viewModel.TotalFilesSummary.Should().Be("No documents selected");
        }

        [Fact]
        public void BuildCreateRequest_ValidState_BuildsCorrectRequest()
        {
            _viewModel.Name = "Finance Corpus";
            _viewModel.Description = "Financial quarterly reports";
            _viewModel.SelectedParserEngine = _viewModel.AvailableParserEngines.First(p => p.EngineId == "aspose");
            _viewModel.SelectedProvider = new ProviderConfigEntity { Name = "OpenAI" };
            _viewModel.SelectedEmbeddingModel = new EmbeddingModelOption("text-embedding-3-small", "OpenAI Small", 1536);

            var file1 = new SelectedFileItemViewModel("C:\\finance\\q1.pdf", 5000);
            var file2 = new SelectedFileItemViewModel("C:\\finance\\q2.xlsx", 10000);
            _viewModel.AddFiles(new[] { file1, file2 });

            var (request, paths) = _viewModel.BuildCreateRequest();

            request.Name.Should().Be("Finance Corpus");
            request.Description.Should().Be("Financial quarterly reports");
            request.ParserEngine.Should().Be("aspose");
            request.EmbeddingProvider.Should().Be("OpenAI");
            request.EmbeddingModel.Should().Be("text-embedding-3-small");
            request.VectorDimensions.Should().Be(1536);

            paths.Should().HaveCount(2);
            paths.Should().ContainInOrder("C:\\finance\\q1.pdf", "C:\\finance\\q2.xlsx");
        }

        [Fact]
        public void BuildCreateRequest_InvalidState_ThrowsInvalidOperationException()
        {
            _viewModel.Name = "";
            var act = () => _viewModel.BuildCreateRequest();
            act.Should().Throw<InvalidOperationException>();
        }

        [Theory]
        [InlineData(".docx", "\uE8A5")]
        [InlineData(".xlsx", "\uE9F9")]
        [InlineData(".pptx", "\uE8B7")]
        [InlineData(".pdf", "\uEA90")]
        [InlineData(".txt", "\uE8C8")]
        [InlineData(".csv", "\uE8C8")]
        [InlineData(".md", "\uE8C8")]
        public void SelectedFileItemViewModel_ResolvesGlyphs(string ext, string expectedGlyph)
        {
            var item = new SelectedFileItemViewModel($"test{ext}", 1024);
            item.FileIconGlyph.Should().Be(expectedGlyph);
            item.FormattedFileSize.Should().Be("1.0 KB");
            item.Status.Should().Be("Pending");
            item.StatusGlyph.Should().Be("\uE823");
        }

        [Fact]
        public async Task CreateAndIngestAsync_NoFiles_CreatesEmptyKnowledgebaseInstantly()
        {
            var fakeKb = new FakeKnowledgebaseService();
            var vm = new CreateKnowledgebaseViewModel(_settingsService, fakeKb);

            vm.Name = "Empty KB";
            vm.SelectedProvider = new ProviderConfigEntity { Name = "Ollama" };
            vm.SelectedEmbeddingModel = new EmbeddingModelOption("nomic-embed-text", "Nomic Embed", 768);

            bool result = await vm.CreateAndIngestAsync();

            result.Should().BeTrue();
            vm.IsCompleted.Should().BeTrue();
            vm.IsIndexing.Should().BeFalse();
            vm.CreatedKnowledgebase.Should().NotBeNull();
            vm.CreatedKnowledgebase!.Name.Should().Be("Empty KB");
            vm.IndexingPercentage.Should().Be(100.0);
            fakeKb.Knowledgebases.Should().HaveCount(1);
        }

        [Fact]
        public async Task CreateAndIngestAsync_WithFiles_UpdatesProgressAndPerDocumentStatuses()
        {
            var fakeKb = new FakeKnowledgebaseService();
            fakeKb.ProgressAction = progress =>
            {
                progress?.Report(new IndexingProgressReport
                {
                    Stage = IndexingStage.Extracting,
                    CurrentDocumentName = "doc1.pdf",
                    CurrentDocumentIndex = 1,
                    TotalDocuments = 2,
                    Percentage = 25.0,
                    Message = "Extracting doc1.pdf..."
                });
                progress?.Report(new IndexingProgressReport
                {
                    Stage = IndexingStage.DocumentCompleted,
                    CurrentDocumentName = "doc1.pdf",
                    CurrentDocumentIndex = 1,
                    TotalDocuments = 2,
                    Percentage = 50.0,
                    TotalChunksIndexed = 10,
                    Message = "Completed doc1.pdf"
                });
            };

            var vm = new CreateKnowledgebaseViewModel(_settingsService, fakeKb);
            vm.Name = "Docs KB";
            vm.SelectedProvider = new ProviderConfigEntity { Name = "OpenAI" };
            vm.SelectedEmbeddingModel = new EmbeddingModelOption("text-embedding-3-small", "Small", 1536);

            var file1 = new SelectedFileItemViewModel("doc1.pdf", 1024);
            var file2 = new SelectedFileItemViewModel("doc2.pdf", 2048);
            vm.AddFiles(new[] { file1, file2 });

            bool result = await vm.CreateAndIngestAsync();

            result.Should().BeTrue();
            vm.IsCompleted.Should().BeTrue();
            vm.IsIndexing.Should().BeFalse();
            file1.Status.Should().Be("Indexed");
            file1.StatusGlyph.Should().Be("\uE73E");
            vm.CurrentStageStep.Should().Be(4);
            vm.IsStep4Completed.Should().BeTrue();
            fakeKb.Knowledgebases.Should().HaveCount(1);
        }

        [Fact]
        public async Task CreateAndIngestAsync_WhenCancelled_SetsCancelledState()
        {
            var fakeKb = new FakeKnowledgebaseService();
            fakeKb.IngestFuncWithToken = (id, files, opt, prog, ct) =>
            {
                throw new OperationCanceledException(ct);
            };

            var vm = new CreateKnowledgebaseViewModel(_settingsService, fakeKb);
            vm.Name = "Cancelled KB";
            vm.SelectedProvider = new ProviderConfigEntity { Name = "OpenAI" };
            vm.SelectedEmbeddingModel = new EmbeddingModelOption("text-embedding-3-small", "Small", 1536);
            vm.AddFiles(new[] { new SelectedFileItemViewModel("doc.pdf", 1024) });

            bool result = await vm.CreateAndIngestAsync();

            result.Should().BeFalse();
            vm.IsCancelled.Should().BeTrue();
            vm.IsIndexing.Should().BeFalse();
            vm.IndexingMessage.Should().Contain("cancelled by user");
        }

        [Fact]
        public void StepperProperties_ReflectStageTransitions()
        {
            var vm = new CreateKnowledgebaseViewModel(_settingsService);

            // Initial: step 0
            vm.CurrentStageStep = 0;
            vm.IsStep1Active.Should().BeFalse();
            vm.IsStep1Completed.Should().BeFalse();

            // Step 1: Extracting
            vm.CurrentStageStep = 1;
            vm.IsStep1Active.Should().BeTrue();
            vm.IsStep1Completed.Should().BeFalse();
            vm.Step1ActiveVisibility.Should().Be(Microsoft.UI.Xaml.Visibility.Visible);
            vm.Step1CompletedVisibility.Should().Be(Microsoft.UI.Xaml.Visibility.Collapsed);

            // Step 2: Chunking
            vm.CurrentStageStep = 2;
            vm.IsStep1Active.Should().BeFalse();
            vm.IsStep1Completed.Should().BeTrue();
            vm.IsStep2Active.Should().BeTrue();
            vm.IsStep2Completed.Should().BeFalse();

            // Step 3: Embedding
            vm.CurrentStageStep = 3;
            vm.IsStep2Completed.Should().BeTrue();
            vm.IsStep3Active.Should().BeTrue();

            // Step 4: Storing
            vm.CurrentStageStep = 4;
            vm.IsStep3Completed.Should().BeTrue();
            vm.IsStep4Active.Should().BeTrue();

            // Completed
            vm.IsCompleted = true;
            vm.IsStep4Completed.Should().BeTrue();
            vm.Step4CompletedVisibility.Should().Be(Microsoft.UI.Xaml.Visibility.Visible);
        }
    }

    internal sealed class FakeSettingsService : ISettingsService
    {
        public List<ProviderConfigEntity> Providers { get; } = new();

        public Task<List<ProviderConfigEntity>> GetProvidersAsync() =>
            Task.FromResult(Providers.ToList());

        public Task<ProviderConfigEntity?> GetProviderByIdAsync(Guid providerId) =>
            Task.FromResult(Providers.FirstOrDefault(p => p.Id == providerId));

        public Task SaveProviderAsync(ProviderConfigEntity provider)
        {
            var existing = Providers.FirstOrDefault(p => p.Id == provider.Id);
            if (existing != null) Providers.Remove(existing);
            Providers.Add(provider);
            return Task.CompletedTask;
        }

        public Task DeleteProviderAsync(Guid providerId)
        {
            Providers.RemoveAll(p => p.Id == providerId);
            return Task.CompletedTask;
        }

        public Task AddModelAsync(ModelConfigEntity model) => Task.CompletedTask;
        public Task DeleteModelAsync(Guid modelId) => Task.CompletedTask;
        public Task<List<ModelConfigEntity>> GetAllEnabledModelsAsync() =>
            Task.FromResult(Providers.SelectMany(p => p.Models ?? new List<ModelConfigEntity>()).ToList());
        public Task RestoreDefaultProvidersAsync() => Task.CompletedTask;
    }
}

