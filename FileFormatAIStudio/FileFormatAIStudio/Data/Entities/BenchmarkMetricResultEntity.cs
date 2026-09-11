using System;

namespace FileFormatAIStudio.Data.Entities
{
    /// <summary>
    /// Represents the evaluation of a specific benchmark metric for a parser engine run.
    /// </summary>
    public class BenchmarkMetricResultEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RunResultId { get; set; }
        public string MetricId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double RawValue { get; set; }
        public string FormattedValue { get; set; } = string.Empty;
        public double NormalizedScore { get; set; } // 0 to 100
        public int Rank { get; set; } = 1;
        public bool HigherIsBetter { get; set; } = true;
        public double Weight { get; set; } = 1.0;
        public string? Notes { get; set; }

        // Navigation property
        public BenchmarkRunResultEntity? RunResult { get; set; }
    }
}

