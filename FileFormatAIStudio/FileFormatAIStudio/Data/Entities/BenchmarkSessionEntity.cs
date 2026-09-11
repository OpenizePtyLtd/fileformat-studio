using System;
using System.Collections.Generic;

namespace FileFormatAIStudio.Data.Entities
{
    /// <summary>
    /// Represents an overall benchmark session containing one or more evaluated documents.
    /// </summary>
    public class BenchmarkSessionEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = "Word";
        public int TotalDocuments { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public List<BenchmarkDocumentEntity> Documents { get; set; } = new();

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string FormattedDate => CreatedAt.ToLocalTime().ToString("g");
    }
}

