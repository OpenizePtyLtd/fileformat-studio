using System;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class EmbeddingPipelineTests
    {
        [Theory]
        [InlineData("text-embedding-3-small", 1536)]
        [InlineData("text-embedding-3-large", 3072)]
        [InlineData("text-embedding-ada-002", 1536)]
        [InlineData("nomic-embed-text", 768)]
        [InlineData("bge-m3", 1024)]
        [InlineData("bge-small-en-v1.5", 384)]
        [InlineData("all-minilm", 384)]
        [InlineData("mxbai-embed-large", 1024)]
        [InlineData("ollama/nomic-embed-text:latest", 768)]
        public void EmbeddingModelMetadata_ResolvesKnownModelDimensions(string modelId, int expectedDimensions)
        {
            var dimensions = EmbeddingModelMetadata.GetKnownDimensions(modelId);
            dimensions.Should().Be(expectedDimensions);
            EmbeddingModelMetadata.IsEmbeddingModel(modelId).Should().BeTrue();
        }

        [Theory]
        [InlineData("gpt-4o")]
        [InlineData("llama3.2")]
        [InlineData("claude-3-5-sonnet")]
        [InlineData("custom-unknown-model")]
        public void EmbeddingModelMetadata_ReturnsNullOrFalse_ForNonEmbeddingModels(string modelId)
        {
            EmbeddingModelMetadata.GetKnownDimensions(modelId).Should().BeNull();
            EmbeddingModelMetadata.IsEmbeddingModel(modelId).Should().BeFalse();
            EmbeddingModelMetadata.GetDefaultDimensions(modelId).Should().Be(1536);
        }

        [Fact]
        public void EmbeddingModelMetadata_RecommendedModels_ContainsCloudAndLocalOptions()
        {
            var recommended = EmbeddingModelMetadata.RecommendedModels;

            recommended.Should().NotBeEmpty();
            recommended.Should().Contain(m => m.ProviderType == "OpenAI" && m.ModelId == "text-embedding-3-small");
            recommended.Should().Contain(m => m.ProviderType == "Ollama" && m.ModelId == "nomic-embed-text");
            recommended.Should().Contain(m => m.ProviderType == "Ollama" && m.ModelId == "bge-m3");
        }

        [Fact]
        public void CreateEmbeddingGenerator_ThrowsOnNullOrEmptyArguments()
        {
            var factory = new AIClientFactory();
            var validProvider = new ProviderConfigEntity { Name = "OpenAI", ApiKey = "sk-test" };

            Action nullProviderAction = () => factory.CreateEmbeddingGenerator(null!, "text-embedding-3-small");
            nullProviderAction.Should().Throw<ArgumentNullException>();

            Action emptyModelAction = () => factory.CreateEmbeddingGenerator(validProvider, "");
            emptyModelAction.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CreateEmbeddingGenerator_ReturnsValidGenerator_ForCloudAndLocal()
        {
            var factory = new AIClientFactory();

            var openAiProvider = new ProviderConfigEntity
            {
                Name = "OpenAI",
                ProviderType = "OpenAI",
                ApiKey = "sk-test-key-12345"
            };

            var ollamaProvider = new ProviderConfigEntity
            {
                Name = "Local Ollama",
                ProviderType = "Custom",
                EndpointUrl = "http://localhost:11434/v1",
                ApiKey = ""
            };

            var cloudGen = factory.CreateEmbeddingGenerator(openAiProvider, "text-embedding-3-small");
            cloudGen.Should().NotBeNull();
            cloudGen.Should().BeAssignableTo<IEmbeddingGenerator<string, Embedding<float>>>();

            var localGen = factory.CreateEmbeddingGenerator(ollamaProvider, "nomic-embed-text");
            localGen.Should().NotBeNull();
            localGen.Should().BeAssignableTo<IEmbeddingGenerator<string, Embedding<float>>>();
        }

        [Fact]
        public async Task TestEmbeddingGenerationAsync_HandlesInvalidHost_GracefullyWithoutThrowing()
        {
            var factory = new AIClientFactory();

            var offlineProvider = new ProviderConfigEntity
            {
                Name = "Unreachable Provider",
                EndpointUrl = "http://127.0.0.1:54321/v1",
                ApiKey = "test"
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var (success, message, dimensions) = await factory.TestEmbeddingGenerationAsync(
                offlineProvider, "text-embedding-3-small", cts.Token);

            success.Should().BeFalse();
            dimensions.Should().Be(0);
            message.Should().NotBeNullOrWhiteSpace();
        }
    }
}

