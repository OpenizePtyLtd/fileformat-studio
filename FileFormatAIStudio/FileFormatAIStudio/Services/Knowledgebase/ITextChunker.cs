using System.Collections.Generic;

namespace FileFormatAIStudio.Services.Knowledgebase
{
    /// <summary>
    /// Contract for splitting raw document text into semantically coherent chunks with sliding window overlap.
    /// </summary>
    public interface ITextChunker
    {
        /// <summary>
        /// Chunks the specified text according to the provided options.
        /// </summary>
        /// <param name="text">Raw plain text extracted from a document.</param>
        /// <param name="options">Optional chunking configuration parameters.</param>
        /// <returns>A read-only list of ordered <see cref="TextChunk"/> instances.</returns>
        IReadOnlyList<TextChunk> ChunkText(string text, ChunkingOptions? options = null);
    }
}
