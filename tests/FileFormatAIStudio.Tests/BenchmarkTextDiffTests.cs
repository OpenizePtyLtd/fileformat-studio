using System;
using System.Collections.Generic;
using System.Linq;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class BenchmarkTextDiffTests
    {
        [Fact]
        public void Compare_IdenticalTexts_ReturnsAllUnchangedAndZeroUnique()
        {
            var text = "Hello World\r\nSecond Line\r\nThird Line";

            var (items, summary) = TextDiffEngine.Compare(text, text);

            items.Should().HaveCount(3);
            items.Should().OnlyContain(i => i.Kind == TextDiffKind.Unchanged);
            summary.CharactersA.Should().Be(text.Length);
            summary.CharactersB.Should().Be(text.Length);
            summary.CharacterDelta.Should().Be(0);
            summary.LinesA.Should().Be(3);
            summary.LinesB.Should().Be(3);
            summary.UniqueLinesA.Should().Be(0);
            summary.UniqueLinesB.Should().Be(0);
            summary.CharacterAdvantagePercentage.Should().Be(0.0);
        }

        [Fact]
        public void Compare_DifferentTexts_ComputesDifferencesCorrectly()
        {
            var textA = "Header\r\nMiddle Section A\r\nFooter";
            var textB = "Header\r\nMiddle Section B\r\nFooter";

            var (items, summary) = TextDiffEngine.Compare(textA, textB);

            items.Should().Contain(i => i.Kind == TextDiffKind.Unchanged && i.Content == "Header");
            items.Should().Contain(i => i.Kind == TextDiffKind.UniqueToA && i.Content == "Middle Section A");
            items.Should().Contain(i => i.Kind == TextDiffKind.UniqueToB && i.Content == "Middle Section B");
            items.Should().Contain(i => i.Kind == TextDiffKind.Unchanged && i.Content == "Footer");

            summary.LinesA.Should().Be(3);
            summary.LinesB.Should().Be(3);
            summary.UniqueLinesA.Should().Be(1);
            summary.UniqueLinesB.Should().Be(1);
        }

        [Fact]
        public void Compare_EmptyTexts_HandlesGracefully()
        {
            var (items, summary) = TextDiffEngine.Compare("", "");

            items.Should().BeEmpty();
            summary.CharactersA.Should().Be(0);
            summary.CharactersB.Should().Be(0);
            summary.LinesA.Should().Be(0);
            summary.LinesB.Should().Be(0);
            summary.CharacterAdvantagePercentage.Should().Be(0.0);
        }

        [Fact]
        public void Compare_OneEmptyOneNonEmpty_ReturnsAllDifferences()
        {
            var textA = "Line 1\r\nLine 2";
            var textB = "";

            var (items, summary) = TextDiffEngine.Compare(textA, textB);

            items.Should().HaveCount(2);
            items.Should().OnlyContain(i => i.Kind == TextDiffKind.UniqueToA);
            summary.LinesA.Should().Be(2);
            summary.LinesB.Should().Be(0);
            summary.UniqueLinesA.Should().Be(2);
            summary.CharacterAdvantagePercentage.Should().Be(100.0);
        }

        [Fact]
        public void Compare_LargeNumberOfLines_UsesFastDiffFallback()
        {
            var linesA = Enumerable.Range(1, 1500).Select(i => $"Line {i}").ToList();
            var linesB = Enumerable.Range(1, 1500).Select(i => i % 2 == 0 ? $"Line {i} modified" : $"Line {i}").ToList();

            var textA = string.Join("\n", linesA);
            var textB = string.Join("\n", linesB);

            var (items, summary) = TextDiffEngine.Compare(textA, textB);

            items.Should().NotBeEmpty();
            summary.LinesA.Should().Be(1500);
            summary.LinesB.Should().Be(1500);
        }

        [Fact]
        public void ViewModel_InitializationAndEngineSelection_ComputesDiffAutomatically()
        {
            var vm = new BenchmarkTextDiffViewModel();

            var run1 = new BenchmarkEngineRunResult(
                "engine1", "Engine One", true, TimeSpan.FromMilliseconds(50), 1024, 100, 15, 95.0, 1,
                Array.Empty<MetricScoreResult>(), "First line\nSecond line\nThird line");
            var run2 = new BenchmarkEngineRunResult(
                "engine2", "Engine Two", true, TimeSpan.FromMilliseconds(60), 2048, 90, 14, 85.0, 2,
                Array.Empty<MetricScoreResult>(), "First line\nSecond line modified\nThird line");

            var docResult = new BenchmarkDocumentResult(
                "C:\\path\\doc.docx", "doc.docx", ".docx", DocumentCategory.Word, 10240,
                new[] { run1, run2 }, run1);

            vm.Initialize(docResult);

            vm.AvailableEngines.Should().HaveCount(2);
            vm.SelectedEngineA.Should().Be(run1);
            vm.SelectedEngineB.Should().Be(run2);
            vm.DocumentName.Should().Be("doc.docx");
            vm.HasDiffItems.Should().BeTrue();
            vm.DiffItems.Should().NotBeEmpty();
            vm.Summary.Should().NotBeNull();
            vm.DiffSummaryText.Should().Contain("Engine One extracted +10 more characters");
        }

        [Fact]
        public void ViewModel_SearchQuery_FiltersBySubstring()
        {
            var vm = new BenchmarkTextDiffViewModel();

            var run1 = new BenchmarkEngineRunResult(
                "engine1", "Engine One", true, TimeSpan.FromMilliseconds(50), 1024, 100, 15, 95.0, 1,
                Array.Empty<MetricScoreResult>(), "Apple\nBanana\nCherry");
            var run2 = new BenchmarkEngineRunResult(
                "engine2", "Engine Two", true, TimeSpan.FromMilliseconds(60), 2048, 90, 14, 85.0, 2,
                Array.Empty<MetricScoreResult>(), "Apple\nBanana\nCherry");

            var docResult = new BenchmarkDocumentResult(
                "C:\\path\\doc.docx", "doc.docx", ".docx", DocumentCategory.Word, 10240,
                new[] { run1, run2 }, run1);

            vm.Initialize(docResult);

            vm.SearchQuery = "Banana";
            vm.DiffItems.Should().HaveCount(1);
            vm.DiffItems[0].Content.Should().Be("Banana");
        }

        [Fact]
        public void ViewModel_GenerateUnifiedDiffText_FormatsValidOutput()
        {
            var vm = new BenchmarkTextDiffViewModel();

            var run1 = new BenchmarkEngineRunResult(
                "engine1", "Engine A", true, TimeSpan.FromMilliseconds(50), 1024, 10, 2, 95.0, 1,
                Array.Empty<MetricScoreResult>(), "Alpha\nBeta");
            var run2 = new BenchmarkEngineRunResult(
                "engine2", "Engine B", true, TimeSpan.FromMilliseconds(60), 2048, 11, 2, 85.0, 2,
                Array.Empty<MetricScoreResult>(), "Alpha\nGamma");

            var docResult = new BenchmarkDocumentResult(
                "C:\\path\\doc.docx", "doc.docx", ".docx", DocumentCategory.Word, 10240,
                new[] { run1, run2 }, run1);

            vm.Initialize(docResult);

            var unified = vm.GenerateUnifiedDiffText();

            unified.Should().Contain("--- Engine A");
            unified.Should().Contain("+++ Engine B");
            unified.Should().Contain("@@ doc.docx @@");
            unified.Should().Contain("Alpha");
            unified.Should().Contain("+ [A] Beta");
            unified.Should().Contain("- [B] Gamma");
        }
    }
}

