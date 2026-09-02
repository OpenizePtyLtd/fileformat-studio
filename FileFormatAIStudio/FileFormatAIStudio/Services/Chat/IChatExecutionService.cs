using System.Collections.Generic;
using System.Threading;
using FileFormatAIStudio.Data.Entities;
using Microsoft.Extensions.AI;

namespace FileFormatAIStudio.Services.Chat
{
    public interface IChatExecutionService
    {
        IAsyncEnumerable<ChatResponseUpdate> StreamResponseAsync(
            ProviderConfigEntity provider,
            string modelId,
            IEnumerable<ChatMessage> history,
            CancellationToken ct = default);
    }
}

