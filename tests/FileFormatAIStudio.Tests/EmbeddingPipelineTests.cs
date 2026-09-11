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
        [InlineData("text-embedding-3-small", true)]
        [InlineData("nomic-embed-text", true)]
        [InlineData("liquid/lfm-2.5-embedding-350m:free", true)]
        [InlineData("bge-small-en-v1.5", true)]
        [InlineData("gpt-4o", false)]
        [InlineData("llama3.2", false)]
        [InlineData("claude-3-5-sonnet", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void EmbeddingModelMetadata_IsEmbeddingModel_ChecksNamePattern(string? modelId, bool expected)
        {
            EmbeddingModelMetadata.IsEmbeddingModel(modelId).Should().Be(expected);
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

