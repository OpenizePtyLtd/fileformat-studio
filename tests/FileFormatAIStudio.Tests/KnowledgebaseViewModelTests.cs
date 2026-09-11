using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class KnowledgebaseViewModelTests
    {
        private readonly FakeKnowledgebaseService _fakeService;
        private readonly KnowledgebaseViewModel _viewModel;

        public KnowledgebaseViewModelTests()
        {
            _fakeService = new FakeKnowledgebaseService();
            _viewModel = new KnowledgebaseViewModel(_fakeService);
        }

        [Fact]
        public async Task LoadKnowledgebasesAsync_PopulatesCollectionAndUpdatesFlags()
        {
            _fakeService.Knowledgebases.Add(new KnowledgebaseEntity
            {
                Id = Guid.NewGuid(),
                Name = "HR Policies",
                Description = "Employee handbook and HR guidelines",
                ParserEngine = "Auto",
                VectorDimensions = 1536,
                Documents =
                [
                    new KnowledgebaseDocumentEntity
                    {
                        Id = Guid.NewGuid(),
                        FileName = "handbook.pdf",
                        FileSize = 1024 * 500,
                        ChunkCount = 42,
                        Status = "Indexed"
                    }
                ]
            });

            await _viewModel.LoadKnowledgebasesAsync();

            _viewModel.Knowledgebases.Should().HaveCount(1);
            _viewModel.HasKnowledgebases.Should().BeTrue();
            _viewModel.HasNoKnowledgebases.Should().BeFalse();

            var item = _viewModel.Knowledgebases[0];
            item.Name.Should().Be("HR Policies");
            item.DocumentCount.Should().Be(1);
            item.ChunkCount.Should().Be(42);
        }

        [Fact]
        public async Task LoadKnowledgebasesAsync_EmptyList_SetsHasNoKnowledgebasesTrue()
        {
            await _viewModel.LoadKnowledgebasesAsync();

            _viewModel.Knowledgebases.Should().BeEmpty();
            _viewModel.HasKnowledgebases.Should().BeFalse();
            _viewModel.HasNoKnowledgebases.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteKnowledgebaseAsync_RemovesItemFromCollection()
        {
            var kb = new KnowledgebaseEntity
            {
                Id = Guid.NewGuid(),
                Name = "To Delete"
            };
            _fakeService.Knowledgebases.Add(kb);
            await _viewModel.LoadKnowledgebasesAsync();

            var item = _viewModel.Knowledgebases[0];
            _viewModel.SelectedKnowledgebase = item;

            await _viewModel.DeleteKnowledgebaseAsync(item);

            _viewModel.Knowledgebases.Should().BeEmpty();
            _viewModel.SelectedKnowledgebase.Should().BeNull();
            _viewModel.HasNoKnowledgebases.Should().BeTrue();
            _viewModel.IsStatusOpen.Should().BeTrue();
            _viewModel.StatusSeverity.Should().Be(InfoBarSeverity.Success);
        }

        [Fact]
        public async Task DeleteDocumentAsync_RemovesDocumentAndRefreshesCounts()
        {
            var doc1 = new KnowledgebaseDocumentEntity
            {
                Id = Guid.NewGuid(),
                FileName = "doc1.txt",
                ChunkCount = 10,
                FileSize = 1024
            };
            var doc2 = new KnowledgebaseDocumentEntity
            {
                Id = Guid.NewGuid(),
                FileName = "doc2.txt",
                ChunkCount = 20,
                FileSize = 2048
            };

            var kb = new KnowledgebaseEntity
            {
                Id = Guid.NewGuid(),
                Name = "Multi Doc KB",
                Documents = [doc1, doc2]
            };
            _fakeService.Knowledgebases.Add(kb);
            await _viewModel.LoadKnowledgebasesAsync();

            var item = _viewModel.Knowledgebases[0];
            item.DocumentCount.Should().Be(2);
            item.ChunkCount.Should().Be(30);

            await _viewModel.DeleteDocumentAsync(item, doc1);

            item.Documents.Should().HaveCount(1);
            item.Documents.Should().NotContain(doc1);
            item.DocumentCount.Should().Be(1);
            item.ChunkCount.Should().Be(20);
        }

        [Fact]
        public void ToggleExpanded_SwitchesStateAndIcon()
        {
            var kb = new KnowledgebaseEntity { Name = "Expand Test" };
            var item = new KnowledgebaseItemViewModel(kb);

            item.IsExpanded.Should().BeFalse();
            item.ExpandIconGlyph.Should().Be("\uE70D");
            item.ExpandedVisibility.Should().Be(Visibility.Collapsed);

            item.ToggleExpandedCommand.Execute(null);

            item.IsExpanded.Should().BeTrue();
            item.ExpandIconGlyph.Should().Be("\uE70E");
            item.ExpandedVisibility.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void RequestCreateKnowledgebase_RaisesEvent()
        {
            bool eventRaised = false;
            _viewModel.CreateKnowledgebaseRequested += () => eventRaised = true;

            _viewModel.RequestCreateKnowledgebaseCommand.Execute(null);

            eventRaised.Should().BeTrue();
        }

        [Theory]
        [InlineData(0, "0 B")]
        [InlineData(500, "500.0 B")]
        [InlineData(1024, "1.0 KB")]
        [InlineData(1024 * 1024 * 5, "5.0 MB")]
        public void FormatBytes_FormatsSizesCorrectly(long bytes, string expected)
        {
            KnowledgebaseItemViewModel.FormatBytes(bytes).Should().Be(expected);
        }

        [Fact]
        public async Task AddDocumentsAsync_AppendsNewDocumentsAndRefreshesCounts()
        {
            var kb = new KnowledgebaseEntity
            {
                Id = Guid.NewGuid(),
                Name = "Engineering Docs",
                Documents = []
            };
            _fakeService.Knowledgebases.Add(kb);
            await _viewModel.LoadKnowledgebasesAsync();

            var item = _viewModel.Knowledgebases[0];
            item.DocumentCount.Should().Be(0);
            item.ChunkCount.Should().Be(0);

            var files = new[] { @"C:\test\spec1.pdf", @"C:\test\spec2.docx" };
            await _viewModel.AddDocumentsAsync(item, files);

            item.Documents.Should().HaveCount(2);
            item.DocumentCount.Should().Be(2);
            item.ChunkCount.Should().Be(10); // 2 docs * 5 chunks
            _viewModel.IsStatusOpen.Should().BeTrue();
            _viewModel.StatusSeverity.Should().Be(InfoBarSeverity.Success);
            _viewModel.StatusMessage.Should().Contain("Successfully indexed 2 document(s)");
        }

        [Fact]
        public async Task AddDocumentsAsync_EmptyFileList_DoesNothing()
        {
            var kb = new KnowledgebaseEntity { Id = Guid.NewGuid(), Name = "Empty Test" };
            _fakeService.Knowledgebases.Add(kb);
            await _viewModel.LoadKnowledgebasesAsync();

            var item = _viewModel.Knowledgebases[0];
            await _viewModel.AddDocumentsAsync(item, Array.Empty<string>());

            item.Documents.Should().BeEmpty();
            _viewModel.IsStatusOpen.Should().BeFalse();
        }

        [Fact]
        public async Task AddDocumentsAsync_WhenIngestionThrows_ShowsErrorStatus()
        {
            var kb = new KnowledgebaseEntity { Id = Guid.NewGuid(), Name = "Failing KB" };
            _fakeService.Knowledgebases.Add(kb);
            _fakeService.IngestFunc = (_, _) => throw new InvalidOperationException("Embedding provider offline");
            await _viewModel.LoadKnowledgebasesAsync();

            var item = _viewModel.Knowledgebases[0];
            await _viewModel.AddDocumentsAsync(item, new[] { @"C:\test\doc.pdf" });

            item.Documents.Should().BeEmpty();
            _viewModel.IsStatusOpen.Should().BeTrue();
            _viewModel.StatusSeverity.Should().Be(InfoBarSeverity.Error);
            _viewModel.StatusMessage.Should().Contain("Embedding provider offline");
        }

        [Fact]
        public async Task AddDocumentsAsync_PartialFailure_ShowsWarningStatus()
        {
            var kb = new KnowledgebaseEntity { Id = Guid.NewGuid(), Name = "Partial KB" };
            _fakeService.Knowledgebases.Add(kb);
            _fakeService.IngestFunc = (id, _) => Task.FromResult(new List<KnowledgebaseDocumentEntity>
            {
                new() { Id = Guid.NewGuid(), KnowledgebaseId = id, FileName = "good.pdf", Status = "Indexed", ChunkCount = 3 },
                new() { Id = Guid.NewGuid(), KnowledgebaseId = id, FileName = "bad.pdf", Status = "Failed", ErrorMessage = "Corrupt file" }
            });
            await _viewModel.LoadKnowledgebasesAsync();

            var item = _viewModel.Knowledgebases[0];
            await _viewModel.AddDocumentsAsync(item, new[] { "good.pdf", "bad.pdf" });

            item.Documents.Should().HaveCount(2);
            _viewModel.IsStatusOpen.Should().BeTrue();
            _viewModel.StatusSeverity.Should().Be(InfoBarSeverity.Warning);
            _viewModel.StatusMessage.Should().Contain("1 document(s) failed");
        }

        [Fact]
        public async Task AddDocumentsAsync_ReportsProgressToItemViewModel()
        {
            var kb = new KnowledgebaseEntity { Id = Guid.NewGuid(), Name = "Progress KB" };
            _fakeService.Knowledgebases.Add(kb);
            _fakeService.ProgressAction = progress =>
            {
                progress?.Report(new IndexingProgressReport
                {
                    Stage = IndexingStage.GeneratingEmbeddings,
                    Percentage = 65.0,
                    CurrentDocumentName = "doc.pdf",
                    CurrentDocumentIndex = 1,
                    TotalDocuments = 1,
                    ProcessedDocuments = 0,
                    TotalChunksIndexed = 12,
                    Message = "Generating embeddings..."
                });
            };

            await _viewModel.LoadKnowledgebasesAsync();
            var item = _viewModel.Knowledgebases[0];

            await _viewModel.AddDocumentsAsync(item, new[] { "doc.pdf" });

            item.CurrentStageStep.Should().Be(3); // GeneratingEmbeddings = step 3
            item.IndexingPercentage.Should().Be(65.0);
            item.TotalChunksIndexed.Should().Be(12);
            item.CurrentDocumentName.Should().Be("doc.pdf");
            item.IsStep3Active.Should().BeTrue();
            item.IsStep1Completed.Should().BeTrue();
            item.IsStep2Completed.Should().BeTrue();
            item.IsIndexing.Should().BeFalse(); // Reset after completion
        }

        [Fact]
        public async Task AddDocumentsAsync_WhenCancelled_ShowsWarningAndCleansUp()
        {
            var kb = new KnowledgebaseEntity { Id = Guid.NewGuid(), Name = "Cancel KB" };
            _fakeService.Knowledgebases.Add(kb);
            _fakeService.IngestFuncWithToken = (id, files, opt, prog, ct) =>
            {
                throw new OperationCanceledException(ct);
            };

            await _viewModel.LoadKnowledgebasesAsync();
            var item = _viewModel.Knowledgebases[0];

            await _viewModel.AddDocumentsAsync(item, new[] { "cancel.pdf" });

            item.IsIndexing.Should().BeFalse();
            item.CanAddDocuments.Should().BeTrue();
            item.IndexingCts.Should().BeNull();
            _viewModel.IsStatusOpen.Should().BeTrue();
            _viewModel.StatusSeverity.Should().Be(InfoBarSeverity.Warning);
            _viewModel.StatusMessage.Should().Contain("was cancelled by user");
        }
    }

    internal sealed class FakeKnowledgebaseService : IKnowledgebaseService
    {
        public List<KnowledgebaseEntity> Knowledgebases { get; } = [];
        public Func<Guid, IEnumerable<string>, Task<List<KnowledgebaseDocumentEntity>>>? IngestFunc { get; set; }
        public Action<IProgress<IndexingProgressReport>?>? ProgressAction { get; set; }
        public Func<Guid, IEnumerable<string>, IngestionOptions?, IProgress<IndexingProgressReport>?, CancellationToken, Task<List<KnowledgebaseDocumentEntity>>>? IngestFuncWithToken { get; set; }

        public Task<List<KnowledgebaseEntity>> GetKnowledgebasesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Knowledgebases.ToList());
        }

        public Task<KnowledgebaseEntity?> GetKnowledgebaseByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Knowledgebases.FirstOrDefault(k => k.Id == id));
        }

        public Task<KnowledgebaseEntity> CreateKnowledgebaseAsync(CreateKnowledgebaseRequest request, CancellationToken ct = default)
        {
            var entity = new KnowledgebaseEntity
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                ParserEngine = request.ParserEngine,
                EmbeddingProvider = request.EmbeddingProvider,
                EmbeddingModel = request.EmbeddingModel,
                VectorDimensions = request.VectorDimensions
            };
            Knowledgebases.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<KnowledgebaseEntity> UpdateKnowledgebaseAsync(Guid id, UpdateKnowledgebaseRequest request, CancellationToken ct = default)
        {
            var entity = Knowledgebases.First(k => k.Id == id);
            entity.Name = request.Name;
            entity.Description = request.Description;
            if (!string.IsNullOrWhiteSpace(request.ParserEngine))
                entity.ParserEngine = request.ParserEngine;
            return Task.FromResult(entity);
        }

        public Task DeleteKnowledgebaseAsync(Guid id, CancellationToken ct = default)
        {
            Knowledgebases.RemoveAll(k => k.Id == id);
            return Task.CompletedTask;
        }

        public Task<List<KnowledgebaseDocumentEntity>> GetDocumentsAsync(Guid knowledgebaseId, CancellationToken ct = default)
        {
            var kb = Knowledgebases.FirstOrDefault(k => k.Id == knowledgebaseId);
            return Task.FromResult(kb?.Documents ?? []);
        }

        public Task<KnowledgebaseDocumentEntity?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
        {
            var doc = Knowledgebases.SelectMany(k => k.Documents).FirstOrDefault(d => d.Id == documentId);
            return Task.FromResult(doc);
        }

        public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
        {
            foreach (var kb in Knowledgebases)
            {
                kb.Documents.RemoveAll(d => d.Id == documentId);
            }
            return Task.CompletedTask;
        }

        public Task<List<KnowledgebaseDocumentEntity>> IngestDocumentsAsync(
            Guid knowledgebaseId,
            IEnumerable<string> filePaths,
            IngestionOptions? options = null,
            IProgress<IndexingProgressReport>? progress = null,
            CancellationToken ct = default)
        {
            ProgressAction?.Invoke(progress);
            if (IngestFuncWithToken != null) return IngestFuncWithToken(knowledgebaseId, filePaths, options, progress, ct);
            if (IngestFunc != null) return IngestFunc(knowledgebaseId, filePaths);

            var results = filePaths.Select(f => new KnowledgebaseDocumentEntity
            {
                Id = Guid.NewGuid(),
                KnowledgebaseId = knowledgebaseId,
                FileName = System.IO.Path.GetFileName(f),
                FileSize = 1024,
                ChunkCount = 5,
                Status = "Indexed"
            }).ToList();

            var kb = Knowledgebases.FirstOrDefault(k => k.Id == knowledgebaseId);
            kb?.Documents.AddRange(results);
            return Task.FromResult(results);
        }

        public Task<string> GetDocumentExtractedTextAsync(Guid documentId, CancellationToken ct = default)
        {
            var doc = Knowledgebases.SelectMany(k => k.Documents).FirstOrDefault(d => d.Id == documentId);
            if (doc != null && !string.IsNullOrEmpty(doc.RawExtractedText))
            {
                return Task.FromResult(doc.RawExtractedText);
            }
            return Task.FromResult(string.Empty);
        }

        public Task<List<DocumentChunkEntity>> GetDocumentChunksAsync(Guid documentId, CancellationToken ct = default)
        {
            var doc = Knowledgebases.SelectMany(k => k.Documents).FirstOrDefault(d => d.Id == documentId);
            return Task.FromResult(doc?.Chunks ?? []);
        }
    }
}

