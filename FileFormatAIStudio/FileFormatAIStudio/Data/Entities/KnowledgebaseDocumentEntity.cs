using System;
using System.Collections.Generic;

namespace FileFormatAIStudio.Data.Entities
{
    public class KnowledgebaseDocumentEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid KnowledgebaseId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Status { get; set; } = "Pending"; // "Pending", "Processing", "Indexed", "Failed"
        public string? ParserEngineUsed { get; set; }
        public string? ErrorMessage { get; set; }
        public int ChunkCount { get; set; }
        public string? RawExtractedText { get; set; }
        public DateTime? IndexedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public KnowledgebaseEntity Knowledgebase { get; set; } = null!;
        public List<DocumentChunkEntity> Chunks { get; set; } = new();
    }
}

