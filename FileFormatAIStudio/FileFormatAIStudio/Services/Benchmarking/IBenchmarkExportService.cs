using System;
using FileFormatAIStudio.Data.Entities;

namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Service responsible for serializing benchmark session outcomes to CSV and JSON formats,
    /// and mapping persisted EF Core benchmark entities to runtime domain models.
    /// </summary>
    public interface IBenchmarkExportService
    {
        /// <summary>
        /// Generates a structured CSV report for a benchmark session.
        /// </summary>
        string ExportToCsv(BenchmarkSessionResult session);

        /// <summary>
        /// Generates a formatted JSON representation of a benchmark session.
        /// </summary>
        string ExportToJson(BenchmarkSessionResult session);

        /// <summary>
        /// Maps an EF Core BenchmarkSessionEntity (with its document and run graphs) into a domain BenchmarkSessionResult.
        /// </summary>
        BenchmarkSessionResult MapEntityToResult(BenchmarkSessionEntity entity);
    }
}
