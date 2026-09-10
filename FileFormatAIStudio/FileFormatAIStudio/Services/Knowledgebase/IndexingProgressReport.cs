namespace FileFormatAIStudio.Services.Knowledgebase
{
    /// <summary>
    /// Represents the high-level lifecycle stage of the document ingestion pipeline.
    /// </summary>
    public enum IndexingStage
    {
        Starting,
        CopyingFiles,
        Extracting,
        Chunking,
        GeneratingEmbeddings,
        StoringVectors,
        DocumentCompleted,
        DocumentFailed,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Progress report payload emitted during knowledgebase document ingestion and indexing.
    /// </summary>
    public sealed record IndexingProgressReport
    {
        /// <summary>
        /// Current lifecycle stage of the indexing operation.
        /// </summary>
        public IndexingStage Stage { get; init; } = IndexingStage.Starting;

        /// <summary>
        /// Total number of documents to be processed in this batch.
        /// </summary>
        public int TotalDocuments { get; init; }

        /// <summary>
        /// 1-based index of the document currently being processed.
        /// </summary>
        public int CurrentDocumentIndex { get; init; }

        /// <summary>
        /// File name of the document currently being processed.
        /// </summary>
        public string CurrentDocumentName { get; init; } = string.Empty;

        /// <summary>
        /// Number of documents that have completed processing (successfully or failed).
        /// </summary>
        public int ProcessedDocuments { get; init; }

        /// <summary>
        /// Cumulative number of chunks indexed into the vector store.
        /// </summary>
        public int TotalChunksIndexed { get; init; }

        /// <summary>
        /// Overall percentage of the ingestion job completed (0.0 - 100.0).
        /// </summary>
        public double Percentage { get; init; }

        /// <summary>
        /// Human-readable message describing the current progress.
        /// </summary>
        public string Message { get; init; } = string.Empty;
    }
}

