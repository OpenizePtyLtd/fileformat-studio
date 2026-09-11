using System;
using System.Collections.Generic;
using System.Linq;

namespace FileFormatAIStudio.Services.Benchmarking.Metrics
{
    /// <summary>
    /// Evaluates parser execution latency and throughput in KB/s.
    /// Faster extraction speeds receive higher scores and lower ranks.
    /// </summary>
    public class ExecutionLatencyMetric : IBenchmarkMetric
    {
        public const string MetricIdentifier = "latency_ms";

        public string MetricId => MetricIdentifier;

        public string DisplayName => "Execution Latency & Throughput";

        public string Description => "Measures elapsed extraction duration and throughput (KB/s). Faster parsers reduce batch ingestion wait times.";

        public string Unit => "ms";

        public bool HigherIsBetter => false;

        public double DefaultWeight => 0.6;

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

            double targetMs = Math.Max(1.0, targetContext.ElapsedTime.TotalMilliseconds);
            double seconds = Math.Max(0.001, targetContext.ElapsedTime.TotalSeconds);
            double kbPerSec = (targetContext.FileSizeBytes / 1024.0) / seconds;

            // Find minimum (fastest) latency among successful competitors
            double minMs = allCompetitors
                .Where(c => c.IsSuccess)
                .Select(c => Math.Max(1.0, c.ElapsedTime.TotalMilliseconds))
                .DefaultIfEmpty(targetMs)
                .Min();

            // Normalized score: fastest gets 100.0, slower engines scaled inversely
            double normalizedScore = targetMs > 0
                ? Math.Clamp((minMs / targetMs) * 100.0, 0.0, 100.0)
                : 100.0;

            // Rank: 1 + count of successful competitors with strictly lower latency
            int rank = 1 + allCompetitors.Count(c =>
                c.IsSuccess &&
                c.ElapsedTime.TotalMilliseconds < targetContext.ElapsedTime.TotalMilliseconds);

            string notes = rank == 1
                ? "Fastest parser engine"
                : $"+{targetMs - minMs:N0} ms slower than leader";

            return new MetricScoreResult(
                MetricId: MetricId,
                DisplayName: DisplayName,
                Unit: Unit,
                RawValue: Math.Round(targetMs, 1),
                FormattedValue: $"{targetMs:N0} ms ({kbPerSec:N1} KB/s)",
                NormalizedScore: Math.Round(normalizedScore, 1),
                Rank: rank,
                HigherIsBetter: HigherIsBetter,
                Weight: DefaultWeight,
                Notes: notes
            );
        }
    }
}

