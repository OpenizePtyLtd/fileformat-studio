namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Represents the evaluation result of a single benchmark metric for a parser engine.
    /// </summary>
    /// <param name="MetricId">Unique identifier of the metric (e.g. "char_count", "latency_ms").</param>
    /// <param name="DisplayName">User-facing name of the metric (e.g. "Extracted Character Count").</param>
    /// <param name="Unit">Unit of measurement (e.g. "chars", "ms", "KB", "%").</param>
    /// <param name="RawValue">Unnormalized numeric raw measurement.</param>
    /// <param name="FormattedValue">Human-readable formatted representation (e.g. "142,520 chars").</param>
    /// <param name="NormalizedScore">Normalized benchmark score ranging from 0.0 to 100.0.</param>
    /// <param name="Rank">Relative rank for this metric among competitors (1 = top performer).</param>
    /// <param name="HigherIsBetter">True if higher raw values yield higher scores; false for lower-is-better metrics like latency.</param>
    /// <param name="Weight">Configured weighting factor used when computing composite overall scores.</param>
    /// <param name="Notes">Optional explanatory remarks or diagnostic insights.</param>
    public record MetricScoreResult(
        string MetricId,
        string DisplayName,
        string Unit,
        double RawValue,
        string FormattedValue,
        double NormalizedScore,
        int Rank,
        bool HigherIsBetter = true,
        double Weight = 1.0,
        string? Notes = null
    );
}

