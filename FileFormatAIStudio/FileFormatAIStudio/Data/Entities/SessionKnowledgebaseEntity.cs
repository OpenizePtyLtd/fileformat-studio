using System;

namespace FileFormatAIStudio.Data.Entities
{
    public class SessionKnowledgebaseEntity
    {
        public Guid SessionId { get; set; }
        public Guid KnowledgebaseId { get; set; }
        public DateTime AttachedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ChatSessionEntity Session { get; set; } = null!;
        public KnowledgebaseEntity Knowledgebase { get; set; } = null!;
    }
}

