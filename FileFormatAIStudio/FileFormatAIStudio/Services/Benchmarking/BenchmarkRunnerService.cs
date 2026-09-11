using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Benchmarking.Metrics;
using FileFormatAIStudio.Services.Parsing;
using Microsoft.EntityFrameworkCore;

namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Core orchestrator for multi-library document parsing benchmarks.
    /// Runs documents across all compatible parser engines under isolated error boundaries,
    /// evaluates pluggable metrics, computes rankings, and persists results to SQLite.
    /// </summary>
    public class BenchmarkRunnerService : IBenchmarkRunnerService
    {
        private static readonly Regex WordRegex = new(@"\b[\w'-]+\b", RegexOptions.Compiled);

        private readonly AppDbContext _dbContext;
        private readonly IDocumentParserFactory _parserFactory;
        private readonly IDocumentCategoryRegistry _categoryRegistry;
        private readonly List<IBenchmarkMetric> _metrics;

        public BenchmarkRunnerService(
            AppDbContext dbContext,
            IDocumentParserFactory parserFactory,
            IDocumentCategoryRegistry categoryRegistry,
            IEnumerable<IBenchmarkMetric>? metrics = null)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _parserFactory = parserFactory ?? throw new ArgumentNullException(nameof(parserFactory));
            _categoryRegistry = categoryRegistry ?? throw new ArgumentNullException(nameof(categoryRegistry));

            var registeredMetrics = metrics?.ToList() ?? new List<IBenchmarkMetric>();
            if (!registeredMetrics.Any(m => m.MetricId == CharacterCountMetric.MetricIdentifier))
            {
                registeredMetrics.Insert(0, new CharacterCountMetric());
            }
            _metrics = registeredMetrics;
        }

        public async Task<BenchmarkSessionResult> RunBenchmarkAsync(
            string filePath,
            BenchmarkOptions? options = null,
            IProgress<BenchmarkProgressReport>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            return await RunBatchBenchmarkAsync(new[] { filePath }, options, progress, cancellationToken);
        }

        public async Task<BenchmarkSessionResult> RunBatchBenchmarkAsync(
            IEnumerable<string> filePaths,
            BenchmarkOptions? options = null,
            IProgress<BenchmarkProgressReport>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var validFiles = filePaths?
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (!validFiles.Any())
            {
                throw new ArgumentException("At least one valid, existing file path must be supplied for benchmarking.", nameof(filePaths));
            }

            options ??= new BenchmarkOptions();

            // Resolve active metrics based on options
            var activeMetrics = _metrics.Where(m =>
                options.SelectedMetricIds == null ||
                options.SelectedMetricIds.Count == 0 ||
                options.SelectedMetricIds.Contains(m.MetricId, StringComparer.OrdinalIgnoreCase)).ToList();

            if (!activeMetrics.Any())
            {
                activeMetrics.Add(new CharacterCountMetric());
            }

            var sessionId = Guid.NewGuid();
            var primaryCategory = _categoryRegistry.ResolveCategory(validFiles[0]);
            var sessionTitle = !string.IsNullOrWhiteSpace(options.CustomSessionTitle)
                ? options.CustomSessionTitle
                : $"{_categoryRegistry.GetCategoryDisplayName(primaryCategory)} Benchmark ({DateTime.Now:MMM dd, yyyy HH:mm})";

            var documentResults = new List<BenchmarkDocumentResult>();
            int totalDocs = validFiles.Count;

            for (int i = 0; i < validFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = validFiles[i];
                var fileName = Path.GetFileName(filePath);
                var ext = DocumentCategoryRegistry.NormalizeExtension(filePath);
                var category = _categoryRegistry.ResolveCategory(filePath);
                var fileInfo = new FileInfo(filePath);
                long fileSize = fileInfo.Length;

                double docStartPct = ((double)i / totalDocs) * 100.0;
                progress?.Report(new BenchmarkProgressReport(
                    i + 1, totalDocs, fileName, null, "Preparing", docStartPct,
                    $"Preparing parsers for {fileName} ({i + 1}/{totalDocs})..."));

                // Resolve all available and compatible parsers
                var eligibleParsers = _parserFactory.GetAllParsers()
                    .Where(p => p.IsAvailable && p.SupportedExtensions.Contains(ext))
                    .ToList();

                if (options.SelectedEngineIds != null && options.SelectedEngineIds.Any())
                {
                    eligibleParsers = eligibleParsers
                        .Where(p => options.SelectedEngineIds.Contains(p.EngineId, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                }

                var executionContexts = new List<BenchmarkExecutionContext>();

                for (int pIdx = 0; pIdx < eligibleParsers.Count; pIdx++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var parser = eligibleParsers[pIdx];

                    double parserPct = docStartPct + (((double)pIdx / Math.Max(1, eligibleParsers.Count)) * (100.0 / totalDocs));
                    progress?.Report(new BenchmarkProgressReport(
                        i + 1, totalDocs, fileName, parser.DisplayName, "Parsing", parserPct,
                        $"Extracting text with {parser.DisplayName}..."));

                    var startTimestamp = Stopwatch.GetTimestamp();
                    long startAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

                    string extractedText = string.Empty;
                    bool isSuccess = false;
                    Exception? thrownException = null;

                    try
                    {
                        extractedText = await parser.ExtractTextAsync(filePath, cancellationToken);
                        isSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        isSuccess = false;
                        thrownException = ex;
                        extractedText = string.Empty;
                    }

                    var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
                    long endAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
                    long allocatedBytes = Math.Max(0, endAllocatedBytes - startAllocatedBytes);

                    var context = new BenchmarkExecutionContext(
                        FilePath: filePath,
                        FileName: fileName,
                        Extension: ext,
                        Category: category,
                        FileSizeBytes: fileSize,
                        EngineId: parser.EngineId,
                        EngineDisplayName: parser.DisplayName,
                        ExtractedText: extractedText ?? string.Empty,
                        ElapsedTime: elapsed,
                        AllocatedBytes: allocatedBytes,
                        IsSuccess: isSuccess,
                        ThrownException: thrownException
                    );

                    executionContexts.Add(context);
                }

                // Metric Evaluation & Ranking for this document
                progress?.Report(new BenchmarkProgressReport(
                    i + 1, totalDocs, fileName, null, "Evaluating Metrics", docStartPct + (80.0 / totalDocs),
                    $"Evaluating metric scores for {fileName}..."));

                var rawEngineRuns = new List<BenchmarkEngineRunResult>();

                foreach (var context in executionContexts)
                {
                    var metricScores = new List<MetricScoreResult>();
                    foreach (var metric in activeMetrics)
                    {
                        var score = metric.Evaluate(context, executionContexts);
                        metricScores.Add(score);
                    }

                    // Compute overall composite weighted score
                    double totalWeight = metricScores.Sum(m => m.Weight);
                    double overallScore = (context.IsSuccess && totalWeight > 0)
                        ? metricScores.Sum(m => m.NormalizedScore * m.Weight) / totalWeight
                        : 0.0;

                    long wordCount = context.IsSuccess && !string.IsNullOrWhiteSpace(context.ExtractedText)
                        ? WordRegex.Matches(context.ExtractedText).Count
                        : 0;

                    rawEngineRuns.Add(new BenchmarkEngineRunResult(
                        EngineId: context.EngineId,
                        EngineDisplayName: context.EngineDisplayName,
                        IsSuccess: context.IsSuccess,
                        ElapsedTime: context.ElapsedTime,
                        AllocatedBytes: context.AllocatedBytes,
                        CharacterCount: context.CharacterCount,
                        WordCount: wordCount,
                        OverallScore: Math.Round(overallScore, 1),
                        Rank: 1, // Computed below
                        MetricScores: metricScores,
                        ExtractedText: context.ExtractedText,
                        ErrorMessage: context.ThrownException?.Message
                    ));
                }

                // Compute rank order: Successful first, then highest overall score, then highest char count, then lowest latency
                var rankedRuns = rawEngineRuns
                    .OrderByDescending(r => r.IsSuccess)
                    .ThenByDescending(r => r.OverallScore)
                    .ThenByDescending(r => r.CharacterCount)
                    .ThenBy(r => r.ElapsedTime)
                    .ToList();

                var finalEngineRuns = new List<BenchmarkEngineRunResult>();
                for (int rankIdx = 0; rankIdx < rankedRuns.Count; rankIdx++)
                {
                    var run = rankedRuns[rankIdx];
                    finalEngineRuns.Add(run with { Rank = rankIdx + 1 });
                }

                var winnerEngine = finalEngineRuns.FirstOrDefault(r => r.IsSuccess && r.Rank == 1);

                documentResults.Add(new BenchmarkDocumentResult(
                    FilePath: filePath,
                    FileName: fileName,
                    Extension: ext,
                    Category: category,
                    FileSizeBytes: fileSize,
                    EngineRuns: finalEngineRuns,
                    WinnerEngine: winnerEngine
                ));
            }

            // Determine overall session winner (engine with most wins / highest average score)
            var overallWinner = documentResults
                .Select(d => d.WinnerEngine)
                .Where(w => w != null)
                .GroupBy(w => w!.EngineId)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.First();

            var sessionResult = new BenchmarkSessionResult(
                SessionId: sessionId,
                Title: sessionTitle,
                Category: primaryCategory,
                CreatedAt: DateTime.UtcNow,
                DocumentResults: documentResults,
                OverallWinnerEngineId: overallWinner?.EngineId,
                OverallWinnerDisplayName: overallWinner?.EngineDisplayName
            );

            // Persist to database if enabled
            if (options.SaveToDatabase)
            {
                await SaveSessionToDatabaseAsync(sessionResult, cancellationToken);
            }

            progress?.Report(new BenchmarkProgressReport(
                totalDocs, totalDocs, string.Empty, null, "Completed", 100.0,
                $"Benchmark completed successfully. Winner: {sessionResult.OverallWinnerDisplayName ?? "N/A"}."));

            return sessionResult;
        }

        public async Task<IReadOnlyList<BenchmarkSessionEntity>> GetBenchmarkHistoryAsync(
            DocumentCategory? category = null,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.BenchmarkSessions
                .Include(s => s.Documents)
                    .ThenInclude(d => d.RunResults)
                        .ThenInclude(r => r.MetricResults)
                .AsNoTracking();

            if (category.HasValue)
            {
                var categoryStr = category.Value.ToString();
                query = query.Where(s => s.Category == categoryStr);
            }

            return await query
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<BenchmarkSessionEntity?> GetBenchmarkSessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.BenchmarkSessions
                .Include(s => s.Documents)
                    .ThenInclude(d => d.RunResults)
                        .ThenInclude(r => r.MetricResults)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        }

        public async Task<bool> DeleteBenchmarkSessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            var session = await _dbContext.BenchmarkSessions.FindAsync(new object[] { sessionId }, cancellationToken);
            if (session == null)
                return false;

            _dbContext.BenchmarkSessions.Remove(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task SaveSessionToDatabaseAsync(
            BenchmarkSessionResult sessionResult,
            CancellationToken cancellationToken)
        {
            var sessionEntity = new BenchmarkSessionEntity
            {
                Id = sessionResult.SessionId,
                Title = sessionResult.Title,
                Category = sessionResult.Category.ToString(),
                TotalDocuments = sessionResult.DocumentResults.Count,
                CreatedAt = sessionResult.CreatedAt
            };

            foreach (var docResult in sessionResult.DocumentResults)
            {
                var docEntity = new BenchmarkDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionEntity.Id,
                    FileName = docResult.FileName,
                    FilePath = docResult.FilePath,
                    Extension = docResult.Extension,
                    Category = docResult.Category.ToString(),
                    FileSizeBytes = docResult.FileSizeBytes
                };

                foreach (var runResult in docResult.EngineRuns)
                {
                    // Snapshot text up to 50,000 characters to prevent excessive DB bloat
                    string textSnapshot = runResult.ExtractedText.Length > 50000
                        ? runResult.ExtractedText.Substring(0, 50000)
                        : runResult.ExtractedText;

                    var runEntity = new BenchmarkRunResultEntity
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = docEntity.Id,
                        EngineId = runResult.EngineId,
                        EngineDisplayName = runResult.EngineDisplayName,
                        Status = runResult.IsSuccess ? "Success" : "Failed",
                        ElapsedMilliseconds = (long)runResult.ElapsedTime.TotalMilliseconds,
                        MemoryAllocatedBytes = runResult.AllocatedBytes,
                        CharacterCount = runResult.CharacterCount,
                        WordCount = runResult.WordCount,
                        OverallScore = runResult.OverallScore,
                        Rank = runResult.Rank,
                        ExtractedTextSnapshot = textSnapshot,
                        ErrorMessage = runResult.ErrorMessage
                    };

                    foreach (var metric in runResult.MetricScores)
                    {
                        runEntity.MetricResults.Add(new BenchmarkMetricResultEntity
                        {
                            Id = Guid.NewGuid(),
                            RunResultId = runEntity.Id,
                            MetricId = metric.MetricId,
                            DisplayName = metric.DisplayName,
                            Unit = metric.Unit,
                            RawValue = metric.RawValue,
                            FormattedValue = metric.FormattedValue,
                            NormalizedScore = metric.NormalizedScore,
                            Rank = metric.Rank,
                            HigherIsBetter = metric.HigherIsBetter,
                            Weight = metric.Weight,
                            Notes = metric.Notes
                        });
                    }

                    docEntity.RunResults.Add(runEntity);
                }

                sessionEntity.Documents.Add(docEntity);
            }

            _dbContext.BenchmarkSessions.Add(sessionEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

