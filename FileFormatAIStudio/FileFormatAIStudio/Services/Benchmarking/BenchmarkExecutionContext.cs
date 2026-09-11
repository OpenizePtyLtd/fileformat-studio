using System;

namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Encapsulates the execution context and measured metrics of a document parser run
    /// passed to metric evaluators for scoring.
    /// </summary>
    /// <param name="FilePath">Full path to the source document file.</param>
    /// <param name="FileName">Name of the document file with extension.</param>
    /// <param name="Extension">Normalized file extension (e.g. ".docx").</param>
    /// <param name="Category">Document category of the tested file.</param>
    /// <param name="FileSizeBytes">Size of the document file in bytes.</param>
    /// <param name="EngineId">Identifier of the parser engine evaluated (e.g. "aspose", "dotnet-oss").</param>
    /// <param name="EngineDisplayName">Human-friendly name of the parser engine.</param>
    /// <param name="ExtractedText">Extracted plain text content (empty string if failed).</param>
    /// <param name="ElapsedTime">Time elapsed during extraction.</param>
    /// <param name="AllocatedBytes">GC heap memory allocated during extraction on the worker thread.</param>
    /// <param name="IsSuccess">Whether extraction completed without throwing an unhandled exception.</param>
    /// <param name="ThrownException">Exception instance if extraction failed.</param>
    public record BenchmarkExecutionContext(
        string FilePath,
        string FileName,
        string Extension,
        DocumentCategory Category,
        long FileSizeBytes,
        string EngineId,
        string EngineDisplayName,
        string ExtractedText,
        TimeSpan ElapsedTime,
        long AllocatedBytes,
        bool IsSuccess,
        Exception? ThrownException = null
    )
    {
        /// <summary>
        /// Gets the total raw character count of the extracted text.
        /// </summary>
        public long CharacterCount => ExtractedText?.Length ?? 0;
    }
}
