using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using Microsoft.Extensions.AI;

namespace FileFormatAIStudio.Services.Chat
{
    public class ChatExecutionService : IChatExecutionService
    {
        private readonly IAIClientFactory _aiClientFactory;

        public ChatExecutionService(IAIClientFactory aiClientFactory)
        {
            _aiClientFactory = aiClientFactory;
        }

        public async IAsyncEnumerable<ChatResponseUpdate> StreamResponseAsync(
            ProviderConfigEntity provider,
            string modelId,
            IEnumerable<ChatMessage> history,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            using var client = _aiClientFactory.CreateChatClient(provider, modelId);
            
            await foreach (var update in client.GetStreamingResponseAsync(history, null, ct))
            {
                yield return update;
            }
        }
    }
}

