using System.Collections.Generic;

namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Pluggable contract for evaluating a specific quality, performance, or integrity dimension of extracted document text.
    /// </summary>
    public interface IBenchmarkMetric
    {
        /// <summary>
        /// Unique machine-readable identifier for the metric (e.g. "char_count", "latency_ms").
        /// </summary>
        string MetricId { get; }

        /// <summary>
        /// Human-readable display name for UI reporting (e.g. "Total Extracted Characters").
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Detailed description of what this metric evaluates and its relevance to text quality or performance.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Measurement unit (e.g. "chars", "ms", "KB", "%").
        /// </summary>
        string Unit { get; }

        /// <summary>
        /// Indicates whether higher numeric measurements indicate better performance (true)
        /// or lower numbers are superior (false, e.g. latency/memory).
        /// </summary>
        bool HigherIsBetter { get; }

        /// <summary>
        /// Default relative importance weight used when computing overall composite rank scores.
        /// </summary>
        double DefaultWeight { get; }

        /// <summary>
        /// Evaluates the target parser execution context against all competing engine contexts for the same document.
        /// </summary>
        /// <param name="targetContext">Execution context of the parser being scored.</param>
        /// <param name="allCompetitors">List of all competing parser execution contexts evaluated for the same document.</param>
        /// <returns>Evaluated score result with raw measurement, normalized 0-100 score, and relative rank.</returns>
        MetricScoreResult Evaluate(
            BenchmarkExecutionContext targetContext,
            IReadOnlyList<BenchmarkExecutionContext> allCompetitors);
    }
}

