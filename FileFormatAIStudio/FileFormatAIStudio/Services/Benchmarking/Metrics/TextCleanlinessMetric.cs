using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileFormatAIStudio.Services.Benchmarking.Metrics
{
    /// <summary>
    /// Evaluates text cleanliness and absence of decoding artifacts.
    /// Penalizes unicode replacement characters (\uFFFD), unresolved font glyph codes (cid:xxx),
    /// and unprintable binary control bytes.
    /// </summary>
    public class TextCleanlinessMetric : IBenchmarkMetric
    {
        public const string MetricIdentifier = "cleanliness_score";
        private static readonly Regex CidArtifactRegex = new(@"\((?:cid:\d+)\)", RegexOptions.Compiled);

        public string MetricId => MetricIdentifier;

        public string DisplayName => "Text Cleanliness & Integrity";

        public string Description => "Measures absence of corrupted font codes (cid:xxx), unicode replacement characters (\uFFFD), and binary noise.";

        public string Unit => "%";

        public bool HigherIsBetter => true;

        public double DefaultWeight => 0.7;

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
                    FormattedValue: "0.0% clean",
                    NormalizedScore: 0.0,
                    Rank: allCompetitors.Count,
                    HigherIsBetter: HigherIsBetter,
                    Weight: DefaultWeight,
                    Notes: targetContext.ThrownException?.Message ?? "No text extracted."
                );
            }

            double cleanlinessPct = CalculateCleanliness(targetContext.ExtractedText, out int totalDefects);

            // Rank against competitors
            int rank = 1 + allCompetitors.Count(c =>
            {
                if (!c.IsSuccess || string.IsNullOrEmpty(c.ExtractedText)) return false;
                double competitorScore = CalculateCleanliness(c.ExtractedText, out _);
                return competitorScore > cleanlinessPct;
            });

            string notes = totalDefects == 0
                ? "Perfect text integrity (0 defects)"
                : $"{totalDefects:N0} noise artifacts detected";

            return new MetricScoreResult(
                MetricId: MetricId,
                DisplayName: DisplayName,
                Unit: Unit,
                RawValue: Math.Round(cleanlinessPct, 1),
                FormattedValue: $"{cleanlinessPct:N1}% clean",
                NormalizedScore: Math.Round(cleanlinessPct, 1),
                Rank: rank,
                HigherIsBetter: HigherIsBetter,
                Weight: DefaultWeight,
                Notes: notes
            );
        }

        private static double CalculateCleanliness(string text, out int totalDefects)
        {
            if (string.IsNullOrEmpty(text))
            {
                totalDefects = 0;
                return 0.0;
            }

            int totalChars = text.Length;
            int replacementCharCount = text.Count(c => c == '\uFFFD');
            int controlCharCount = text.Count(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');
            int cidArtifacts = CidArtifactRegex.Matches(text).Count * 8;

            totalDefects = replacementCharCount + controlCharCount + cidArtifacts;
            int cleanChars = Math.Max(0, totalChars - totalDefects);

            return Math.Clamp(((double)cleanChars / totalChars) * 100.0, 0.0, 100.0);
        }
    }
}

