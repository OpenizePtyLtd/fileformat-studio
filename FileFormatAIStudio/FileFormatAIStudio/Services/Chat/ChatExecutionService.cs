using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Knowledgebase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileFormatAIStudio.Services.Chat
{
    public class ChatExecutionService : IChatExecutionService
    {
        private readonly IAIClientFactory _aiClientFactory;
        private readonly IVectorStoreService _vectorStoreService;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ChatExecutionService> _logger;

        public ChatExecutionService(
            IAIClientFactory aiClientFactory,
            IVectorStoreService vectorStoreService,
            AppDbContext dbContext,
            ILogger<ChatExecutionService>? logger = null)
        {
            _aiClientFactory = aiClientFactory ?? throw new ArgumentNullException(nameof(aiClientFactory));
            _vectorStoreService = vectorStoreService ?? throw new ArgumentNullException(nameof(vectorStoreService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? NullLogger<ChatExecutionService>.Instance;
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

        public async Task<GroundedRagResult> RetrieveGroundedContextAsync(
            string userPrompt,
            IReadOnlyList<KnowledgebaseEntity> attachedKnowledgebases,
            int topK = 5,
            float minSimilarity = 0.35f,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userPrompt) || attachedKnowledgebases == null || attachedKnowledgebases.Count == 0 || topK <= 0)
            {
                return new GroundedRagResult(Array.Empty<CitationReference>(), string.Empty);
            }

            var allScoredChunks = new List<ScoredChunkResult>();

            // Group attached KBs by (EmbeddingProvider, EmbeddingModel) so each unique embedding model is queried with its matching query vector
            var groups = attachedKnowledgebases
                .Where(kb => !string.IsNullOrWhiteSpace(kb.EmbeddingModel))
                .GroupBy(kb => (Provider: kb.EmbeddingProvider.Trim(), Model: kb.EmbeddingModel.Trim()));

            // Load all providers once to avoid multiple round-trips
            var providers = await _dbContext.Providers.AsNoTracking().ToListAsync(ct);

            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();

                var provider = providers.FirstOrDefault(p =>
                    string.Equals(p.Name, group.Key.Provider, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.ProviderType, group.Key.Provider, StringComparison.OrdinalIgnoreCase));

                if (provider == null)
                {
                    _logger.LogWarning("Embedding provider '{Provider}' not found for model '{Model}'. Skipping group.", group.Key.Provider, group.Key.Model);
                    continue;
                }

                try
                {
                    using var generator = _aiClientFactory.CreateEmbeddingGenerator(provider, group.Key.Model);
                    var embeddings = await generator.GenerateAsync(new[] { userPrompt }, cancellationToken: ct);

                    if (embeddings == null || embeddings.Count == 0)
                    {
                        continue;
                    }

                    var queryVector = embeddings[0].Vector;
                    var groupKbIds = group.Select(k => k.Id).ToList();

                    var matchedChunks = await _vectorStoreService.SearchAsync(
                        queryVector,
                        groupKbIds,
                        topK,
                        minSimilarity,
                        ct);

                    allScoredChunks.AddRange(matchedChunks);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to generate query embeddings or search vector store for provider '{Provider}', model '{Model}'.", group.Key.Provider, group.Key.Model);
                }
            }

            if (allScoredChunks.Count == 0)
            {
                return new GroundedRagResult(Array.Empty<CitationReference>(), string.Empty);
            }

            // Rank by highest similarity score and select Top-K overall
            var topChunks = allScoredChunks
                .OrderByDescending(c => c.Score)
                .Take(topK)
                .ToList();

            var citations = new List<CitationReference>(topChunks.Count);
            for (int i = 0; i < topChunks.Count; i++)
            {
                var chunk = topChunks[i];
                var kbName = attachedKnowledgebases.FirstOrDefault(k => k.Id == chunk.KnowledgebaseId)?.Name ?? "Knowledgebase";

                citations.Add(new CitationReference
                {
                    Index = i + 1,
                    ChunkId = chunk.ChunkId,
                    DocumentId = chunk.DocumentId,
                    KnowledgebaseId = chunk.KnowledgebaseId,
                    KnowledgebaseName = kbName,
                    DocumentName = chunk.SourceFileName,
                    PageOrSectionNumber = chunk.PageOrSectionNumber,
                    Snippet = chunk.TextContent,
                    Score = chunk.Score
                });
            }

            // Build grounded system prompt
            var sb = new StringBuilder();
            sb.AppendLine("You are a helpful and precise assistant with access to the following reference documents from the user's knowledgebases.");
            sb.AppendLine("Answer the user's inquiry based on the context provided below.");
            sb.AppendLine("When citing facts or quoting information from the sources, reference the source index in square brackets, such as [1], [2], etc.");
            sb.AppendLine("If the answer cannot be found in the provided sources, state that clearly while providing helpful assistance.");
            sb.AppendLine();
            sb.AppendLine("--- BEGIN REFERENCE SOURCES ---");
            foreach (var citation in citations)
            {
                sb.AppendLine($"[{citation.Index}] Source: {citation.DocumentName} (Page/Section {citation.PageOrSectionNumber}, Knowledge Base: {citation.KnowledgebaseName})");
                sb.AppendLine(citation.Snippet);
                sb.AppendLine();
            }
            sb.AppendLine("--- END REFERENCE SOURCES ---");

            return new GroundedRagResult(citations, sb.ToString());
        }
    }
}

