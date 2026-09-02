using System;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using Microsoft.Extensions.AI;

namespace FileFormatAIStudio.Services.AI
{
    public interface IAIClientFactory
    {
        IChatClient CreateChatClient(ProviderConfigEntity provider, string modelId);
        Task<(bool Success, string Message)> TestConnectionAsync(ProviderConfigEntity provider, string modelId, CancellationToken ct = default);
    }
}

