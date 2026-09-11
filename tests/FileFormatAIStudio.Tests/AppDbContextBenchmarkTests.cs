using System;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class AppDbContextBenchmarkTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public AppDbContextBenchmarkTests()
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
        }

        [Fact]
        public async Task BenchmarkSession_WithDocumentsRunsAndMetrics_CanBePersistedAndRetrieved()
        {
            var sessionId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var runId1 = Guid.NewGuid();
            var runId2 = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                var session = new BenchmarkSessionEntity
                {
                    Id = sessionId,
                    Title = "Word Processing Text Extraction Benchmark",
                    Category = "Word",
                    TotalDocuments = 1,
                    CreatedAt = DateTime.UtcNow
                };

                var doc = new BenchmarkDocumentEntity
                {
                    Id = docId,
                    SessionId = sessionId,
                    FileName = "sample_agreement.docx",
                    FilePath = @"C:\docs\sample_agreement.docx",
                    Extension = ".docx",
                    Category = "Word",
                    FileSizeBytes = 1048576
                };

                var run1 = new BenchmarkRunResultEntity
                {
                    Id = runId1,
                    DocumentId = docId,
                    EngineId = "aspose",
                    EngineDisplayName = "Aspose Words Engine",
                    Status = "Success",
                    ElapsedMilliseconds = 48,
                    MemoryAllocatedBytes = 3145728,
                    CharacterCount = 142520,
                    WordCount = 21430,
                    OverallScore = 96.5,
                    Rank = 1,
                    ExtractedTextSnapshot = "Comprehensive legal agreement plain text..."
                };

                run1.MetricResults.Add(new BenchmarkMetricResultEntity
                {
                    RunResultId = runId1,
                    MetricId = "char_count",
                    DisplayName = "Total Character Count",
                    Unit = "chars",
                    RawValue = 142520,
                    FormattedValue = "142,520 chars",
                    NormalizedScore = 100.0,
                    Rank = 1,
                    HigherIsBetter = true,
                    Weight = 1.0,
                    Notes = "Highest character volume extracted"
                });

                run1.MetricResults.Add(new BenchmarkMetricResultEntity
                {
                    RunResultId = runId1,
                    MetricId = "latency_ms",
                    DisplayName = "Execution Latency",
                    Unit = "ms",
                    RawValue = 48,
                    FormattedValue = "48 ms",
                    NormalizedScore = 90.0,
                    Rank = 2,
                    HigherIsBetter = false,
                    Weight = 0.5
                });

                var run2 = new BenchmarkRunResultEntity
                {
                    Id = runId2,
                    DocumentId = docId,
                    EngineId = "dotnet-oss",
                    EngineDisplayName = ".NET OSS Engine (OpenXml)",
                    Status = "Success",
                    ElapsedMilliseconds = 32,
                    MemoryAllocatedBytes = 1835008,
                    CharacterCount = 118200,
                    WordCount = 17610,
                    OverallScore = 82.1,
                    Rank = 2,
                    ExtractedTextSnapshot = "Legal agreement plain text without header notes..."
                };

                doc.RunResults.Add(run1);
                doc.RunResults.Add(run2);
                session.Documents.Add(doc);

                context.BenchmarkSessions.Add(session);
                await context.SaveChangesAsync();
            }

            // Assert from a new context instance
            using (var context = new AppDbContext(_options))
            {
                var loadedSession = await context.BenchmarkSessions
                    .Include(s => s.Documents)
                        .ThenInclude(d => d.RunResults)
                            .ThenInclude(r => r.MetricResults)
                    .FirstOrDefaultAsync(s => s.Id == sessionId);

                loadedSession.Should().NotBeNull();
                loadedSession!.Title.Should().Be("Word Processing Text Extraction Benchmark");
                loadedSession.Category.Should().Be("Word");
                loadedSession.Documents.Should().HaveCount(1);

                var loadedDoc = loadedSession.Documents.First();
                loadedDoc.FileName.Should().Be("sample_agreement.docx");
                loadedDoc.RunResults.Should().HaveCount(2);

                var asposeRun = loadedDoc.RunResults.First(r => r.EngineId == "aspose");
                asposeRun.Rank.Should().Be(1);
                asposeRun.CharacterCount.Should().Be(142520);
                asposeRun.MetricResults.Should().HaveCount(2);

                var charMetric = asposeRun.MetricResults.First(m => m.MetricId == "char_count");
                charMetric.RawValue.Should().Be(142520);
                charMetric.NormalizedScore.Should().Be(100.0);
            }
        }

        [Fact]
        public async Task DeletingBenchmarkSession_CascadesDeleteToDocumentsRunsAndMetrics()
        {
            var sessionId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            var metricId = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                var session = new BenchmarkSessionEntity
                {
                    Id = sessionId,
                    Title = "Session to Delete",
                    Category = "Excel"
                };

                var doc = new BenchmarkDocumentEntity
                {
                    Id = docId,
                    SessionId = sessionId,
                    FileName = "financials.xlsx",
                    Category = "Excel"
                };

                var run = new BenchmarkRunResultEntity
                {
                    Id = runId,
                    DocumentId = docId,
                    EngineId = "aspose",
                    EngineDisplayName = "Aspose Cells"
                };

                var metric = new BenchmarkMetricResultEntity
                {
                    Id = metricId,
                    RunResultId = runId,
                    MetricId = "char_count",
                    RawValue = 50000
                };

                run.MetricResults.Add(metric);
                doc.RunResults.Add(run);
                session.Documents.Add(doc);

                context.BenchmarkSessions.Add(session);
                await context.SaveChangesAsync();
            }

            // Delete session and verify cascading deletion
            using (var context = new AppDbContext(_options))
            {
                var session = await context.BenchmarkSessions.FindAsync(sessionId);
                session.Should().NotBeNull();
                context.BenchmarkSessions.Remove(session!);
                await context.SaveChangesAsync();
            }

            // Assert everything was cascade deleted
            using (var context = new AppDbContext(_options))
            {
                (await context.BenchmarkSessions.AnyAsync(s => s.Id == sessionId)).Should().BeFalse();
                (await context.BenchmarkDocuments.AnyAsync(d => d.Id == docId)).Should().BeFalse();
                (await context.BenchmarkRunResults.AnyAsync(r => r.Id == runId)).Should().BeFalse();
                (await context.BenchmarkMetricResults.AnyAsync(m => m.Id == metricId)).Should().BeFalse();
            }
        }

        [Fact]
        public async Task DeletingBenchmarkDocument_CascadesDeleteToRunsAndMetrics()
        {
            var sessionId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            var metricId = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                var session = new BenchmarkSessionEntity
                {
                    Id = sessionId,
                    Title = "Multi-Doc Session",
                    Category = "Pdf"
                };

                var doc = new BenchmarkDocumentEntity
                {
                    Id = docId,
                    SessionId = sessionId,
                    FileName = "whitepaper.pdf",
                    Category = "Pdf"
                };

                var run = new BenchmarkRunResultEntity
                {
                    Id = runId,
                    DocumentId = docId,
                    EngineId = "dotnet-oss"
                };

                var metric = new BenchmarkMetricResultEntity
                {
                    Id = metricId,
                    RunResultId = runId,
                    MetricId = "char_count",
                    RawValue = 12000
                };

                run.MetricResults.Add(metric);
                doc.RunResults.Add(run);
                session.Documents.Add(doc);

                context.BenchmarkSessions.Add(session);
                await context.SaveChangesAsync();
            }

            // Delete document only
            using (var context = new AppDbContext(_options))
            {
                var doc = await context.BenchmarkDocuments.FindAsync(docId);
                doc.Should().NotBeNull();
                context.BenchmarkDocuments.Remove(doc!);
                await context.SaveChangesAsync();
            }

            // Assert session remains, but doc, runs, and metrics are deleted
            using (var context = new AppDbContext(_options))
            {
                (await context.BenchmarkSessions.AnyAsync(s => s.Id == sessionId)).Should().BeTrue();
                (await context.BenchmarkDocuments.AnyAsync(d => d.Id == docId)).Should().BeFalse();
                (await context.BenchmarkRunResults.AnyAsync(r => r.Id == runId)).Should().BeFalse();
                (await context.BenchmarkMetricResults.AnyAsync(m => m.Id == metricId)).Should().BeFalse();
            }
        }
    }
}

