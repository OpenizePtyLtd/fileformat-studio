using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Benchmarking.Metrics;
using FileFormatAIStudio.Services.Parsing;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class BenchmarkRunnerServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly DocumentCategoryRegistry _categoryRegistry = new();
        private readonly List<string> _tempFiles = new();

        public BenchmarkRunnerServiceTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }

        private string CreateTempFile(string extension = ".docx", string content = "Sample file content")
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
            File.WriteAllText(filePath, content);
            _tempFiles.Add(filePath);
            return filePath;
        }

        private class MockParser : IDocumentParser
        {
            public string EngineId { get; init; } = string.Empty;
            public string DisplayName { get; init; } = string.Empty;
            public int Priority { get; init; } = 50;
            public bool IsAvailable { get; init; } = true;
            public IReadOnlySet<string> SupportedExtensions { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Func<string, string>? ExtractFunc { get; init; }

            public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
            {
                if (ExtractFunc != null)
                {
                    return Task.FromResult(ExtractFunc(filePath));
                }
                return Task.FromResult($"Extracted by {EngineId}");
            }
        }

        [Fact]
        public async Task RunBenchmarkAsync_WithMultipleEngines_RanksHighestCharacterCountAsWinner()
        {
            var tempFile = CreateTempFile(".docx", "Placeholder text for testing");

            var p1 = new MockParser
            {
                EngineId = "aspose",
                DisplayName = "Aspose Words",
                SupportedExtensions = new HashSet<string>(new[] { ".docx" }, StringComparer.OrdinalIgnoreCase),
                ExtractFunc = _ => new string('A', 10000) // 10,000 characters
            };

            var p2 = new MockParser
            {
                EngineId = "dotnet-oss",
                DisplayName = ".NET OSS OpenXml",
                SupportedExtensions = new HashSet<string>(new[] { ".docx" }, StringComparer.OrdinalIgnoreCase),
                ExtractFunc = _ => new string('B', 7000) // 7,000 characters
            };

            var factory = new DocumentParserFactory(new[] { p1, p2 });

            using var dbContext = new AppDbContext(_options);
            var runner = new BenchmarkRunnerService(dbContext, factory, _categoryRegistry);

            var result = await runner.RunBenchmarkAsync(tempFile);

            result.Should().NotBeNull();
            result.Category.Should().Be(DocumentCategory.Word);
            result.DocumentResults.Should().HaveCount(1);

            var docResult = result.DocumentResults[0];
            docResult.EngineRuns.Should().HaveCount(2);

            // Aspose should be Rank 1 (10,000 chars = 100.0 score)
            var asposeRun = docResult.EngineRuns.First(r => r.EngineId == "aspose");
            asposeRun.Rank.Should().Be(1);
            asposeRun.CharacterCount.Should().Be(10000);
            asposeRun.OverallScore.Should().Be(100.0);
            asposeRun.IsSuccess.Should().BeTrue();

            // .NET OSS should be Rank 2 (7,000 chars = 70.0 score)
            var ossRun = docResult.EngineRuns.First(r => r.EngineId == "dotnet-oss");
            ossRun.Rank.Should().Be(2);
            ossRun.CharacterCount.Should().Be(7000);
            ossRun.OverallScore.Should().Be(70.0);
            ossRun.IsSuccess.Should().BeTrue();

            // Document winner and session winner should be Aspose
            docResult.WinnerEngine.Should().NotBeNull();
            docResult.WinnerEngine!.EngineId.Should().Be("aspose");
            result.OverallWinnerEngineId.Should().Be("aspose");

            // Verify persistence in SQLite
            var savedSession = await dbContext.BenchmarkSessions
                .Include(s => s.Documents)
                    .ThenInclude(d => d.RunResults)
                        .ThenInclude(r => r.MetricResults)
                .FirstOrDefaultAsync(s => s.Id == result.SessionId);

            savedSession.Should().NotBeNull();
            savedSession!.Documents.Should().HaveCount(1);
            savedSession.Documents[0].RunResults.Should().HaveCount(2);
        }

        [Fact]
        public async Task RunBenchmarkAsync_WhenOneEngineThrows_MaintainsIsolatedErrorBoundary()
        {
            var tempFile = CreateTempFile(".pdf", "PDF test file");

            var p1 = new MockParser
            {
                EngineId = "aspose",
                DisplayName = "Aspose PDF",
                SupportedExtensions = new HashSet<string>(new[] { ".pdf" }, StringComparer.OrdinalIgnoreCase),
                ExtractFunc = _ => "Valid PDF text content extracted."
            };

            var p2 = new MockParser
            {
                EngineId = "failing-parser",
                DisplayName = "Failing Engine",
                SupportedExtensions = new HashSet<string>(new[] { ".pdf" }, StringComparer.OrdinalIgnoreCase),
                ExtractFunc = _ => throw new InvalidOperationException("Corrupted PDF document stream.")
            };

            var factory = new DocumentParserFactory(new[] { p1, p2 });

            using var dbContext = new AppDbContext(_options);
            var runner = new BenchmarkRunnerService(dbContext, factory, _categoryRegistry);

            var result = await runner.RunBenchmarkAsync(tempFile);

            var docResult = result.DocumentResults[0];
            docResult.EngineRuns.Should().HaveCount(2);

            var successfulRun = docResult.EngineRuns.First(r => r.EngineId == "aspose");
            successfulRun.IsSuccess.Should().BeTrue();
            successfulRun.Rank.Should().Be(1);
            successfulRun.CharacterCount.Should().Be(33);

            var failedRun = docResult.EngineRuns.First(r => r.EngineId == "failing-parser");
            failedRun.IsSuccess.Should().BeFalse();
            failedRun.Rank.Should().Be(2);
            failedRun.CharacterCount.Should().Be(0);
            failedRun.OverallScore.Should().Be(0.0);
            failedRun.ErrorMessage.Should().Contain("Corrupted PDF document stream");
        }

        [Fact]
        public async Task RunBenchmarkAsync_WithSelectedEngineFilter_OnlyExecutesSelectedEngines()
        {
            var tempFile = CreateTempFile(".xlsx", "data");

            var p1 = new MockParser
            {
                EngineId = "aspose",
                DisplayName = "Aspose Cells",
                SupportedExtensions = new HashSet<string>(new[] { ".xlsx" }, StringComparer.OrdinalIgnoreCase),
                ExtractFunc = _ => "Aspose extracted text"
            };

            var p2 = new MockParser
            {
                EngineId = "dotnet-oss",
                DisplayName = ".NET OSS Excel",
                SupportedExtensions = new HashSet<string>(new[] { ".xlsx" }, StringComparer.OrdinalIgnoreCase),
                ExtractFunc = _ => "OSS extracted text"
            };

            var p3 = new MockParser
            {
                EngineId = "unwanted-engine",
                DisplayName = "Ignored Engine",
                SupportedExtensions = new HashSet<string>(new[] { ".xlsx" }, StringComparer.OrdinalIgnoreCase),
                ExtractFunc = _ => "Ignored text"
            };

            var factory = new DocumentParserFactory(new[] { p1, p2, p3 });

            using var dbContext = new AppDbContext(_options);
            var runner = new BenchmarkRunnerService(dbContext, factory, _categoryRegistry);

            var options = new BenchmarkOptions(
                SelectedEngineIds: new[] { "aspose", "dotnet-oss" }
            );

            var result = await runner.RunBenchmarkAsync(tempFile, options);

            var docResult = result.DocumentResults[0];
            docResult.EngineRuns.Should().HaveCount(2);
            docResult.EngineRuns.Select(r => r.EngineId).Should().Contain(new[] { "aspose", "dotnet-oss" })
                .And.NotContain("unwanted-engine");
        }

        [Fact]
        public async Task HistoryAndDeletion_CanRetrieveAndCascadeDeleteSessions()
        {
            var tempFile = CreateTempFile(".docx", "Test Content");

            var p1 = new MockParser
            {
                EngineId = "aspose",
                SupportedExtensions = new HashSet<string>(new[] { ".docx" }, StringComparer.OrdinalIgnoreCase)
            };

            var factory = new DocumentParserFactory(new[] { p1 });

            using var dbContext = new AppDbContext(_options);
            var runner = new BenchmarkRunnerService(dbContext, factory, _categoryRegistry);

            var runResult = await runner.RunBenchmarkAsync(tempFile);

            var history = await runner.GetBenchmarkHistoryAsync();
            history.Should().NotBeEmpty();
            history.Should().Contain(s => s.Id == runResult.SessionId);

            var singleSession = await runner.GetBenchmarkSessionAsync(runResult.SessionId);
            singleSession.Should().NotBeNull();
            singleSession!.Documents.Should().HaveCount(1);

            // Delete session
            var deleted = await runner.DeleteBenchmarkSessionAsync(runResult.SessionId);
            deleted.Should().BeTrue();

            var afterDelete = await runner.GetBenchmarkSessionAsync(runResult.SessionId);
            afterDelete.Should().BeNull();
        }

        [Fact]
        public async Task ProgressReporting_EmitsProgressCallbacksCorrectly()
        {
            var tempFile = CreateTempFile(".docx", "Document text");

            var p1 = new MockParser
            {
                EngineId = "aspose",
                DisplayName = "Aspose Engine",
                SupportedExtensions = new HashSet<string>(new[] { ".docx" }, StringComparer.OrdinalIgnoreCase)
            };

            var factory = new DocumentParserFactory(new[] { p1 });

            using var dbContext = new AppDbContext(_options);
            var runner = new BenchmarkRunnerService(dbContext, factory, _categoryRegistry);

            var progressReports = new List<BenchmarkProgressReport>();
            var progress = new DirectProgress<BenchmarkProgressReport>(r => progressReports.Add(r));

            await runner.RunBenchmarkAsync(tempFile, progress: progress);

            progressReports.Should().NotBeEmpty();
            progressReports.Should().Contain(r => r.Stage == "Preparing");
            progressReports.Should().Contain(r => r.Stage == "Parsing");
            progressReports.Should().Contain(r => r.Stage == "Evaluating Metrics");
            progressReports.Should().Contain(r => r.Stage == "Completed");
            progressReports.Last().PercentComplete.Should().Be(100.0);
        }

        [Fact]
        public async Task RunBatchBenchmarkAsync_WithMultipleFiles_AggregatesResultsAndCalculatesOverallWinner()
        {
            var file1 = CreateTempFile(".docx", "Doc 1 text");
            var file2 = CreateTempFile(".docx", "Doc 2 text");

            var p1 = new MockParser
            {
                EngineId = "aspose",
                DisplayName = "Aspose",
                SupportedExtensions = new HashSet<string>(new[] { ".docx" }, StringComparer.OrdinalIgnoreCase),
                ExtractFunc = path => path == file1 ? new string('A', 5000) : new string('A', 6000)
            };

            var p2 = new MockParser
            {
                EngineId = "dotnet-oss",
                DisplayName = ".NET OSS",
                SupportedExtensions = new HashSet<string>(new[] { ".docx" }, StringComparer.OrdinalIgnoreCase),
                ExtractFunc = path => path == file1 ? new string('B', 2000) : new string('B', 3000)
            };

            var factory = new DocumentParserFactory(new[] { p1, p2 });

            using var dbContext = new AppDbContext(_options);
            var runner = new BenchmarkRunnerService(dbContext, factory, _categoryRegistry);

            var batchResult = await runner.RunBatchBenchmarkAsync(new[] { file1, file2 });

            batchResult.Should().NotBeNull();
            batchResult.DocumentResults.Should().HaveCount(2);
            batchResult.OverallWinnerEngineId.Should().Be("aspose");
            batchResult.OverallWinnerDisplayName.Should().Be("Aspose");

            // Document 1 winner
            batchResult.DocumentResults[0].WinnerEngine!.EngineId.Should().Be("aspose");
            // Document 2 winner
            batchResult.DocumentResults[1].WinnerEngine!.EngineId.Should().Be("aspose");
        }

        [Fact]
        public async Task RunBenchmarkAsync_WithCancellationToken_ThrowsOperationCanceledException()
        {
            var tempFile = CreateTempFile(".docx", "Cancelled doc");

            var p1 = new MockParser
            {
                EngineId = "aspose",
                SupportedExtensions = new HashSet<string>(new[] { ".docx" }, StringComparer.OrdinalIgnoreCase)
            };

            var factory = new DocumentParserFactory(new[] { p1 });

            using var dbContext = new AppDbContext(_options);
            var runner = new BenchmarkRunnerService(dbContext, factory, _categoryRegistry);

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            Func<Task> act = async () => await runner.RunBenchmarkAsync(tempFile, cancellationToken: cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task RunBenchmarkAsync_WithSaveToDatabaseFalse_DoesNotCreateEntitiesInDatabase()
        {
            var tempFile = CreateTempFile(".docx", "In-memory benchmark doc");

            var p1 = new MockParser
            {
                EngineId = "aspose",
                SupportedExtensions = new HashSet<string>(new[] { ".docx" }, StringComparer.OrdinalIgnoreCase),
                ExtractFunc = _ => "In memory extraction"
            };

            var factory = new DocumentParserFactory(new[] { p1 });

            using var dbContext = new AppDbContext(_options);
            var runner = new BenchmarkRunnerService(dbContext, factory, _categoryRegistry);

            var options = new BenchmarkOptions(SaveToDatabase: false);
            var result = await runner.RunBenchmarkAsync(tempFile, options);

            result.Should().NotBeNull();

            // Verify database has no records for this session
            var saved = await dbContext.BenchmarkSessions.FindAsync(result.SessionId);
            saved.Should().BeNull();
        }

        [Fact]
        public async Task RunBenchmarkAsync_WithCustomTitle_PersistsCustomTitleProperly()
        {
            var tempFile = CreateTempFile(".docx", "Custom title doc");

            var p1 = new MockParser
            {
                EngineId = "aspose",
                SupportedExtensions = new HashSet<string>(new[] { ".docx" }, StringComparer.OrdinalIgnoreCase)
            };

            var factory = new DocumentParserFactory(new[] { p1 });

            using var dbContext = new AppDbContext(_options);
            var runner = new BenchmarkRunnerService(dbContext, factory, _categoryRegistry);

            var options = new BenchmarkOptions(CustomSessionTitle: "Q3 Executive Document Parsing Audit");
            var result = await runner.RunBenchmarkAsync(tempFile, options);

            result.Title.Should().Be("Q3 Executive Document Parsing Audit");

            var sessionEntity = await dbContext.BenchmarkSessions.FindAsync(result.SessionId);
            sessionEntity.Should().NotBeNull();
            sessionEntity!.Title.Should().Be("Q3 Executive Document Parsing Audit");
        }

        [Fact]
        public async Task GetBenchmarkHistoryAsync_WithCategoryFilter_FiltersSessionsCorrectly()
        {
            var wordFile = CreateTempFile(".docx", "Word document");
            var excelFile = CreateTempFile(".xlsx", "Excel sheet");

            var p1 = new MockParser
            {
                EngineId = "aspose",
                SupportedExtensions = new HashSet<string>(new[] { ".docx", ".xlsx" }, StringComparer.OrdinalIgnoreCase)
            };

            var factory = new DocumentParserFactory(new[] { p1 });

            using var dbContext = new AppDbContext(_options);
            var runner = new BenchmarkRunnerService(dbContext, factory, _categoryRegistry);

            var wordResult = await runner.RunBenchmarkAsync(wordFile);
            var excelResult = await runner.RunBenchmarkAsync(excelFile);

            var wordHistory = await runner.GetBenchmarkHistoryAsync(DocumentCategory.Word);
            wordHistory.Should().Contain(s => s.Id == wordResult.SessionId);
            wordHistory.Should().NotContain(s => s.Id == excelResult.SessionId);

            var excelHistory = await runner.GetBenchmarkHistoryAsync(DocumentCategory.Excel);
            excelHistory.Should().Contain(s => s.Id == excelResult.SessionId);
            excelHistory.Should().NotContain(s => s.Id == wordResult.SessionId);
        }

        [Fact]
        public async Task RunBenchmarkAsync_WithSelectedMetricIds_OnlyEvaluatesSelectedMetrics()
        {
            var tempFile = CreateTempFile(".docx", "Selective metrics doc");

            var p1 = new MockParser
            {
                EngineId = "aspose",
                DisplayName = "Aspose",
                SupportedExtensions = new HashSet<string>(new[] { ".docx" }, StringComparer.OrdinalIgnoreCase),
                ExtractFunc = _ => "Selective text"
            };

            var factory = new DocumentParserFactory(new[] { p1 });

            using var dbContext = new AppDbContext(_options);
            var metrics = new IBenchmarkMetric[] { new CharacterCountMetric(), new ExecutionLatencyMetric(), new MemoryAllocationMetric() };
            var runner = new BenchmarkRunnerService(dbContext, factory, _categoryRegistry, metrics);

            var options = new BenchmarkOptions(
                SelectedMetricIds: new[] { "char_count", "latency_ms" }
            );

            var result = await runner.RunBenchmarkAsync(tempFile, options);

            var run = result.DocumentResults[0].EngineRuns[0];
            run.MetricScores.Should().HaveCount(2);
            run.MetricScores.Select(m => m.MetricId).Should().Contain(new[] { "char_count", "latency_ms" });
            run.MetricScores.Select(m => m.MetricId).Should().NotContain("mem_alloc_mb");
        }

        private class DirectProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;
            public DirectProgress(Action<T> handler) => _handler = handler;
            public void Report(T value) => _handler(value);
        }
    }
}

