using System;

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

    /// <summary>
    /// Thread-safe progress reporter that dispatches to the WinUI 3 DispatcherQueue if on a background thread,
    /// or invokes the callback synchronously if on the UI thread or in a unit testing environment.
    /// </summary>
    public sealed class AppProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue;

        public AppProgress(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            try
            {
                _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            }
            catch
            {
                // In non-WinUI / unit test environments, DispatcherQueue is not registered
                _dispatcherQueue = null;
            }
        }

        public void Report(T value)
        {
            if (_dispatcherQueue != null && !_dispatcherQueue.HasThreadAccess)
            {
                _dispatcherQueue.TryEnqueue(() => _handler(value));
            }
            else
            {
                _handler(value);
            }
        }
    }
}

