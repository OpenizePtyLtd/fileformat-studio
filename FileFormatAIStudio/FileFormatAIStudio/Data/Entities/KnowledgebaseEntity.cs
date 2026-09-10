using System;
using System.Collections.Generic;

namespace FileFormatAIStudio.Data.Entities
{
    public class KnowledgebaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ParserEngine { get; set; } = "Auto"; // "Auto", "Aspose", "DotNetOss", "NodeJs"
        public string EmbeddingProvider { get; set; } = string.Empty; // e.g. "OpenAI", "Ollama"
        public string EmbeddingModel { get; set; } = string.Empty; // e.g. "text-embedding-3-small", "nomic-embed-text"
        public int VectorDimensions { get; set; } = 1536;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public List<KnowledgebaseDocumentEntity> Documents { get; set; } = new();
        public List<DocumentChunkEntity> Chunks { get; set; } = new();
        public List<SessionKnowledgebaseEntity> SessionKnowledgebases { get; set; } = new();
    }
}

