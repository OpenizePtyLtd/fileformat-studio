using System;
using System.Collections.Generic;

namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Evaluated benchmark outcome for a specific engine on a single document.
    /// </summary>
    public record BenchmarkEngineRunResult(
        string EngineId,
        string EngineDisplayName,
        bool IsSuccess,
        TimeSpan ElapsedTime,
        long AllocatedBytes,
        long CharacterCount,
        long WordCount,
        double OverallScore,
        int Rank,
        IReadOnlyList<MetricScoreResult> MetricScores,
        string ExtractedText,
        string? ErrorMessage = null
    );

    /// <summary>
    /// Consolidated benchmark evaluation across all competing engines for a single document.
    /// </summary>
    public record BenchmarkDocumentResult(
        string FilePath,
        string FileName,
        string Extension,
        DocumentCategory Category,
        long FileSizeBytes,
        IReadOnlyList<BenchmarkEngineRunResult> EngineRuns,
        BenchmarkEngineRunResult? WinnerEngine
    );

    /// <summary>
    /// Overall result of a complete benchmark session containing one or more evaluated documents.
    /// </summary>
    public record BenchmarkSessionResult(
        Guid SessionId,
        string Title,
        DocumentCategory Category,
        DateTime CreatedAt,
        IReadOnlyList<BenchmarkDocumentResult> DocumentResults,
        string? OverallWinnerEngineId,
        string? OverallWinnerDisplayName
    );
}

