using System;

namespace FileFormatAIStudio.Services.Knowledgebase
{
    /// <summary>
    /// Configuration options for the text chunking algorithm.
    /// </summary>
    public sealed record ChunkingOptions
    {
        /// <summary>
        /// Target maximum number of tokens per chunk (default: 500).
        /// </summary>
        public int TargetChunkSizeTokens { get; init; } = 500;

        /// <summary>
        /// Number of tokens that overlap between adjacent chunks to maintain semantic continuity (default: 100).
        /// </summary>
        public int OverlapTokens { get; init; } = 100;

        /// <summary>
        /// Minimum token count required for an independent chunk (default: 20).
        /// Small trailing fragments below this threshold are merged into the preceding chunk.
        /// </summary>
        public int MinChunkSizeTokens { get; init; } = 20;

        /// <summary>
        /// Optional Document ID used to generate deterministic chunk GUIDs.
        /// </summary>
        public Guid? DocumentId { get; init; }

        /// <summary>
        /// Optional custom token estimation function. Defaults to ~4 characters per token.
        /// </summary>
        public Func<string, int>? TokenEstimator { get; init; }
    }
}
