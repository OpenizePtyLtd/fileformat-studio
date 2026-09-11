using System;
using System.Collections.Generic;

namespace FileFormatAIStudio.Data.Entities
{
    /// <summary>
    /// Represents an individual document file tested within a benchmark session.
    /// </summary>
    public class BenchmarkDocumentEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SessionId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string Category { get; set; } = "Word";
        public long FileSizeBytes { get; set; }

        // Navigation properties
        public BenchmarkSessionEntity? Session { get; set; }
        public List<BenchmarkRunResultEntity> RunResults { get; set; } = new();
    }
}

