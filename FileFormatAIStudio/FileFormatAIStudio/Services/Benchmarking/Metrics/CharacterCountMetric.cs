using System;
using System.Collections.Generic;
using System.Linq;

namespace FileFormatAIStudio.Services.Benchmarking.Metrics
{
    /// <summary>
    /// Core baseline benchmark metric evaluating total raw character count extracted from the document.
    /// The parser engine that extracts the highest volume of characters receives the highest score and top rank.
    /// </summary>
    public class CharacterCountMetric : IBenchmarkMetric
    {
        public const string MetricIdentifier = "char_count";

        public string MetricId => MetricIdentifier;

        public string DisplayName => "Total Extracted Characters";

        public string Description => "Measures the total raw character volume extracted from the document. The library extracting the largest character count receives the highest score and rank #1.";

        public string Unit => "chars";

        public bool HigherIsBetter => true;

        public double DefaultWeight => 1.0;

        public MetricScoreResult Evaluate(
            BenchmarkExecutionContext targetContext,
            IReadOnlyList<BenchmarkExecutionContext> allCompetitors)
        {
            if (!targetContext.IsSuccess || string.IsNullOrEmpty(targetContext.ExtractedText))
            {
                return new MetricScoreResult(
                    MetricId: MetricId,
                    DisplayName: DisplayName,
                    Unit: Unit,
                    RawValue: 0,
                    FormattedValue: "0 chars",
                    NormalizedScore: 0.0,
                    Rank: allCompetitors.Count,
                    HigherIsBetter: HigherIsBetter,
                    Weight: DefaultWeight,
                    Notes: targetContext.ThrownException?.Message ?? "Extraction failed or returned empty text."
                );
            }

            long targetCount = targetContext.CharacterCount;

            // Find max extracted characters among successful competitors
            long maxCompetitorCount = allCompetitors
                .Where(c => c.IsSuccess)
                .Select(c => c.CharacterCount)
                .DefaultIfEmpty(0)
                .Max();

            double normalizedScore = maxCompetitorCount > 0
                ? Math.Clamp(((double)targetCount / maxCompetitorCount) * 100.0, 0.0, 100.0)
                : 0.0;

            // Rank is 1 + count of successful competitors with strictly higher character count
            int rank = 1 + allCompetitors.Count(c => c.IsSuccess && c.CharacterCount > targetCount);

            string notes = rank == 1
                ? "Highest character volume extracted (Rank #1)"
                : $"{maxCompetitorCount - targetCount:N0} fewer chars than leader";

            return new MetricScoreResult(
                MetricId: MetricId,
                DisplayName: DisplayName,
                Unit: Unit,
                RawValue: targetCount,
                FormattedValue: $"{targetCount:N0} chars",
                NormalizedScore: Math.Round(normalizedScore, 1),
                Rank: rank,
                HigherIsBetter: HigherIsBetter,
                Weight: DefaultWeight,
                Notes: notes
            );
        }
    }
}

