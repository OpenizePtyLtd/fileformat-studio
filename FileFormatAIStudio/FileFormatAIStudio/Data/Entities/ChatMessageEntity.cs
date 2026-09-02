using System;

namespace FileFormatAIStudio.Data.Entities
{
    public class ChatMessageEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public string Role { get; set; } = "User"; // "User" or "Assistant" or "System"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int TokenCount { get; set; }

        public ChatSessionEntity? Session { get; set; }
    }
}

