using System;
using System.Collections.Generic;
using System.Linq;

namespace FileFormatAIStudio.Services.AI
{
    /// <summary>
    /// Represents metadata and dimension characteristics for a known embedding model.
    /// </summary>
    public sealed record EmbeddingModelInfo(
        string ModelId,
        string DisplayName,
        string ProviderType,
        int Dimensions,
        string Description
    );

    /// <summary>
    /// Registry and dimension helper for cloud (OpenAI) and local (Ollama) embedding models.
    /// </summary>
    public static class EmbeddingModelMetadata
    {
        public const int DefaultDimensions = 1536;

        private static readonly Dictionary<string, EmbeddingModelInfo> KnownModels =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Cloud: OpenAI
                ["text-embedding-3-small"] = new(
                    "text-embedding-3-small",
                    "OpenAI text-embedding-3-small",
                    "OpenAI",
                    1536,
                    "High performance, cost-effective general-purpose embedding model (1536 dimensions)."),

                ["text-embedding-3-large"] = new(
                    "text-embedding-3-large",
                    "OpenAI text-embedding-3-large",
                    "OpenAI",
                    3072,
                    "Highest precision multilingual embedding model from OpenAI (3072 dimensions)."),

                ["text-embedding-ada-002"] = new(
                    "text-embedding-ada-002",
                    "OpenAI text-embedding-ada-002",
                    "OpenAI",
                    1536,
                    "Legacy OpenAI embedding model (1536 dimensions)."),

                // Local: Ollama / Local Infrastructure
                ["nomic-embed-text"] = new(
                    "nomic-embed-text",
                    "Nomic Embed Text (Local)",
                    "Ollama",
                    768,
                    "Popular open-source local embedding model with 8192 context window (768 dimensions)."),

                ["bge-m3"] = new(
                    "bge-m3",
                    "BAAI BGE-M3 (Local)",
                    "Ollama",
                    1024,
                    "State-of-the-art multilingual and multi-granularity local model (1024 dimensions)."),

                ["bge-small-en-v1.5"] = new(
                    "bge-small-en-v1.5",
                    "BAAI BGE-Small English (Local)",
                    "Ollama",
                    384,
                    "Fast, compact English embedding model for lightweight local use (384 dimensions)."),

                ["all-minilm"] = new(
                    "all-minilm",
                    "MiniLM L6 v2 (Local)",
                    "Ollama",
                    384,
                    "Ultra-fast, low-memory sentence-transformers model (384 dimensions)."),

                ["mxbai-embed-large"] = new(
                    "mxbai-embed-large",
                    "MixedBread AI Large (Local)",
                    "Ollama",
                    1024,
                    "High-accuracy large local embedding model (1024 dimensions).")
            };

        /// <summary>
        /// Gets the curated list of recommended embedding models for display in UI pickers.
        /// </summary>
        public static IReadOnlyList<EmbeddingModelInfo> RecommendedModels => KnownModels.Values.ToList();

        /// <summary>
        /// Retrieves the known dimension count for a given model ID, or null if unknown / custom.
        /// </summary>
        public static int? GetKnownDimensions(string? modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return null;
            }

            string cleanId = modelId.Trim();

            // Try direct match
            if (KnownModels.TryGetValue(cleanId, out var directInfo))
            {
                return directInfo.Dimensions;
            }

            // Try matching without namespace prefixes (e.g. "ollama/nomic-embed-text:latest" -> "nomic-embed-text")
            string baseModel = cleanId.Split('/').Last().Split(':').First();
            if (KnownModels.TryGetValue(baseModel, out var baseInfo))
            {
                return baseInfo.Dimensions;
            }

            return null;
        }

        /// <summary>
        /// Returns whether the specified model identifier represents a recognized embedding model.
        /// </summary>
        public static bool IsEmbeddingModel(string? modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return false;
            }

            if (GetKnownDimensions(modelId) != null)
            {
                return true;
            }

            string lower = modelId.ToLowerInvariant();
            return lower.Contains("embed") || lower.Contains("embedding");
        }

        /// <summary>
        /// Resolves the default dimension count for a model ID, falling back to 1536 if unrecognized.
        /// </summary>
        public static int GetDefaultDimensions(string? modelId)
        {
            return GetKnownDimensions(modelId) ?? DefaultDimensions;
        }
    }
}

