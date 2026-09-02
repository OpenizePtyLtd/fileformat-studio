using System;

namespace FileFormatAIStudio.Data.Entities
{
    public class ModelConfigEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProviderId { get; set; }
        public string ModelId { get; set; } = string.Empty; // e.g. "gpt-4o", "openai/gpt-4o-mini", "llama3.2"
        public string DisplayName { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsEmbeddingModel { get; set; }

        public ProviderConfigEntity? Provider { get; set; }
    }
}

