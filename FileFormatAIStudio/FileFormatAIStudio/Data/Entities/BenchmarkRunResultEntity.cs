using System;
using System.Collections.Generic;

namespace FileFormatAIStudio.Data.Entities
{
    /// <summary>
    /// Represents the parsing outcome of a specific parser engine on a benchmark document.
    /// </summary>
    public class BenchmarkRunResultEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DocumentId { get; set; }
        public string EngineId { get; set; } = string.Empty;
        public string EngineDisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = "Success"; // "Success", "Failed", "Timeout"
        public long ElapsedMilliseconds { get; set; }
        public long MemoryAllocatedBytes { get; set; }
        public long CharacterCount { get; set; }
        public long WordCount { get; set; }
        public double OverallScore { get; set; }
        public int Rank { get; set; } = 1;
        public string ExtractedTextSnapshot { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }

        // Navigation properties
        public BenchmarkDocumentEntity? Document { get; set; }
        public List<BenchmarkMetricResultEntity> MetricResults { get; set; } = new();
    }
}

