using System;

namespace FileFormatAIStudio.Data.Entities
{
    /// <summary>
    /// Represents a source citation reference attached to a chat message, linking
    /// generated content to the specific knowledgebase document and chunk that grounded it.
    /// </summary>
    public sealed class CitationReference
    {
        public int Index { get; set; }
        public Guid ChunkId { get; set; }
        public Guid DocumentId { get; set; }
        public Guid KnowledgebaseId { get; set; }
        public string KnowledgebaseName { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public int PageOrSectionNumber { get; set; }
        public string Snippet { get; set; } = string.Empty;
        public float Score { get; set; }

        public string DisplayIndex => $"[{Index}]";
        public string PageOrSectionDisplay => PageOrSectionNumber > 0 ? $"p. {PageOrSectionNumber}" : "sec. 1";
        public string SourceHeaderInfo => $"KB: {KnowledgebaseName} • {PageOrSectionDisplay}";
        public string MatchScoreText => $"{(int)Math.Round(Score * 100)}% match";
    }
}

