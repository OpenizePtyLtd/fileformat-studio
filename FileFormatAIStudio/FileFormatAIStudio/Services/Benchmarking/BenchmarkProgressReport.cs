namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Progress report emitted during multi-library benchmark execution.
    /// </summary>
    /// <param name="CurrentDocumentIndex">1-based index of the document currently being evaluated.</param>
    /// <param name="TotalDocuments">Total number of documents in the benchmark run.</param>
    /// <param name="CurrentFileName">Filename currently being processed.</param>
    /// <param name="CurrentEngineDisplayName">Name of the parser engine currently executing.</param>
    /// <param name="Stage">Current execution phase (e.g. "Parsing", "Evaluating Metrics").</param>
    /// <param name="PercentComplete">Overall progress percentage from 0 to 100.</param>
    /// <param name="Message">Descriptive status message.</param>
    public record BenchmarkProgressReport(
        int CurrentDocumentIndex,
        int TotalDocuments,
        string CurrentFileName,
        string? CurrentEngineDisplayName,
        string Stage,
        double PercentComplete,
        string Message
    );
}

