using System;
using System.Collections.Generic;

namespace FileFormatAIStudio.Data.Entities
{
    public class ProviderConfigEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string ProviderType { get; set; } = "OpenAI"; // OpenAI, OpenRouter, Custom
        public string EndpointUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<ModelConfigEntity> Models { get; set; } = new();
    }
}


