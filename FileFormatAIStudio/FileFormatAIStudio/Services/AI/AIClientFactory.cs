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
        public IChatClient CreateChatClient(ProviderConfigEntity provider, string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Model ID cannot be empty.", nameof(modelId));
            }

            string apiKey = string.IsNullOrWhiteSpace(provider.ApiKey) ? "sk-local-no-key-required" : provider.ApiKey;
            var credentials = new ApiKeyCredential(apiKey);

            var options = new OpenAIClientOptions();

            if (!string.IsNullOrWhiteSpace(provider.EndpointUrl) &&
                Uri.TryCreate(provider.EndpointUrl, UriKind.Absolute, out var endpointUri))
            {
                options.Endpoint = endpointUri;
            }

            var openAiClient = new OpenAIClient(credentials, options);

            // Converts the OpenAI ChatClient to an IChatClient
            return openAiClient.GetChatClient(modelId).AsIChatClient();
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(ProviderConfigEntity provider, string modelId, CancellationToken ct = default)
        {
            try
            {
                using var client = CreateChatClient(provider, modelId);
                var response = await client.GetResponseAsync(new[]
                {
                    new ChatMessage(ChatRole.User, "Respond with only the single word: OK")
                }, null, ct);

                string reply = response?.Text ?? string.Empty;
                return (true, $"Connection successful! Response: {reply.Trim()}");
            }
            catch (Exception ex)
            {
                return (false, $"Connection failed: {ex.Message}");
            }
        }
    }
}

