using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using Microsoft.Extensions.AI;

namespace FileFormatAIStudio.Services.Chat
{
    public sealed record GroundedRagResult(
        IReadOnlyList<CitationReference> Citations,
        string GroundedSystemPrompt
    );

    public interface IChatExecutionService
    {
        IAsyncEnumerable<ChatResponseUpdate> StreamResponseAsync(
            ProviderConfigEntity provider,
            string modelId,
            IEnumerable<ChatMessage> history,
            CancellationToken ct = default);

        Task<GroundedRagResult> RetrieveGroundedContextAsync(
            string userPrompt,
            IReadOnlyList<KnowledgebaseEntity> attachedKnowledgebases,
            int topK = 5,
            float minSimilarity = 0.35f,
            CancellationToken ct = default);
    }
}

