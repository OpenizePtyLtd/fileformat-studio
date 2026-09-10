using System;

namespace FileFormatAIStudio.Services.Knowledgebase
{
    /// <summary>
    /// Represents a scored document chunk retrieved from vector similarity search.
    /// </summary>
    public sealed record ScoredChunkResult(
        Guid ChunkId,
        Guid DocumentId,
        Guid KnowledgebaseId,
        string TextContent,
        string SourceFileName,
        int PageOrSectionNumber,
        int ChunkIndex,
        int TokenCount,
        float Score
    );
}
