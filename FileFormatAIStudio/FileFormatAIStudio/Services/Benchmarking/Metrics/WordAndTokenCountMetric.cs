using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileFormatAIStudio.Services.Benchmarking.Metrics
{
    /// <summary>
    /// Evaluates word count and estimated LLM token volume for downstream RAG embeddings.
    /// Higher lexical volume indicates more semantic grounding for LLM prompt augmentation.
    /// </summary>
    public class WordAndTokenCountMetric : IBenchmarkMetric
    {
        public const string MetricIdentifier = "word_token_count";
        private static readonly Regex WordRegex = new(@"\b[\w'-]+\b", RegexOptions.Compiled);

        public string MetricId => MetricIdentifier;

        public string DisplayName => "Word & Estimated Token Count";

        public string Description => "Measures lexical volume and estimated LLM tokens. Higher word and token volume yields superior document grounding for RAG search.";

        public string Unit => "words";

        public bool HigherIsBetter => true;

        public double DefaultWeight => 0.8;

        public MetricScoreResult Evaluate(
            BenchmarkExecutionContext targetContext,
            IReadOnlyList<BenchmarkExecutionContext> allCompetitors)
        {
            if (!targetContext.IsSuccess || string.IsNullOrWhiteSpace(targetContext.ExtractedText))
            {
                return new MetricScoreResult(
                    MetricId: MetricId,
                    DisplayName: DisplayName,
                    Unit: Unit,
                    RawValue: 0,
                    FormattedValue: "0 words (~0 tokens)",
                    NormalizedScore: 0.0,
                    Rank: allCompetitors.Count,
                    HigherIsBetter: HigherIsBetter,
                    Weight: DefaultWeight,
                    Notes: targetContext.ThrownException?.Message ?? "No text extracted."
                );
            }

            long targetWords = WordRegex.Matches(targetContext.ExtractedText).Count;
            long targetTokens = (long)Math.Ceiling(targetContext.CharacterCount / 4.0);

            long maxWords = allCompetitors
                .Where(c => c.IsSuccess && !string.IsNullOrWhiteSpace(c.ExtractedText))
                .Select(c => (long)WordRegex.Matches(c.ExtractedText).Count)
                .DefaultIfEmpty(0)
                .Max();

            double normalizedScore = maxWords > 0
                ? Math.Clamp(((double)targetWords / maxWords) * 100.0, 0.0, 100.0)
                : 0.0;

            int rank = 1 + allCompetitors.Count(c =>
                c.IsSuccess &&
                !string.IsNullOrWhiteSpace(c.ExtractedText) &&
                WordRegex.Matches(c.ExtractedText).Count > targetWords);

            string notes = rank == 1
                ? "Highest lexical volume"
                : $"{maxWords - targetWords:N0} fewer words than leader";

            return new MetricScoreResult(
                MetricId: MetricId,
                DisplayName: DisplayName,
                Unit: Unit,
                RawValue: targetWords,
                FormattedValue: $"{targetWords:N0} words (~{targetTokens:N0} tokens)",
                NormalizedScore: Math.Round(normalizedScore, 1),
                Rank: rank,
                HigherIsBetter: HigherIsBetter,
                Weight: DefaultWeight,
                Notes: notes
            );
        }
    }
}

