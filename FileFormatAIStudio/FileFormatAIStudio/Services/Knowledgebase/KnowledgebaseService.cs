using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Parsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace FileFormatAIStudio.Services.Knowledgebase
{
    /// <summary>
    /// Implements end-to-end knowledgebase lifecycle management, document storage,
    /// and multi-stage vector indexing pipeline.
    /// </summary>
    public class KnowledgebaseService : IKnowledgebaseService
    {
        private readonly AppDbContext _context;
        private readonly IDocumentParserFactory _parserFactory;
        private readonly ITextChunker _textChunker;
        private readonly IVectorStoreService _vectorStore;
        private readonly IAIClientFactory _aiClientFactory;
        private readonly string _storageDirectory;

        public KnowledgebaseService(
            AppDbContext context,
            IDocumentParserFactory parserFactory,
            ITextChunker textChunker,
            IVectorStoreService vectorStore,
            IAIClientFactory aiClientFactory,
            string? customStorageDirectory = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _parserFactory = parserFactory ?? throw new ArgumentNullException(nameof(parserFactory));
            _textChunker = textChunker ?? throw new ArgumentNullException(nameof(textChunker));
            _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
            _aiClientFactory = aiClientFactory ?? throw new ArgumentNullException(nameof(aiClientFactory));

            if (!string.IsNullOrWhiteSpace(customStorageDirectory))
            {
                _storageDirectory = customStorageDirectory;
            }
            else
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                _storageDirectory = Path.Combine(localAppData, "FileFormatAIStudio", "Knowledgebases");
            }

            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }
        }

        public async Task<List<KnowledgebaseEntity>> GetKnowledgebasesAsync(CancellationToken ct = default)
        {
            return await _context.Knowledgebases
                .Include(k => k.Documents)
                .OrderByDescending(k => k.UpdatedAt)
                .ToListAsync(ct);
        }

        public async Task<KnowledgebaseEntity?> GetKnowledgebaseByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Knowledgebases
                .Include(k => k.Documents)
                .FirstOrDefaultAsync(k => k.Id == id, ct);
        }

        public async Task<KnowledgebaseEntity> CreateKnowledgebaseAsync(CreateKnowledgebaseRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Knowledgebase name cannot be empty.", nameof(request));
            }

            int dimensions = request.VectorDimensions > 0
                ? request.VectorDimensions
                : 1536;

            var entity = new KnowledgebaseEntity
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Description = request.Description.Trim(),
                ParserEngine = string.IsNullOrWhiteSpace(request.ParserEngine) ? "Auto" : request.ParserEngine.Trim(),
                EmbeddingProvider = request.EmbeddingProvider.Trim(),
                EmbeddingModel = request.EmbeddingModel.Trim(),
                VectorDimensions = dimensions,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Knowledgebases.Add(entity);
            await _context.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<KnowledgebaseEntity> UpdateKnowledgebaseAsync(Guid id, UpdateKnowledgebaseRequest request, CancellationToken ct = default)
        {
            var entity = await _context.Knowledgebases.FirstOrDefaultAsync(k => k.Id == id, ct)
                ?? throw new KeyNotFoundException($"Knowledgebase with ID '{id}' was not found.");

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Knowledgebase name cannot be empty.", nameof(request));
            }

            entity.Name = request.Name.Trim();
            entity.Description = request.Description.Trim();
            if (!string.IsNullOrWhiteSpace(request.ParserEngine))
            {
                entity.ParserEngine = request.ParserEngine.Trim();
            }
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            return entity;
        }

        public async Task DeleteKnowledgebaseAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await _context.Knowledgebases
                .Include(k => k.Documents)
                .FirstOrDefaultAsync(k => k.Id == id, ct);

            if (entity == null)
            {
                return;
            }

            // 1. Delete all chunks from vector store
            await _vectorStore.DeleteByKnowledgebaseIdAsync(id, ct);
            foreach (var entry in _context.ChangeTracker.Entries<DocumentChunkEntity>().Where(e => e.Entity.KnowledgebaseId == id).ToList())
            {
                entry.State = EntityState.Detached;
            }

            // 2. Remove physical storage folder
            string kbFolder = Path.Combine(_storageDirectory, id.ToString());
            if (Directory.Exists(kbFolder))
            {
                try
                {
                    Directory.Delete(kbFolder, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup for physical files
                }
            }

            // 3. Remove entity (EF Core cascades documents and join records)
            _context.Knowledgebases.Remove(entity);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<KnowledgebaseDocumentEntity>> GetDocumentsAsync(Guid knowledgebaseId, CancellationToken ct = default)
        {
            return await _context.KnowledgebaseDocuments
                .Where(d => d.KnowledgebaseId == knowledgebaseId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<KnowledgebaseDocumentEntity?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
        {
            return await _context.KnowledgebaseDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId, ct);
        }

        public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
        {
            var document = await _context.KnowledgebaseDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct);
            if (document == null)
            {
                return;
            }

            // 1. Delete associated chunks from vector store
            await _vectorStore.DeleteByDocumentIdAsync(documentId, ct);
            foreach (var entry in _context.ChangeTracker.Entries<DocumentChunkEntity>().Where(e => e.Entity.DocumentId == documentId).ToList())
            {
                entry.State = EntityState.Detached;
            }

            // 2. Delete stored file from disk
            if (!string.IsNullOrWhiteSpace(document.FilePath) && File.Exists(document.FilePath))
            {
                try
                {
                    File.Delete(document.FilePath);
                }
                catch (IOException)
                {
                    // Best-effort cleanup
                }
            }

            // 3. Remove document entity
            _context.KnowledgebaseDocuments.Remove(document);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<KnowledgebaseDocumentEntity>> IngestDocumentsAsync(
            Guid knowledgebaseId,
            IEnumerable<string> filePaths,
            IngestionOptions? options = null,
            IProgress<IndexingProgressReport>? progress = null,
            CancellationToken ct = default)
        {
            var kb = await _context.Knowledgebases.FirstOrDefaultAsync(k => k.Id == knowledgebaseId, ct)
                ?? throw new KeyNotFoundException($"Knowledgebase with ID '{knowledgebaseId}' was not found.");

            var fileList = filePaths?.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().ToList()
                ?? throw new ArgumentException("File paths collection cannot be null or empty.", nameof(filePaths));

            if (fileList.Count == 0)
            {
                return [];
            }

            string kbFolder = Path.Combine(_storageDirectory, knowledgebaseId.ToString());
            if (!Directory.Exists(kbFolder))
            {
                Directory.CreateDirectory(kbFolder);
            }

            progress?.Report(new IndexingProgressReport
            {
                Stage = IndexingStage.Starting,
                TotalDocuments = fileList.Count,
                ProcessedDocuments = 0,
                Percentage = 0.0,
                Message = $"Initializing ingestion for {fileList.Count} document(s)..."
            });

            // 1. Validate files and copy to storage directory, creating pending document entities
            var documents = new List<KnowledgebaseDocumentEntity>(fileList.Count);
            foreach (var filePath in fileList)
            {
                ct.ThrowIfCancellationRequested();

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Source document file not found: '{filePath}'", filePath);
                }

                var fileInfo = new FileInfo(filePath);
                var docId = Guid.NewGuid();
                string sanitizedName = Path.GetFileName(filePath);
                string targetPath = Path.Combine(kbFolder, $"{docId}_{sanitizedName}");

                File.Copy(filePath, targetPath, overwrite: true);

                var doc = new KnowledgebaseDocumentEntity
                {
                    Id = docId,
                    KnowledgebaseId = knowledgebaseId,
                    FileName = sanitizedName,
                    FilePath = targetPath,
                    FileType = fileInfo.Extension.ToLowerInvariant(),
                    FileSize = fileInfo.Length,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                documents.Add(doc);
                _context.KnowledgebaseDocuments.Add(doc);
            }

            await _context.SaveChangesAsync(ct);

            // 2. Resolve embedding generator
            var generator = ResolveEmbeddingGenerator(kb, options);

            int batchSize = options?.EmbeddingBatchSize > 0 ? options.EmbeddingBatchSize : 16;
            int totalChunksAccumulated = 0;

            // 3. Process each document through the pipeline
            for (int i = 0; i < documents.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var doc = documents[i];
                int docNumber = i + 1;
                double basePercent = (double)i / documents.Count * 100.0;

                try
                {
                    doc.Status = "Processing";
                    await _context.SaveChangesAsync(ct);

                    // Stage A: Extraction
                    progress?.Report(new IndexingProgressReport
                    {
                        Stage = IndexingStage.Extracting,
                        TotalDocuments = documents.Count,
                        CurrentDocumentIndex = docNumber,
                        CurrentDocumentName = doc.FileName,
                        ProcessedDocuments = i,
                        TotalChunksIndexed = totalChunksAccumulated,
                        Percentage = basePercent + (10.0 / documents.Count),
                        Message = $"[{docNumber}/{documents.Count}] Extracting text from {doc.FileName}..."
                    });

                    string? preferredEngine = string.Equals(kb.ParserEngine, "Auto", StringComparison.OrdinalIgnoreCase)
                        ? null
                        : kb.ParserEngine;

                    string extractedText = await _parserFactory.ExtractTextAsync(doc.FilePath, preferredEngine, ct);
                    var resolvedParser = _parserFactory.ResolveParser(doc.FilePath, preferredEngine);
                    doc.ParserEngineUsed = resolvedParser?.EngineId ?? preferredEngine ?? "Auto";

                    // Stage B: Chunking
                    progress?.Report(new IndexingProgressReport
                    {
                        Stage = IndexingStage.Chunking,
                        TotalDocuments = documents.Count,
                        CurrentDocumentIndex = docNumber,
                        CurrentDocumentName = doc.FileName,
                        ProcessedDocuments = i,
                        TotalChunksIndexed = totalChunksAccumulated,
                        Percentage = basePercent + (30.0 / documents.Count),
                        Message = $"[{docNumber}/{documents.Count}] Chunking text for {doc.FileName}..."
                    });

                    var chunkOptions = options?.Chunking ?? new ChunkingOptions { DocumentId = doc.Id };
                    if (chunkOptions.DocumentId != doc.Id)
                    {
                        chunkOptions = chunkOptions with { DocumentId = doc.Id };
                    }

                    var chunks = _textChunker.ChunkText(extractedText, chunkOptions);

                    if (chunks.Count == 0)
                    {
                        doc.Status = "Indexed";
                        doc.ChunkCount = 0;
                        doc.IndexedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(ct);

                        progress?.Report(new IndexingProgressReport
                        {
                            Stage = IndexingStage.DocumentCompleted,
                            TotalDocuments = documents.Count,
                            CurrentDocumentIndex = docNumber,
                            CurrentDocumentName = doc.FileName,
                            ProcessedDocuments = docNumber,
                            TotalChunksIndexed = totalChunksAccumulated,
                            Percentage = (double)docNumber / documents.Count * 100.0,
                            Message = $"[{docNumber}/{documents.Count}] {doc.FileName} contained no extractable text chunks."
                        });

                        continue;
                    }

                    // Stage C: Batch Embedding Generation
                    progress?.Report(new IndexingProgressReport
                    {
                        Stage = IndexingStage.GeneratingEmbeddings,
                        TotalDocuments = documents.Count,
                        CurrentDocumentIndex = docNumber,
                        CurrentDocumentName = doc.FileName,
                        ProcessedDocuments = i,
                        TotalChunksIndexed = totalChunksAccumulated,
                        Percentage = basePercent + (60.0 / documents.Count),
                        Message = $"[{docNumber}/{documents.Count}] Generating embeddings for {chunks.Count} chunk(s) of {doc.FileName}..."
                    });

                    var chunkEntities = new List<DocumentChunkEntity>(chunks.Count);

                    for (int chunkOffset = 0; chunkOffset < chunks.Count; chunkOffset += batchSize)
                    {
                        ct.ThrowIfCancellationRequested();

                        var chunkBatch = chunks.Skip(chunkOffset).Take(batchSize).ToList();
                        var textBatch = chunkBatch.Select(c => c.Content).ToList();

                        var embeddings = await generator.GenerateAsync(textBatch, cancellationToken: ct);

                        if (embeddings == null || embeddings.Count != chunkBatch.Count)
                        {
                            throw new InvalidOperationException(
                                $"Embedding generator returned {embeddings?.Count ?? 0} embeddings, expected {chunkBatch.Count}.");
                        }

                        for (int j = 0; j < chunkBatch.Count; j++)
                        {
                            var textChunk = chunkBatch[j];
                            var embedding = embeddings[j];

                            if (embedding.Vector.Length != kb.VectorDimensions)
                            {
                                throw new InvalidOperationException(
                                    $"Dimension mismatch: Model generated a vector of {embedding.Vector.Length} dimensions, " +
                                    $"but Knowledgebase '{kb.Name}' is configured for {kb.VectorDimensions} dimensions.");
                            }

                            var chunkEntity = new DocumentChunkEntity
                            {
                                Id = textChunk.Id,
                                DocumentId = doc.Id,
                                KnowledgebaseId = kb.Id,
                                TextContent = textChunk.Content,
                                SourceFileName = doc.FileName,
                                PageOrSectionNumber = 1,
                                ChunkIndex = textChunk.SequenceIndex,
                                TokenCount = textChunk.EstimatedTokenCount,
                                EmbeddingVector = ToBlob(embedding.Vector.Span),
                                CreatedAt = DateTime.UtcNow
                            };

                            chunkEntities.Add(chunkEntity);
                        }
                    }

                    // Stage D: Vector Storage
                    progress?.Report(new IndexingProgressReport
                    {
                        Stage = IndexingStage.StoringVectors,
                        TotalDocuments = documents.Count,
                        CurrentDocumentIndex = docNumber,
                        CurrentDocumentName = doc.FileName,
                        ProcessedDocuments = i,
                        TotalChunksIndexed = totalChunksAccumulated,
                        Percentage = basePercent + (90.0 / documents.Count),
                        Message = $"[{docNumber}/{documents.Count}] Storing {chunkEntities.Count} vector(s) into database..."
                    });

                    await _vectorStore.StoreChunksAsync(chunkEntities, ct);

                    totalChunksAccumulated += chunkEntities.Count;
                    doc.Status = "Indexed";
                    doc.ChunkCount = chunkEntities.Count;
                    doc.IndexedAt = DateTime.UtcNow;
                    doc.ErrorMessage = null;
                    await _context.SaveChangesAsync(ct);

                    progress?.Report(new IndexingProgressReport
                    {
                        Stage = IndexingStage.DocumentCompleted,
                        TotalDocuments = documents.Count,
                        CurrentDocumentIndex = docNumber,
                        CurrentDocumentName = doc.FileName,
                        ProcessedDocuments = docNumber,
                        TotalChunksIndexed = totalChunksAccumulated,
                        Percentage = (double)docNumber / documents.Count * 100.0,
                        Message = $"[{docNumber}/{documents.Count}] Successfully indexed {doc.FileName} ({chunkEntities.Count} chunks)."
                    });
                }
                catch (OperationCanceledException)
                {
                    progress?.Report(new IndexingProgressReport
                    {
                        Stage = IndexingStage.Cancelled,
                        TotalDocuments = documents.Count,
                        CurrentDocumentIndex = docNumber,
                        CurrentDocumentName = doc.FileName,
                        ProcessedDocuments = i,
                        TotalChunksIndexed = totalChunksAccumulated,
                        Percentage = basePercent,
                        Message = "Indexing operation was cancelled by user."
                    });
                    throw;
                }
                catch (Exception ex)
                {
                    doc.Status = "Failed";
                    doc.ErrorMessage = ex.Message;
                    await _context.SaveChangesAsync(CancellationToken.None);

                    progress?.Report(new IndexingProgressReport
                    {
                        Stage = IndexingStage.DocumentFailed,
                        TotalDocuments = documents.Count,
                        CurrentDocumentIndex = docNumber,
                        CurrentDocumentName = doc.FileName,
                        ProcessedDocuments = docNumber,
                        TotalChunksIndexed = totalChunksAccumulated,
                        Percentage = (double)docNumber / documents.Count * 100.0,
                        Message = $"[{docNumber}/{documents.Count}] Failed indexing {doc.FileName}: {ex.Message}"
                    });
                }
            }

            kb.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            progress?.Report(new IndexingProgressReport
            {
                Stage = IndexingStage.Completed,
                TotalDocuments = documents.Count,
                CurrentDocumentIndex = documents.Count,
                ProcessedDocuments = documents.Count,
                TotalChunksIndexed = totalChunksAccumulated,
                Percentage = 100.0,
                Message = $"Indexing complete! Successfully processed {documents.Count(d => d.Status == "Indexed")}/{documents.Count} documents ({totalChunksAccumulated} total chunks)."
            });

            return documents;
        }

        private IEmbeddingGenerator<string, Embedding<float>> ResolveEmbeddingGenerator(
            KnowledgebaseEntity kb,
            IngestionOptions? options)
        {
            if (options?.CustomEmbeddingGenerator != null)
            {
                return options.CustomEmbeddingGenerator;
            }

            var provider = _context.Providers.AsNoTracking()
                .FirstOrDefault(p => p.Name.ToLower() == kb.EmbeddingProvider.ToLower()
                                  || p.ProviderType.ToLower() == kb.EmbeddingProvider.ToLower());

            if (provider == null)
            {
                throw new InvalidOperationException(
                    $"Embedding provider '{kb.EmbeddingProvider}' could not be found among configured providers.");
            }

            return _aiClientFactory.CreateEmbeddingGenerator(provider, kb.EmbeddingModel);
        }

        private static byte[] ToBlob(ReadOnlySpan<float> vector)
        {
            return MemoryMarshal.AsBytes(vector).ToArray();
        }
    }
}

