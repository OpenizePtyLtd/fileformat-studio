using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;

namespace FileFormatAIStudio.Services.Chat
{
    public interface IChatSessionService
    {
        Task<List<ChatSessionEntity>> GetSessionsAsync();
        Task<ChatSessionEntity?> GetEmptySessionAsync();
        Task<ChatSessionEntity> CreateSessionAsync(string title = "New Chat", Guid? defaultModelId = null);
        Task<ChatSessionEntity?> GetSessionAsync(Guid sessionId);
        Task UpdateSessionTitleAsync(Guid sessionId, string title);
        Task UpdateSessionModelAsync(Guid sessionId, Guid modelId);
        Task DeleteSessionAsync(Guid sessionId);
        Task<ChatMessageEntity> AddMessageAsync(Guid sessionId, string role, string content, string? citationJson = null);

        Task<List<KnowledgebaseEntity>> GetAttachedKnowledgebasesAsync(Guid sessionId);
        Task AttachKnowledgebaseAsync(Guid sessionId, Guid knowledgebaseId);
        Task DetachKnowledgebaseAsync(Guid sessionId, Guid knowledgebaseId);
    }
}

