using System;

namespace FileFormatAIStudio.Services.Knowledgebase
{
    /// <summary>
    /// Represents a discrete text chunk extracted from a document with boundary offsets and metadata.
    /// </summary>
    public sealed record TextChunk
    {
        /// <summary>
        /// Deterministic unique identifier for this chunk (derived from document ID, sequence, and content).
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        /// 0-based sequence index of the chunk within the document.
        /// </summary>
        public int SequenceIndex { get; init; }

        /// <summary>
        /// The plain text content of the chunk.
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// Starting character offset in the source document.
        /// </summary>
        public int CharacterStart { get; init; }

        /// <summary>
        /// Ending character offset in the source document.
        /// </summary>
        public int CharacterEnd { get; init; }

        /// <summary>
        /// Estimated token count for this chunk.
        /// </summary>
        public int EstimatedTokenCount { get; init; }
    }
}
