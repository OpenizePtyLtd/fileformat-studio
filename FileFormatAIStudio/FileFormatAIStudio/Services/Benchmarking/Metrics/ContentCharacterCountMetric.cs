using System;
using System.Collections.Generic;
using System.Linq;

namespace FileFormatAIStudio.Services.Benchmarking.Metrics
{
    /// <summary>
    /// Evaluates effective content character volume, excluding whitespace, blank lines, and padding.
    /// Prevents parsers from artificially inflating scores through repeated newlines or tabs.
    /// </summary>
    public class ContentCharacterCountMetric : IBenchmarkMetric
    {
        public const string MetricIdentifier = "content_char_count";

        public string MetricId => MetricIdentifier;

        public string DisplayName => "Content Characters (No Whitespace)";

        public string Description => "Measures non-whitespace character volume. Prevents libraries from inflating text length with empty lines, indentation, or trailing whitespace.";

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
                    Notes: targetContext.ThrownException?.Message ?? "No text extracted."
                );
            }

            long targetContentChars = targetContext.ExtractedText.Count(c => !char.IsWhiteSpace(c));

            long maxContentChars = allCompetitors
                .Where(c => c.IsSuccess && !string.IsNullOrEmpty(c.ExtractedText))
                .Select(c => (long)c.ExtractedText.Count(ch => !char.IsWhiteSpace(ch)))
                .DefaultIfEmpty(0)
                .Max();

            double normalizedScore = maxContentChars > 0
                ? Math.Clamp(((double)targetContentChars / maxContentChars) * 100.0, 0.0, 100.0)
                : 0.0;

            int rank = 1 + allCompetitors.Count(c =>
                c.IsSuccess &&
                !string.IsNullOrEmpty(c.ExtractedText) &&
                c.ExtractedText.Count(ch => !char.IsWhiteSpace(ch)) > targetContentChars);

            string notes = rank == 1
                ? "Highest content character density"
                : $"{maxContentChars - targetContentChars:N0} fewer content chars than leader";

            return new MetricScoreResult(
                MetricId: MetricId,
                DisplayName: DisplayName,
                Unit: Unit,
                RawValue: targetContentChars,
                FormattedValue: $"{targetContentChars:N0} chars",
                NormalizedScore: Math.Round(normalizedScore, 1),
                Rank: rank,
                HigherIsBetter: HigherIsBetter,
                Weight: DefaultWeight,
                Notes: notes
            );
        }
    }
}

