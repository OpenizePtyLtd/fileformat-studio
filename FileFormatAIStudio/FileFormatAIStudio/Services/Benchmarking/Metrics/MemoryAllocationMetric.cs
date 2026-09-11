using System;
using System.Collections.Generic;
using System.Linq;

namespace FileFormatAIStudio.Services.Benchmarking.Metrics
{
    /// <summary>
    /// Evaluates GC heap memory allocated on the parser execution thread.
    /// Lower memory consumption prevents desktop out-of-memory crashes on large files.
    /// </summary>
    public class MemoryAllocationMetric : IBenchmarkMetric
    {
        public const string MetricIdentifier = "memory_allocated_bytes";

        public string MetricId => MetricIdentifier;

        public string DisplayName => "Memory Allocated";

        public string Description => "Measures thread heap memory allocated during extraction. Lower memory usage ensures smooth desktop multitasking.";

        public string Unit => "MB";

        public bool HigherIsBetter => false;

        public double DefaultWeight => 0.4;

        public MetricScoreResult Evaluate(
            BenchmarkExecutionContext targetContext,
            IReadOnlyList<BenchmarkExecutionContext> allCompetitors)
        {
            if (!targetContext.IsSuccess)
            {
                return new MetricScoreResult(
                    MetricId: MetricId,
                    DisplayName: DisplayName,
                    Unit: Unit,
                    RawValue: double.MaxValue,
                    FormattedValue: "Failed (N/A)",
                    NormalizedScore: 0.0,
                    Rank: allCompetitors.Count,
                    HigherIsBetter: HigherIsBetter,
                    Weight: DefaultWeight,
                    Notes: targetContext.ThrownException?.Message ?? "Extraction failed."
                );
            }

            long targetBytes = Math.Max(1024, targetContext.AllocatedBytes);
            double targetMb = targetBytes / (1024.0 * 1024.0);

            long minBytes = allCompetitors
                .Where(c => c.IsSuccess)
                .Select(c => Math.Max(1024, c.AllocatedBytes))
                .DefaultIfEmpty(targetBytes)
                .Min();

            double normalizedScore = targetBytes > 0
                ? Math.Clamp(((double)minBytes / targetBytes) * 100.0, 0.0, 100.0)
                : 100.0;

            int rank = 1 + allCompetitors.Count(c =>
                c.IsSuccess &&
                c.AllocatedBytes < targetContext.AllocatedBytes);

            string formatted = targetMb >= 1.0
                ? $"{targetMb:N2} MB"
                : $"{targetBytes / 1024.0:N1} KB";

            string notes = rank == 1
                ? "Lowest memory footprint"
                : $"+{(targetBytes - minBytes) / (1024.0 * 1024.0):N2} MB over lowest";

            return new MetricScoreResult(
                MetricId: MetricId,
                DisplayName: DisplayName,
                Unit: Unit,
                RawValue: Math.Round(targetMb, 2),
                FormattedValue: formatted,
                NormalizedScore: Math.Round(normalizedScore, 1),
                Rank: rank,
                HigherIsBetter: HigherIsBetter,
                Weight: DefaultWeight,
                Notes: notes
            );
        }
    }
}

