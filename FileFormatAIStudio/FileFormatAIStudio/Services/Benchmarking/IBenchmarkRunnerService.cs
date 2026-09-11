using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Configuration options for a benchmark execution pass.
    /// </summary>
    public record BenchmarkOptions(
        IReadOnlyList<string>? SelectedEngineIds = null,
        IReadOnlyList<string>? SelectedMetricIds = null,
        bool SaveToDatabase = true,
        string? CustomSessionTitle = null
    );

    /// <summary>
    /// Core orchestrator for multi-library document parsing benchmarks.
    /// </summary>
    public interface IBenchmarkRunnerService
    {
        /// <summary>
        /// Runs a benchmark on a single document file across all supported and available parser engines.
        /// </summary>
        /// <param name="filePath">Path to document to benchmark.</param>
        /// <param name="options">Optional execution configurations.</param>
        /// <param name="progress">Progress notification callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Session benchmark results with per-engine scoring and rankings.</returns>
        Task<BenchmarkSessionResult> RunBenchmarkAsync(
            string filePath,
            BenchmarkOptions? options = null,
            IProgress<BenchmarkProgressReport>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs a benchmark across a batch of documents, grouping by categories and comparing across engines.
        /// </summary>
        /// <param name="filePaths">Collection of document file paths to benchmark.</param>
        /// <param name="options">Optional execution configurations.</param>
        /// <param name="progress">Progress notification callback.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Session benchmark results containing all processed documents.</returns>
        Task<BenchmarkSessionResult> RunBatchBenchmarkAsync(
            IEnumerable<string> filePaths,
            BenchmarkOptions? options = null,
            IProgress<BenchmarkProgressReport>? progress = null,
            CancellationToken cancellationToken = default);
    }
}

