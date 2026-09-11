using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FileFormatAIStudio.Data.Entities;

namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Implements export routines for benchmark results to CSV and JSON,
    /// along with EF Core entity-to-domain model mapping.
    /// </summary>
    public class BenchmarkExportService : IBenchmarkExportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public string ExportToCsv(BenchmarkSessionResult session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            var sb = new StringBuilder();

            // Session Header
            sb.AppendLine("# Benchmark Session Report");
            sb.AppendLine($"Session Title,\"{EscapeCsv(session.Title)}\"");
            sb.AppendLine($"Session ID,\"{session.SessionId}\"");
            sb.AppendLine($"Category,\"{session.Category}\"");
            sb.AppendLine($"Created At (UTC),\"{session.CreatedAt:u}\"");
            sb.AppendLine($"Overall Winner,\"{EscapeCsv(session.OverallWinnerDisplayName ?? "N/A")}\"");
            sb.AppendLine($"Total Documents Evaluated,{session.DocumentResults.Count}");
            sb.AppendLine();

            // Per Document Results
            foreach (var doc in session.DocumentResults)
            {
                sb.AppendLine($"## Document: \"{EscapeCsv(doc.FileName)}\"");
                sb.AppendLine($"File Path,\"{EscapeCsv(doc.FilePath)}\"");
                sb.AppendLine($"File Size (Bytes),{doc.FileSizeBytes}");
                sb.AppendLine($"Document Winner,\"{EscapeCsv(doc.WinnerEngine?.EngineDisplayName ?? "N/A")}\"");
                sb.AppendLine();

                var engines = doc.EngineRuns.OrderBy(r => r.Rank).ToList();
                if (engines.Count > 0)
                {
                    // Header row: Metric, Engine1, Engine2...
                    sb.Append("Metric");
                    foreach (var engine in engines)
                    {
                        sb.Append($",\"{EscapeCsv(engine.EngineDisplayName)}\"");
                    }
                    sb.AppendLine();

                    // Rank
                    AppendRow(sb, "Overall Rank", engines.Select(e => e.Rank.ToString(CultureInfo.InvariantCulture)));

                    // Winner Badge
                    AppendRow(sb, "Winner Status", engines.Select(e => e.Rank == 1 ? "Winner 🥇" : $"Rank #{e.Rank}"));

                    // Composite Score
                    AppendRow(sb, "Composite Score (0-100)", engines.Select(e => e.OverallScore.ToString("F1", CultureInfo.InvariantCulture)));

                    // Total Characters (Primary baseline!)
                    AppendRow(sb, "Total Characters (Baseline)", engines.Select(e => e.CharacterCount.ToString(CultureInfo.InvariantCulture)));

                    // Word Count
                    AppendRow(sb, "Word Count", engines.Select(e => e.WordCount.ToString(CultureInfo.InvariantCulture)));

                    // Latency
                    AppendRow(sb, "Latency (ms)", engines.Select(e => e.ElapsedTime.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture)));

                    // Memory
                    AppendRow(sb, "Memory Allocated (Bytes)", engines.Select(e => e.AllocatedBytes.ToString(CultureInfo.InvariantCulture)));

                    // Cleanliness score if present
                    var cleanlinessScores = engines.Select(e =>
                    {
                        var metric = e.MetricScores.FirstOrDefault(m => m.MetricId == "cleanliness_score");
                        return metric != null ? metric.RawValue.ToString("F1", CultureInfo.InvariantCulture) + "%" : "N/A";
                    });
                    AppendRow(sb, "Cleanliness Score", cleanlinessScores);

                    // Execution Status
                    AppendRow(sb, "Status", engines.Select(e => e.IsSuccess ? "Success" : "Failed"));

                    // Error message
                    if (engines.Any(e => !string.IsNullOrEmpty(e.ErrorMessage)))
                    {
                        AppendRow(sb, "Error Message", engines.Select(e => e.ErrorMessage ?? string.Empty));
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        public string ExportToJson(BenchmarkSessionResult session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            return JsonSerializer.Serialize(session, JsonOptions);
        }

        public BenchmarkSessionResult MapEntityToResult(BenchmarkSessionEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var category = Enum.TryParse<DocumentCategory>(entity.Category, out var parsedCategory)
                ? parsedCategory
                : DocumentCategory.Word;

            var documentResults = new List<BenchmarkDocumentResult>();

            foreach (var docEntity in entity.Documents)
            {
                var engineRuns = new List<BenchmarkEngineRunResult>();

                foreach (var runEntity in docEntity.RunResults)
                {
                    var metricScores = runEntity.MetricResults.Select(m => new MetricScoreResult(
                        MetricId: m.MetricId,
                        DisplayName: m.DisplayName,
                        Unit: m.Unit,
                        RawValue: m.RawValue,
                        FormattedValue: m.FormattedValue,
                        NormalizedScore: m.NormalizedScore,
                        Rank: m.Rank,
                        HigherIsBetter: m.HigherIsBetter,
                        Weight: m.Weight,
                        Notes: m.Notes
                    )).ToList();

                    bool isSuccess = string.Equals(runEntity.Status, "Success", StringComparison.OrdinalIgnoreCase);

                    engineRuns.Add(new BenchmarkEngineRunResult(
                        EngineId: runEntity.EngineId,
                        EngineDisplayName: runEntity.EngineDisplayName,
                        IsSuccess: isSuccess,
                        ElapsedTime: TimeSpan.FromMilliseconds(runEntity.ElapsedMilliseconds),
                        AllocatedBytes: runEntity.MemoryAllocatedBytes,
                        CharacterCount: runEntity.CharacterCount,
                        WordCount: runEntity.WordCount,
                        OverallScore: runEntity.OverallScore,
                        Rank: runEntity.Rank,
                        MetricScores: metricScores,
                        ExtractedText: runEntity.ExtractedTextSnapshot,
                        ErrorMessage: runEntity.ErrorMessage
                    ));
                }

                var winner = engineRuns.OrderBy(r => r.Rank).FirstOrDefault();

                documentResults.Add(new BenchmarkDocumentResult(
                    FilePath: docEntity.FilePath,
                    FileName: docEntity.FileName,
                    Extension: docEntity.Extension,
                    Category: category,
                    FileSizeBytes: docEntity.FileSizeBytes,
                    EngineRuns: engineRuns,
                    WinnerEngine: winner
                ));
            }

            // Determine overall winner
            var overallWinner = documentResults
                .Select(d => d.WinnerEngine)
                .Where(w => w != null)
                .GroupBy(w => w!.EngineId)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.First();

            return new BenchmarkSessionResult(
                SessionId: entity.Id,
                Title: entity.Title,
                Category: category,
                CreatedAt: entity.CreatedAt,
                DocumentResults: documentResults,
                OverallWinnerEngineId: overallWinner?.EngineId,
                OverallWinnerDisplayName: overallWinner?.EngineDisplayName
            );
        }

        private static void AppendRow(StringBuilder sb, string metricName, IEnumerable<string> values)
        {
            sb.Append($"\"{EscapeCsv(metricName)}\"");
            foreach (var val in values)
            {
                sb.Append($",\"{EscapeCsv(val)}\"");
            }
            sb.AppendLine();
        }

        private static string EscapeCsv(string text)
        {
            return text.Replace("\"", "\"\"");
        }
    }
}
