using System;

namespace FileFormatAIStudio.Data.Entities
{
    public class DocumentChunkEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DocumentId { get; set; }
        public Guid KnowledgebaseId { get; set; }
        public string TextContent { get; set; } = string.Empty;
        public string SourceFileName { get; set; } = string.Empty;
        public int PageOrSectionNumber { get; set; }
        public int ChunkIndex { get; set; }
        public int TokenCount { get; set; }
        public byte[] EmbeddingVector { get; set; } = [];
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public KnowledgebaseDocumentEntity Document { get; set; } = null!;
        public KnowledgebaseEntity Knowledgebase { get; set; } = null!;
    }
}

