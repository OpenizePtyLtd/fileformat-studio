using System;
using System.Collections.Generic;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Benchmarking.Metrics;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class BenchmarkMetricsTests
    {
        private BenchmarkExecutionContext CreateContext(
            string engineId,
            string text,
            TimeSpan elapsed,
            long allocatedBytes = 1048576,
            bool isSuccess = true,
            Exception? ex = null,
            long fileSizeBytes = 102400)
        {
            return new BenchmarkExecutionContext(
                FilePath: @"C:\docs\sample.docx",
                FileName: "sample.docx",
                Extension: ".docx",
                Category: DocumentCategory.Word,
                FileSizeBytes: fileSizeBytes,
                EngineId: engineId,
                EngineDisplayName: $"Engine {engineId}",
                ExtractedText: text,
                ElapsedTime: elapsed,
                AllocatedBytes: allocatedBytes,
                IsSuccess: isSuccess,
                ThrownException: ex
            );
        }

        [Fact]
        public void CharacterCountMetric_RanksHighestExtractedCharactersAsRank1()
        {
            var metric = new CharacterCountMetric();

            var ctx1 = CreateContext("aspose", new string('X', 5000), TimeSpan.FromMilliseconds(50));
            var ctx2 = CreateContext("dotnet-oss", new string('Y', 2500), TimeSpan.FromMilliseconds(30));
            var competitors = new List<BenchmarkExecutionContext> { ctx1, ctx2 };

            var score1 = metric.Evaluate(ctx1, competitors);
            var score2 = metric.Evaluate(ctx2, competitors);

            score1.Rank.Should().Be(1);
            score1.NormalizedScore.Should().Be(100.0);
            score1.RawValue.Should().Be(5000);
            score1.FormattedValue.Should().Be("5,000 chars");

            score2.Rank.Should().Be(2);
            score2.NormalizedScore.Should().Be(50.0);
            score2.RawValue.Should().Be(2500);
        }

        [Fact]
        public void ContentCharacterCountMetric_FiltersWhitespaceAndPadding()
        {
            var metric = new ContentCharacterCountMetric();

            // ctx1 has 2,000 content chars and 8,000 spaces/newlines (total 10,000)
            var paddedText = new string('A', 2000) + new string(' ', 4000) + new string('\n', 4000);
            var ctx1 = CreateContext("padded", paddedText, TimeSpan.FromMilliseconds(40));

            // ctx2 has 3,000 content chars and no padding (total 3,000)
            var denseText = new string('B', 3000);
            var ctx2 = CreateContext("dense", denseText, TimeSpan.FromMilliseconds(30));

            var competitors = new List<BenchmarkExecutionContext> { ctx1, ctx2 };

            var score1 = metric.Evaluate(ctx1, competitors);
            var score2 = metric.Evaluate(ctx2, competitors);

            // Dense should win on content characters (3,000 vs 2,000)
            score2.Rank.Should().Be(1);
            score2.NormalizedScore.Should().Be(100.0);
            score2.RawValue.Should().Be(3000);

            score1.Rank.Should().Be(2);
            score1.RawValue.Should().Be(2000);
            score1.NormalizedScore.Should().Be(66.7);
        }

        [Fact]
        public void WordAndTokenCountMetric_EvaluatesLexicalDensityAccurately()
        {
            var metric = new WordAndTokenCountMetric();

            var text1 = "The quick brown fox jumps over the lazy dog in the summer heat."; // 13 words
            var text2 = "Hello world."; // 2 words

            var ctx1 = CreateContext("rich", text1, TimeSpan.FromMilliseconds(20));
            var ctx2 = CreateContext("sparse", text2, TimeSpan.FromMilliseconds(10));
            var competitors = new List<BenchmarkExecutionContext> { ctx1, ctx2 };

            var score1 = metric.Evaluate(ctx1, competitors);
            var score2 = metric.Evaluate(ctx2, competitors);

            score1.Rank.Should().Be(1);
            score1.NormalizedScore.Should().Be(100.0);
            score1.RawValue.Should().Be(13);
            score1.FormattedValue.Should().Contain("13 words");

            score2.Rank.Should().Be(2);
            score2.RawValue.Should().Be(2);
        }

        [Fact]
        public void ExecutionLatencyMetric_RanksFastestEngineAsRank1()
        {
            var metric = new ExecutionLatencyMetric();

            var ctxFast = CreateContext("fast", "content", TimeSpan.FromMilliseconds(25), fileSizeBytes: 102400);
            var ctxSlow = CreateContext("slow", "content", TimeSpan.FromMilliseconds(100), fileSizeBytes: 102400);
            var competitors = new List<BenchmarkExecutionContext> { ctxFast, ctxSlow };

            var scoreFast = metric.Evaluate(ctxFast, competitors);
            var scoreSlow = metric.Evaluate(ctxSlow, competitors);

            scoreFast.Rank.Should().Be(1);
            scoreFast.NormalizedScore.Should().Be(100.0);
            scoreFast.RawValue.Should().Be(25.0);
            scoreFast.HigherIsBetter.Should().BeFalse();

            scoreSlow.Rank.Should().Be(2);
            scoreSlow.NormalizedScore.Should().Be(25.0); // 25ms / 100ms = 25.0%
        }

        [Fact]
        public void MemoryAllocationMetric_RanksLowestMemoryConsumptionAsRank1()
        {
            var metric = new MemoryAllocationMetric();

            var ctxLow = CreateContext("low-mem", "content", TimeSpan.FromMilliseconds(50), allocatedBytes: 1048576); // 1 MB
            var ctxHigh = CreateContext("high-mem", "content", TimeSpan.FromMilliseconds(50), allocatedBytes: 4194304); // 4 MB
            var competitors = new List<BenchmarkExecutionContext> { ctxLow, ctxHigh };

            var scoreLow = metric.Evaluate(ctxLow, competitors);
            var scoreHigh = metric.Evaluate(ctxHigh, competitors);

            scoreLow.Rank.Should().Be(1);
            scoreLow.NormalizedScore.Should().Be(100.0);
            scoreLow.FormattedValue.Should().Be("1.00 MB");
            scoreLow.HigherIsBetter.Should().BeFalse();

            scoreHigh.Rank.Should().Be(2);
            scoreHigh.NormalizedScore.Should().Be(25.0);
            scoreHigh.FormattedValue.Should().Be("4.00 MB");
        }

        [Fact]
        public void TextCleanlinessMetric_PenalizesReplacementCharsAndFontNoise()
        {
            var metric = new TextCleanlinessMetric();

            var cleanText = "This is perfectly legible and clean document text.";
            var noisyText = "Corrupted \uFFFD text with (cid:120) (cid:121) artifacts and control \0 bytes.";

            var ctxClean = CreateContext("clean", cleanText, TimeSpan.FromMilliseconds(20));
            var ctxNoisy = CreateContext("noisy", noisyText, TimeSpan.FromMilliseconds(20));
            var competitors = new List<BenchmarkExecutionContext> { ctxClean, ctxNoisy };

            var scoreClean = metric.Evaluate(ctxClean, competitors);
            var scoreNoisy = metric.Evaluate(ctxNoisy, competitors);

            scoreClean.Rank.Should().Be(1);
            scoreClean.NormalizedScore.Should().Be(100.0);
            scoreClean.FormattedValue.Should().Be("100.0% clean");

            scoreNoisy.Rank.Should().Be(2);
            scoreNoisy.NormalizedScore.Should().BeLessThan(100.0);
            scoreNoisy.Notes.Should().Contain("noise artifacts detected");
        }

        [Fact]
        public void FailedExecution_ReceivesZeroScoreAcrossAllMetrics()
        {
            var failedCtx = CreateContext("crashed", "", TimeSpan.FromMilliseconds(10), isSuccess: false, ex: new Exception("OOM"));
            var successCtx = CreateContext("ok", "Text", TimeSpan.FromMilliseconds(50), isSuccess: true);
            var competitors = new List<BenchmarkExecutionContext> { failedCtx, successCtx };

            var charMetric = new CharacterCountMetric();
            var contentMetric = new ContentCharacterCountMetric();
            var wordMetric = new WordAndTokenCountMetric();
            var latencyMetric = new ExecutionLatencyMetric();
            var memMetric = new MemoryAllocationMetric();
            var cleanMetric = new TextCleanlinessMetric();

            charMetric.Evaluate(failedCtx, competitors).NormalizedScore.Should().Be(0.0);
            contentMetric.Evaluate(failedCtx, competitors).NormalizedScore.Should().Be(0.0);
            wordMetric.Evaluate(failedCtx, competitors).NormalizedScore.Should().Be(0.0);
            latencyMetric.Evaluate(failedCtx, competitors).NormalizedScore.Should().Be(0.0);
            memMetric.Evaluate(failedCtx, competitors).NormalizedScore.Should().Be(0.0);
            cleanMetric.Evaluate(failedCtx, competitors).NormalizedScore.Should().Be(0.0);
        }

        [Fact]
        public void AllFailedCompetitors_EvaluatesZeroScoresWithoutException()
        {
            var failedCtx1 = CreateContext("crashed1", "", TimeSpan.FromMilliseconds(10), isSuccess: false, ex: new Exception("OOM"));
            var failedCtx2 = CreateContext("crashed2", "", TimeSpan.FromMilliseconds(20), isSuccess: false, ex: new Exception("Timeout"));
            var competitors = new List<BenchmarkExecutionContext> { failedCtx1, failedCtx2 };

            var charMetric = new CharacterCountMetric();
            var latencyMetric = new ExecutionLatencyMetric();
            var cleanMetric = new TextCleanlinessMetric();

            var score1 = charMetric.Evaluate(failedCtx1, competitors);
            var score2 = latencyMetric.Evaluate(failedCtx2, competitors);
            var score3 = cleanMetric.Evaluate(failedCtx1, competitors);

            score1.NormalizedScore.Should().Be(0.0);
            score2.NormalizedScore.Should().Be(0.0);
            score3.NormalizedScore.Should().Be(0.0);
        }

        [Fact]
        public void SingleCompetitor_ReceivesFullScoreAndRank1()
        {
            var ctx = CreateContext("sole-engine", "Standalone extracted text", TimeSpan.FromMilliseconds(45));
            var competitors = new List<BenchmarkExecutionContext> { ctx };

            var charMetric = new CharacterCountMetric();
            var latencyMetric = new ExecutionLatencyMetric();
            var memMetric = new MemoryAllocationMetric();
            var cleanMetric = new TextCleanlinessMetric();

            var charScore = charMetric.Evaluate(ctx, competitors);
            var latencyScore = latencyMetric.Evaluate(ctx, competitors);
            var memScore = memMetric.Evaluate(ctx, competitors);
            var cleanScore = cleanMetric.Evaluate(ctx, competitors);

            charScore.Rank.Should().Be(1);
            charScore.NormalizedScore.Should().Be(100.0);

            latencyScore.Rank.Should().Be(1);
            latencyScore.NormalizedScore.Should().Be(100.0);

            memScore.Rank.Should().Be(1);
            memScore.NormalizedScore.Should().Be(100.0);

            cleanScore.Rank.Should().Be(1);
            cleanScore.NormalizedScore.Should().Be(100.0);
        }

        [Fact]
        public void TiesInCharacterCount_BothGetRank1And100Score()
        {
            var ctx1 = CreateContext("engine1", "Identical length", TimeSpan.FromMilliseconds(40));
            var ctx2 = CreateContext("engine2", "Identical length", TimeSpan.FromMilliseconds(60));
            var competitors = new List<BenchmarkExecutionContext> { ctx1, ctx2 };

            var charMetric = new CharacterCountMetric();

            var score1 = charMetric.Evaluate(ctx1, competitors);
            var score2 = charMetric.Evaluate(ctx2, competitors);

            score1.Rank.Should().Be(1);
            score1.NormalizedScore.Should().Be(100.0);
            score2.Rank.Should().Be(1);
            score2.NormalizedScore.Should().Be(100.0);
        }

        [Fact]
        public void TiesInLatency_BothGetRank1And100Score()
        {
            var ctx1 = CreateContext("engine1", "Some text", TimeSpan.FromMilliseconds(50));
            var ctx2 = CreateContext("engine2", "Some text", TimeSpan.FromMilliseconds(50));
            var competitors = new List<BenchmarkExecutionContext> { ctx1, ctx2 };

            var latencyMetric = new ExecutionLatencyMetric();

            var score1 = latencyMetric.Evaluate(ctx1, competitors);
            var score2 = latencyMetric.Evaluate(ctx2, competitors);

            score1.Rank.Should().Be(1);
            score1.NormalizedScore.Should().Be(100.0);
            score2.Rank.Should().Be(1);
            score2.NormalizedScore.Should().Be(100.0);
        }

        [Fact]
        public void MemoryAllocationMetric_ZeroOrNegativeAllocatedBytes_HandlesGracefully()
        {
            var ctx1 = CreateContext("engine1", "Text", TimeSpan.FromMilliseconds(30), allocatedBytes: 0);
            var ctx2 = CreateContext("engine2", "Text", TimeSpan.FromMilliseconds(30), allocatedBytes: 1024);
            var competitors = new List<BenchmarkExecutionContext> { ctx1, ctx2 };

            var memMetric = new MemoryAllocationMetric();

            var score1 = memMetric.Evaluate(ctx1, competitors);
            var score2 = memMetric.Evaluate(ctx2, competitors);

            score1.NormalizedScore.Should().BeGreaterThanOrEqualTo(0.0);
            score2.NormalizedScore.Should().BeGreaterThanOrEqualTo(0.0);
        }

        [Fact]
        public void TextCleanlinessMetric_CompletelyCorruptedText_ScoresZeroOrNearZero()
        {
            var corrupted = "\uFFFD\uFFFD\uFFFD\0\0\0(cid:1)(cid:2)";
            var ctx = CreateContext("corrupted", corrupted, TimeSpan.FromMilliseconds(30));
            var competitors = new List<BenchmarkExecutionContext> { ctx };

            var cleanMetric = new TextCleanlinessMetric();
            var score = cleanMetric.Evaluate(ctx, competitors);

            score.NormalizedScore.Should().BeLessThan(50.0);
        }
    }
}

