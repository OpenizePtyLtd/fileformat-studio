namespace FileFormatAIStudio.Services.AI
{
    /// <summary>
    /// Helper utilities for detecting embedding model naming patterns.
    /// Vector dimensions are dynamically probed live via the provider's embedding API.
    /// </summary>
    public static class EmbeddingModelMetadata
    {
        /// <summary>
        /// Simple heuristic to check if a model ID represents an embedding model based on its name.
        /// </summary>
        public static bool IsEmbeddingModel(string? modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return false;
            }

            string lower = modelId.ToLowerInvariant();
            return lower.Contains("embed") ||
                   lower.Contains("embedding") ||
                   lower.Contains("bge-") ||
                   lower.Contains("gte-") ||
                   lower.Contains("mxbai-") ||
                   lower.Contains("e5-");
        }
    }
}

