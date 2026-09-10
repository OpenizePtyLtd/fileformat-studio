using System;
using System.ClientModel;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using Microsoft.Extensions.AI;
using OpenAI;

namespace FileFormatAIStudio.Services.AI
{
    public class AIClientFactory : IAIClientFactory
    {
        private static OpenAIClient CreateOpenAIClient(ProviderConfigEntity provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            string apiKey = string.IsNullOrWhiteSpace(provider.ApiKey) ? "sk-local-no-key-required" : provider.ApiKey;
            var credentials = new ApiKeyCredential(apiKey);

            var options = new OpenAIClientOptions();

            if (!string.IsNullOrWhiteSpace(provider.EndpointUrl) &&
                Uri.TryCreate(provider.EndpointUrl, UriKind.Absolute, out var endpointUri))
            {
                options.Endpoint = endpointUri;
            }

            return new OpenAIClient(credentials, options);
        }

        public IChatClient CreateChatClient(ProviderConfigEntity provider, string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Model ID cannot be empty.", nameof(modelId));
            }

            var openAiClient = CreateOpenAIClient(provider);
            return openAiClient.GetChatClient(modelId).AsIChatClient();
        }

        public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(ProviderConfigEntity provider, string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Model ID cannot be empty.", nameof(modelId));
            }

            var openAiClient = CreateOpenAIClient(provider);
            return openAiClient.GetEmbeddingClient(modelId).AsIEmbeddingGenerator();
        }

        public async Task<(bool Success, string Message, int Dimensions)> TestEmbeddingGenerationAsync(
            ProviderConfigEntity provider, string modelId, CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(15));

                using var generator = CreateEmbeddingGenerator(provider, modelId);
                var response = await generator.GenerateAsync(["FileFormat AI Studio embedding test"], cancellationToken: cts.Token);

                if (response == null || response.Count == 0 || response[0].Vector.IsEmpty)
                {
                    return (false, $"Model '{modelId}' returned an empty embedding response.", 0);
                }

                int dimensions = response[0].Vector.Length;
                return (true, $"Embedding test successful! Model '{modelId}' generated a {dimensions}-dimensional vector.", dimensions);
            }
            catch (TaskCanceledException)
            {
                return (false, "Embedding generation timed out after 15 seconds. Please verify host responsiveness.", 0);
            }
            catch (ClientResultException cre)
            {
                if (cre.Status == 401)
                {
                    return (false, "Authentication failed (401 Unauthorized): The provided API key is invalid or expired.", 0);
                }
                if (cre.Status == 404)
                {
                    return (false, $"Endpoint or embedding model not found (404): Ensure model ID '{modelId}' exists on this provider.", 0);
                }
                return (false, $"API Error (HTTP {cre.Status}): {cre.Message}", 0);
            }
            catch (Exception ex)
            {
                return (false, $"Embedding test failed: {ex.Message}", 0);
            }
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(ProviderConfigEntity provider, string modelId, CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(15));

                using var client = CreateChatClient(provider, modelId);
                var response = await client.GetResponseAsync(new[]
                {
                    new ChatMessage(ChatRole.User, "Respond with only the single word: OK")
                }, null, cts.Token);

                string reply = response?.Text ?? string.Empty;
                return (true, $"Connection successful! Model '{modelId}' responded: {reply.Trim()}");
            }
            catch (TaskCanceledException)
            {
                return (false, "Connection timed out after 15 seconds. Please verify host and network responsiveness.");
            }
            catch (ClientResultException cre)
            {
                if (cre.Status == 401)
                {
                    return (false, "Authentication failed (401 Unauthorized): The provided API key is invalid or expired.");
                }
                if (cre.Status == 404)
                {
                    return (false, $"Endpoint or model not found (404): Ensure the model ID '{modelId}' and endpoint URL are correct.");
                }
                return (false, $"API Error (HTTP {cre.Status}): {cre.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Connection failed: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> ValidateProviderAsync(ProviderConfigEntity provider, string? modelId = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(provider.EndpointUrl) ||
                !Uri.TryCreate(provider.EndpointUrl.Trim(), UriKind.Absolute, out var endpointUri) ||
                (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
            {
                return (false, "Endpoint URL must be a valid HTTP or HTTPS address.");
            }

            string? targetModelId = !string.IsNullOrWhiteSpace(modelId)
                ? modelId
                : provider.Models != null && provider.Models.Count > 0 ? provider.Models[0].ModelId : null;

            // If a model is available, validate via chat completion ping
            if (!string.IsNullOrWhiteSpace(targetModelId))
            {
                return await TestConnectionAsync(provider, targetModelId, ct);
            }

            // If no model is configured yet, test provider connectivity via GET /models
            try
            {
                using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(12) };
                string modelsUrl = $"{endpointUri.ToString().TrimEnd('/')}/models";
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, modelsUrl);

                if (!string.IsNullOrWhiteSpace(provider.ApiKey))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", provider.ApiKey.Trim());
                }

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linkedCts.CancelAfter(TimeSpan.FromSeconds(12));

                using var response = await httpClient.SendAsync(request, linkedCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "Connection successful! Provider endpoint and API key verified.");
                }

                int statusCode = (int)response.StatusCode;
                if (statusCode == 401)
                {
                    return (false, "Authentication failed (401 Unauthorized): The provided API key is invalid or unauthorized.");
                }
                if (statusCode == 403)
                {
                    return (false, "Access forbidden (403 Forbidden): The API key does not have access to this resource.");
                }
                if (statusCode == 404)
                {
                    return (false, "Endpoint returned 404 Not Found. Please verify the URL path (e.g. ensure '/v1' is included).");
                }

                return (false, $"Provider returned HTTP error {statusCode} ({response.ReasonPhrase}).");
            }
            catch (TaskCanceledException)
            {
                return (false, "Connection timed out after 12 seconds. Check the URL and server responsiveness.");
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                return (false, $"Network connection failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Validation failed: {ex.Message}");
            }
        }
    }
}

