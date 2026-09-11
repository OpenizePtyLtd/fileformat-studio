using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileFormatAIStudio.Services.Chat
{
    public class ChatSessionService : IChatSessionService
    {
        private readonly AppDbContext _context;

        public ChatSessionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ChatSessionEntity>> GetSessionsAsync()
        {
            return await _context.Sessions
                .Include(s => s.Messages)
                .OrderByDescending(s => s.UpdatedAt)
                .ToListAsync();
        }

        public async Task<ChatSessionEntity?> GetEmptySessionAsync()
        {
            return await _context.Sessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Messages.Count == 0);
        }

        public async Task<ChatSessionEntity> CreateSessionAsync(string title = "New Chat", Guid? defaultModelId = null)
        {
            // If an empty session already exists, return it instead of creating duplicate empty sessions
            var emptySession = await GetEmptySessionAsync();
            if (emptySession != null)
            {
                return emptySession;
            }

            var session = new ChatSessionEntity
            {
                Title = title,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SelectedModelId = defaultModelId
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<ChatSessionEntity?> GetSessionAsync(Guid sessionId)
        {
            return await _context.Sessions
                .Include(s => s.Messages.OrderBy(m => m.Timestamp))
                .Include(s => s.SessionKnowledgebases)
                    .ThenInclude(sk => sk.Knowledgebase)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
        }

        public async Task UpdateSessionTitleAsync(Guid sessionId, string title)
        {
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session != null)
            {
                session.Title = title;
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateSessionModelAsync(Guid sessionId, Guid modelId)
        {
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session != null)
            {
                session.SelectedModelId = modelId;
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteSessionAsync(Guid sessionId)
        {
            var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session != null)
            {
                _context.Sessions.Remove(session);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<ChatMessageEntity> AddMessageAsync(Guid sessionId, string role, string content, string? citationJson = null)
        {
            var message = new ChatMessageEntity
            {
                SessionId = sessionId,
                Role = role,
                Content = content,
                CitationJson = citationJson,
                Timestamp = DateTime.UtcNow
            };

            _context.Messages.Add(message);

            var session = await _context.Sessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session != null)
            {
                session.UpdatedAt = DateTime.UtcNow;
                session.Messages.Add(message);

                // Auto-generate title from first user message if still default
                if (session.Title == "New Chat" && role == "User")
                {
                    session.Title = content.Length > 30 ? content.Substring(0, 30) + "..." : content;
                }
            }

            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<List<KnowledgebaseEntity>> GetAttachedKnowledgebasesAsync(Guid sessionId)
        {
            return await _context.SessionKnowledgebases
                .Where(sk => sk.SessionId == sessionId)
                .Include(sk => sk.Knowledgebase)
                .Select(sk => sk.Knowledgebase)
                .OrderBy(k => k.Name)
                .ToListAsync();
        }

        public async Task AttachKnowledgebaseAsync(Guid sessionId, Guid knowledgebaseId)
        {
            var exists = await _context.SessionKnowledgebases
                .AnyAsync(sk => sk.SessionId == sessionId && sk.KnowledgebaseId == knowledgebaseId);

            if (!exists)
            {
                var link = new SessionKnowledgebaseEntity
                {
                    SessionId = sessionId,
                    KnowledgebaseId = knowledgebaseId,
                    AttachedAt = DateTime.UtcNow
                };

                _context.SessionKnowledgebases.Add(link);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DetachKnowledgebaseAsync(Guid sessionId, Guid knowledgebaseId)
        {
            var link = await _context.SessionKnowledgebases
                .FirstOrDefaultAsync(sk => sk.SessionId == sessionId && sk.KnowledgebaseId == knowledgebaseId);

            if (link != null)
            {
                _context.SessionKnowledgebases.Remove(link);
                await _context.SaveChangesAsync();
            }
        }
    }
}

