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
using Microsoft.UI.Xaml.Controls;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class BenchmarkViewModelTests : IDisposable
    {
        private readonly DocumentCategoryRegistry _categoryRegistry = new();
        private readonly List<string> _tempFiles = new();

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

        private string CreateTempFile(string ext = ".docx")
        {
            var p = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{ext}");
            File.WriteAllText(p, "Sample text content");
            _tempFiles.Add(p);
            return p;
        }

        private class MockBenchmarkRunner : IBenchmarkRunnerService
        {
            public Func<string, BenchmarkOptions?, IProgress<BenchmarkProgressReport>?, CancellationToken, Task<BenchmarkSessionResult>>? RunFunc { get; set; }
            public Func<IEnumerable<string>, BenchmarkOptions?, IProgress<BenchmarkProgressReport>?, CancellationToken, Task<BenchmarkSessionResult>>? RunBatchFunc { get; set; }
            public List<BenchmarkSessionEntity> History { get; set; } = new();

            public Task<BenchmarkSessionResult> RunBenchmarkAsync(string filePath, BenchmarkOptions? options = null, IProgress<BenchmarkProgressReport>? progress = null, CancellationToken cancellationToken = default)
            {
                if (RunFunc != null) return RunFunc(filePath, options, progress, cancellationToken);
                return Task.FromResult(new BenchmarkSessionResult(Guid.NewGuid(), "Session", DocumentCategory.Word, DateTime.UtcNow, new List<BenchmarkDocumentResult>(), null, null));
            }

            public Task<BenchmarkSessionResult> RunBatchBenchmarkAsync(IEnumerable<string> filePaths, BenchmarkOptions? options = null, IProgress<BenchmarkProgressReport>? progress = null, CancellationToken cancellationToken = default)
            {
                if (RunBatchFunc != null) return RunBatchFunc(filePaths, options, progress, cancellationToken);
                return Task.FromResult(new BenchmarkSessionResult(Guid.NewGuid(), "Session", DocumentCategory.Word, DateTime.UtcNow, new List<BenchmarkDocumentResult>(), "aspose", "Aspose Words"));
            }

            public Task<IReadOnlyList<BenchmarkSessionEntity>> GetBenchmarkHistoryAsync(DocumentCategory? category = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyList<BenchmarkSessionEntity>>(History.AsReadOnly());
            }

            public Task<BenchmarkSessionEntity?> GetBenchmarkSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(History.FirstOrDefault(s => s.Id == sessionId));
            }

            public Task<bool> DeleteBenchmarkSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            {
                int removed = History.RemoveAll(s => s.Id == sessionId);
                return Task.FromResult(removed > 0);
            }
        }

        [Fact]
        public void Initialization_PopulatesCategoriesAndDefaultFormats()
        {
            var runner = new MockBenchmarkRunner();
            var vm = new BenchmarkViewModel(runner, _categoryRegistry);

            vm.Categories.Should().HaveCount(5);
            vm.SelectedCategory.Should().NotBeNull();
            vm.SelectedCategory!.Category.Should().Be(DocumentCategory.Word);
            vm.AvailableFormats.Should().NotBeEmpty();
            vm.AvailableFormats.Select(f => f.Extension).Should().Contain(new[] { ".docx", ".doc", ".rtf" });
        }

        [Fact]
        public void ChangingSelectedCategory_UpdatesAvailableFormats()
        {
            var runner = new MockBenchmarkRunner();
            var vm = new BenchmarkViewModel(runner, _categoryRegistry);

            var excelCategory = vm.Categories.First(c => c.Category == DocumentCategory.Excel);
            vm.SelectedCategory = excelCategory;

            vm.AvailableFormats.Should().NotBeEmpty();
            vm.AvailableFormats.Select(f => f.Extension).Should().Contain(new[] { ".xlsx", ".xls", ".csv" });
        }

        [Fact]
        public void AddFiles_And_RemoveFile_UpdatesCollectionAndCanRunState()
        {
            var runner = new MockBenchmarkRunner();
            var vm = new BenchmarkViewModel(runner, _categoryRegistry);

            var f1 = CreateTempFile(".docx");
            var f2 = CreateTempFile(".xlsx");

            vm.CanRunBenchmark.Should().BeFalse();

            vm.AddFiles(new[] { f1, f2 });

            vm.SelectedFiles.Should().HaveCount(2);
            vm.HasSelectedFiles.Should().BeTrue();
            vm.CanRunBenchmark.Should().BeTrue();

            var firstFile = vm.SelectedFiles[0];
            vm.RemoveFile(firstFile);

            vm.SelectedFiles.Should().HaveCount(1);
            vm.CanRunBenchmark.Should().BeTrue();

            vm.ClearFiles();
            vm.SelectedFiles.Should().BeEmpty();
            vm.CanRunBenchmark.Should().BeFalse();
        }

        [Fact]
        public async Task RunBenchmarkCommand_ExecutesAndSetsLatestResult()
        {
            var runner = new MockBenchmarkRunner();
            var expectedResult = new BenchmarkSessionResult(
                SessionId: Guid.NewGuid(),
                Title: "Test Benchmark",
                Category: DocumentCategory.Word,
                CreatedAt: DateTime.UtcNow,
                DocumentResults: new List<BenchmarkDocumentResult>(),
                OverallWinnerEngineId: "aspose",
                OverallWinnerDisplayName: "Aspose Words"
            );

            runner.RunBatchFunc = (files, opts, prog, ct) => Task.FromResult(expectedResult);

            var vm = new BenchmarkViewModel(runner, _categoryRegistry);
            var f1 = CreateTempFile(".docx");
            vm.AddFiles(new[] { f1 });

            await vm.RunBenchmarkCommand.ExecuteAsync(null);

            vm.LatestResult.Should().NotBeNull();
            vm.LatestResult!.OverallWinnerEngineId.Should().Be("aspose");
            vm.HasLatestResult.Should().BeTrue();
            vm.IsRunning.Should().BeFalse();
            vm.StatusSeverity.Should().Be(InfoBarSeverity.Success);
            vm.StatusMessage.Should().Contain("Aspose Words");
        }

        [Fact]
        public async Task DeleteSessionCommand_RemovesSessionFromRecentList()
        {
            var sessionId = Guid.NewGuid();
            var session = new BenchmarkSessionEntity
            {
                Id = sessionId,
                Title = "Session to Delete"
            };

            var runner = new MockBenchmarkRunner();
            runner.History.Add(session);

            var vm = new BenchmarkViewModel(runner, _categoryRegistry);
            await vm.LoadHistoryCommand.ExecuteAsync(null);

            vm.RecentSessions.Should().Contain(session);

            await vm.DeleteSessionCommand.ExecuteAsync(session);

            vm.RecentSessions.Should().NotContain(session);
            runner.History.Should().NotContain(session);
        }
    }
}

