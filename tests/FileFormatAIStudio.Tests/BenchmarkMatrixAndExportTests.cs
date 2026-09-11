using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class BenchmarkMatrixAndExportTests : IDisposable
    {
        private readonly List<string> _tempFiles = new();
        private readonly BenchmarkExportService _exportService = new();

        public void Dispose()
        {
            foreach (var f in _tempFiles)
            {
                if (File.Exists(f))
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }

        private BenchmarkSessionResult CreateSampleSessionResult()
        {
            var sessionId = Guid.NewGuid();
            var doc1Runs = new List<BenchmarkEngineRunResult>
            {
                new(
                    EngineId: "aspose_words",
                    EngineDisplayName: "Aspose.Words",
                    IsSuccess: true,
                    ElapsedTime: TimeSpan.FromMilliseconds(48),
                    AllocatedBytes: 1_240_000,
                    CharacterCount: 142_520,
                    WordCount: 24_500,
                    OverallScore: 98.5,
                    Rank: 1,
                    MetricScores: new List<MetricScoreResult>
                    {
                        new("char_count", "Character Count", "chars", 142520, "142,520 chars", 100, 1, true, 0.4),
                        new("content_char_count", "Content Characters", "chars", 119800, "119,800 chars", 100, 1, true, 0.15),
                        new("latency_ms", "Latency", "ms", 48, "48 ms", 95, 1, false, 0.15),
                        new("cleanliness_score", "Cleanliness", "%", 100.0, "100.0%", 100, 1, true, 0.1)
                    },
                    ExtractedText: "Aspose full extracted text..."
                ),
                new(
                    EngineId: "dotnet_oss",
                    EngineDisplayName: ".NET OSS (OpenXML)",
                    IsSuccess: true,
                    ElapsedTime: TimeSpan.FromMilliseconds(120),
                    AllocatedBytes: 2_560_000,
                    CharacterCount: 110_200,
                    WordCount: 19_800,
                    OverallScore: 78.2,
                    Rank: 2,
                    MetricScores: new List<MetricScoreResult>
                    {
                        new("char_count", "Character Count", "chars", 110200, "110,200 chars", 77.3, 2, true, 0.4),
                        new("content_char_count", "Content Characters", "chars", 92000, "92,000 chars", 76.8, 2, true, 0.15),
                        new("latency_ms", "Latency", "ms", 120, "120 ms", 60, 2, false, 0.15),
                        new("cleanliness_score", "Cleanliness", "%", 95.5, "95.5%", 95.5, 2, true, 0.1)
                    },
                    ExtractedText: "OSS extracted text..."
                )
            };

            var doc1 = new BenchmarkDocumentResult(
                FilePath: @"C:\Docs\Contract.docx",
                FileName: "Contract.docx",
                Extension: ".docx",
                Category: DocumentCategory.Word,
                FileSizeBytes: 45_000,
                EngineRuns: doc1Runs,
                WinnerEngine: doc1Runs[0]
            );

            return new BenchmarkSessionResult(
                SessionId: sessionId,
                Title: "Word Benchmark Session",
                Category: DocumentCategory.Word,
                CreatedAt: DateTime.UtcNow,
                DocumentResults: new List<BenchmarkDocumentResult> { doc1 },
                OverallWinnerEngineId: "aspose_words",
                OverallWinnerDisplayName: "Aspose.Words"
            );
        }

        [Fact]
        public void ExportService_ExportToCsv_GeneratesValidCsvHeadersAndMetrics()
        {
            var session = CreateSampleSessionResult();

            string csv = _exportService.ExportToCsv(session);

            csv.Should().NotBeNullOrWhiteSpace();
            csv.Should().Contain("# Benchmark Session Report");
            csv.Should().Contain("Session Title,\"Word Benchmark Session\"");
            csv.Should().Contain("Overall Winner,\"Aspose.Words\"");
            csv.Should().Contain("## Document: \"Contract.docx\"");
            csv.Should().Contain("Metric,\"Aspose.Words\",\".NET OSS (OpenXML)\"");
            csv.Should().Contain("Total Characters (Baseline)");
            csv.Should().Contain("142520");
            csv.Should().Contain("110200");
            csv.Should().Contain("Winner 🥇");
        }

        [Fact]
        public void ExportService_ExportToJson_GeneratesStructuredValidJson()
        {
            var session = CreateSampleSessionResult();

            string json = _exportService.ExportToJson(session);

            json.Should().NotBeNullOrWhiteSpace();
            json.Should().Contain("\"title\": \"Word Benchmark Session\"");
            json.Should().Contain("\"overallWinnerDisplayName\": \"Aspose.Words\"");
            json.Should().Contain("\"documentResults\"");
            json.Should().Contain("\"characterCount\": 142520");
        }

        [Fact]
        public void ExportService_MapEntityToResult_MapsGraphsCorrectly()
        {
            var sessionId = Guid.NewGuid();
            var docId = Guid.NewGuid();
            var runId = Guid.NewGuid();

            var entity = new BenchmarkSessionEntity
            {
                Id = sessionId,
                Title = "Historical Run",
                Category = "Excel",
                CreatedAt = DateTime.UtcNow,
                Documents = new List<BenchmarkDocumentEntity>
                {
                    new()
                    {
                        Id = docId,
                        SessionId = sessionId,
                        FileName = "Sales.xlsx",
                        FilePath = @"C:\Data\Sales.xlsx",
                        Extension = ".xlsx",
                        Category = "Excel",
                        FileSizeBytes = 120_000,
                        RunResults = new List<BenchmarkRunResultEntity>
                        {
                            new()
                            {
                                Id = runId,
                                DocumentId = docId,
                                EngineId = "aspose_cells",
                                EngineDisplayName = "Aspose.Cells",
                                Status = "Success",
                                ElapsedMilliseconds = 65,
                                MemoryAllocatedBytes = 2_000_000,
                                CharacterCount = 85_000,
                                WordCount = 12_000,
                                OverallScore = 95.0,
                                Rank = 1,
                                ExtractedTextSnapshot = "Quarterly Sales figures...",
                                MetricResults = new List<BenchmarkMetricResultEntity>
                                {
                                    new()
                                    {
                                        RunResultId = runId,
                                        MetricId = "char_count",
                                        DisplayName = "Character Count",
                                        RawValue = 85000,
                                        FormattedValue = "85,000 chars",
                                        NormalizedScore = 100,
                                        Rank = 1,
                                        HigherIsBetter = true
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var domainResult = _exportService.MapEntityToResult(entity);

            domainResult.Should().NotBeNull();
            domainResult.SessionId.Should().Be(sessionId);
            domainResult.Title.Should().Be("Historical Run");
            domainResult.Category.Should().Be(DocumentCategory.Excel);
            domainResult.OverallWinnerEngineId.Should().Be("aspose_cells");
            domainResult.DocumentResults.Should().HaveCount(1);
            domainResult.DocumentResults[0].WinnerEngine.Should().NotBeNull();
            domainResult.DocumentResults[0].WinnerEngine!.CharacterCount.Should().Be(85000);
        }

        [Fact]
        public void BenchmarkViewModel_SettingActiveResult_GeneratesScorecardsAndMatrix()
        {
            var mockRunner = new MockBenchmarkRunner();
            var registry = new DocumentCategoryRegistry();
            var vm = new BenchmarkViewModel(mockRunner, registry, _exportService);

            var session = CreateSampleSessionResult();
            vm.ActiveResult = session;

            vm.HasActiveResult.Should().BeTrue();
            vm.WinnerScorecard.Should().NotBeNull();
            vm.WinnerScorecard!.EngineDisplayName.Should().Be("Aspose.Words");
            vm.WinnerScorecard.IsWinner.Should().BeTrue();
            vm.WinnerScorecard.Rank.Should().Be(1);
            vm.WinnerScorecard.RankBadgeText.Should().Contain("🥇");
            vm.WinnerScorecard.CharacterCount.Should().Be(142520);

            vm.WinnerBannerTitle.Should().Contain("Aspose.Words");
            vm.WinnerBannerTitle.Should().Contain("Rank #1");

            vm.EngineScorecards.Should().HaveCount(2);
            vm.EngineScorecards[0].EngineDisplayName.Should().Be("Aspose.Words");
            vm.EngineScorecards[1].EngineDisplayName.Should().Be(".NET OSS (OpenXML)");
            vm.EngineScorecards[1].Rank.Should().Be(2);
            vm.EngineScorecards[1].RankBadgeText.Should().Contain("🥈");

            vm.MatrixEngineHeaders.Should().ContainInOrder("Aspose.Words", ".NET OSS (OpenXML)");
            vm.MatrixRows.Should().NotBeEmpty();

            var charRow = vm.MatrixRows.FirstOrDefault(r => r.MetricName.Contains("Total Characters"));
            charRow.Should().NotBeNull();
            charRow!.Cells.Should().HaveCount(2);
            charRow.Cells[0].IsBest.Should().BeTrue();
            charRow.Cells[1].IsBest.Should().BeFalse();
        }

        [Fact]
        public async Task BenchmarkViewModel_ViewSessionAsync_LoadsHistoricalDetails()
        {
            var sessionId = Guid.NewGuid();
            var entity = new BenchmarkSessionEntity
            {
                Id = sessionId,
                Title = "Past PDF Session",
                Category = "Pdf",
                CreatedAt = DateTime.UtcNow,
                Documents = new List<BenchmarkDocumentEntity>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        SessionId = sessionId,
                        FileName = "Doc.pdf",
                        FilePath = @"C:\Doc.pdf",
                        Extension = ".pdf",
                        Category = "Pdf",
                        FileSizeBytes = 50_000,
                        RunResults = new List<BenchmarkRunResultEntity>
                        {
                            new()
                            {
                                Id = Guid.NewGuid(),
                                EngineId = "aspose_pdf",
                                EngineDisplayName = "Aspose.PDF",
                                Status = "Success",
                                CharacterCount = 50000,
                                WordCount = 8000,
                                OverallScore = 99.0,
                                Rank = 1,
                                ElapsedMilliseconds = 40
                            }
                        }
                    }
                }
            };

            var mockRunner = new MockBenchmarkRunner();
            mockRunner.HistoryDetails[sessionId] = entity;
            var registry = new DocumentCategoryRegistry();
            var vm = new BenchmarkViewModel(mockRunner, registry, _exportService);

            await vm.ViewSessionAsync(entity);

            vm.HasActiveResult.Should().BeTrue();
            vm.ActiveResult!.Title.Should().Be("Past PDF Session");
            vm.WinnerScorecard.Should().NotBeNull();
            vm.WinnerScorecard!.EngineDisplayName.Should().Be("Aspose.PDF");
        }

        [Fact]
        public async Task BenchmarkViewModel_ExportToFileAsync_WritesValidFile()
        {
            var mockRunner = new MockBenchmarkRunner();
            var registry = new DocumentCategoryRegistry();
            var vm = new BenchmarkViewModel(mockRunner, registry, _exportService);

            vm.ActiveResult = CreateSampleSessionResult();

            var tempCsv = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
            var tempJson = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            _tempFiles.Add(tempCsv);
            _tempFiles.Add(tempJson);

            bool csvExported = await vm.ExportToFileAsync(tempCsv, "csv");
            bool jsonExported = await vm.ExportToFileAsync(tempJson, "json");

            csvExported.Should().BeTrue();
            jsonExported.Should().BeTrue();

            File.Exists(tempCsv).Should().BeTrue();
            File.Exists(tempJson).Should().BeTrue();

            var csvContent = await File.ReadAllTextAsync(tempCsv);
            csvContent.Should().Contain("Aspose.Words");

            var jsonContent = await File.ReadAllTextAsync(tempJson);
            jsonContent.Should().Contain("aspose_words");
        }

        private class MockBenchmarkRunner : IBenchmarkRunnerService
        {
            public Dictionary<Guid, BenchmarkSessionEntity> HistoryDetails { get; } = new();

            public Task<BenchmarkSessionResult> RunBenchmarkAsync(string filePath, BenchmarkOptions? options = null, IProgress<BenchmarkProgressReport>? progress = null, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException();

            public Task<BenchmarkSessionResult> RunBatchBenchmarkAsync(IEnumerable<string> filePaths, BenchmarkOptions? options = null, IProgress<BenchmarkProgressReport>? progress = null, CancellationToken cancellationToken = default) =>
                throw new NotImplementedException();

            public Task<IReadOnlyList<BenchmarkSessionEntity>> GetBenchmarkHistoryAsync(DocumentCategory? category = null, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<BenchmarkSessionEntity>>(HistoryDetails.Values.ToList().AsReadOnly());

            public Task<BenchmarkSessionEntity?> GetBenchmarkSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            {
                HistoryDetails.TryGetValue(sessionId, out var session);
                return Task.FromResult(session);
            }

            public Task<bool> DeleteBenchmarkSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            {
                HistoryDetails.Remove(sessionId);
                return Task.FromResult(true);
            }
        }
    }
}
