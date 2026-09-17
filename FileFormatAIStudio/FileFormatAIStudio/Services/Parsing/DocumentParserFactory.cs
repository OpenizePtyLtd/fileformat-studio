using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Parsing
{
    /// <summary>
    /// Default implementation of IDocumentParserFactory.
    /// Manages document parsers registered via dependency injection, resolving by engine ID or file extension.
    /// </summary>
    public class DocumentParserFactory : IDocumentParserFactory
    {
        private readonly List<IDocumentParser> _parsers;

        public DocumentParserFactory(IEnumerable<IDocumentParser> parsers)
        {
            _parsers = parsers?.ToList() ?? new List<IDocumentParser>();
        }

        public IReadOnlyList<IDocumentParser> GetAllParsers()
        {
            return _parsers.AsReadOnly();
        }

        public IReadOnlyList<IDocumentParser> GetParsersByCategory(FileFormatAIStudio.Services.Benchmarking.DocumentCategory category)
        {
            return _parsers
                .Where(p => p.Category == category)
                .ToList()
                .AsReadOnly();
        }

        public IDocumentParser? GetParser(string engineId)
        {
            if (string.IsNullOrWhiteSpace(engineId))
                return null;

            // Direct match
            var direct = _parsers.FirstOrDefault(p =>
                string.Equals(p.EngineId, engineId, StringComparison.OrdinalIgnoreCase));
            if (direct != null)
                return direct;

            // Legacy alias fallback: "aspose" -> first aspose- engine
            if (string.Equals(engineId, "aspose", StringComparison.OrdinalIgnoreCase))
            {
                return _parsers.FirstOrDefault(p =>
                    p.EngineId.StartsWith("aspose", StringComparison.OrdinalIgnoreCase));
            }

            // Legacy alias fallback: "dotnet-oss" -> first open-source engine
            if (string.Equals(engineId, "dotnet-oss", StringComparison.OrdinalIgnoreCase))
            {
                return _parsers.FirstOrDefault(p =>
                    string.Equals(p.EngineId, "dotnet-oss", StringComparison.OrdinalIgnoreCase) ||
                    p.EngineId is "openxml-words" or "pdfpig" or "exceldatareader" or "csvhelper");
            }

            return null;
        }

        public IDocumentParser? ResolveParser(string filePathOrExtension, string? engineId = null)
        {
            var extension = NormalizeExtension(filePathOrExtension);
            if (string.IsNullOrEmpty(extension))
                return null;

            // If a specific engine was requested, resolve that exact engine or legacy alias
            if (!string.IsNullOrWhiteSpace(engineId))
            {
                // Check direct engine match
                var specificParser = _parsers.FirstOrDefault(p =>
                    string.Equals(p.EngineId, engineId, StringComparison.OrdinalIgnoreCase) &&
                    p.IsAvailable &&
                    p.SupportedExtensions.Contains(extension));

                if (specificParser != null)
                    return specificParser;

                // Check legacy alias for "aspose"
                if (string.Equals(engineId, "aspose", StringComparison.OrdinalIgnoreCase))
                {
                    var asposeMatch = _parsers.FirstOrDefault(p =>
                        p.IsAvailable &&
                        p.SupportedExtensions.Contains(extension) &&
                        p.EngineId.StartsWith("aspose", StringComparison.OrdinalIgnoreCase));

                    if (asposeMatch != null)
                        return asposeMatch;
                }

                // Check legacy alias for "dotnet-oss"
                if (string.Equals(engineId, "dotnet-oss", StringComparison.OrdinalIgnoreCase))
                {
                    var ossMatch = _parsers.FirstOrDefault(p =>
                        p.IsAvailable &&
                        p.SupportedExtensions.Contains(extension) &&
                        (string.Equals(p.EngineId, "dotnet-oss", StringComparison.OrdinalIgnoreCase) ||
                         p.EngineId is "openxml-words" or "pdfpig" or "exceldatareader" or "csvhelper"));

                    if (ossMatch != null)
                        return ossMatch;
                }

                return null;
            }

            // Auto-select highest-priority available parser that supports this extension
            return _parsers
                .Where(p => p.IsAvailable && p.SupportedExtensions.Contains(extension))
                .OrderByDescending(p => p.Priority)
                .FirstOrDefault();
        }

        public IReadOnlySet<string> GetSupportedExtensions()
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var parser in _parsers.Where(p => p.IsAvailable))
            {
                foreach (var ext in parser.SupportedExtensions)
                {
                    extensions.Add(ext);
                }
            }
            return extensions;
        }

        public async Task<string> ExtractTextAsync(
            string filePath,
            string? engineId = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            var extension = NormalizeExtension(filePath);

            IDocumentParser? parser;

            if (!string.IsNullOrWhiteSpace(engineId))
            {
                parser = GetParser(engineId);

                if (parser == null)
                    throw new InvalidOperationException($"Document parser engine '{engineId}' is not registered.");

                if (!parser.IsAvailable)
                    throw new InvalidOperationException($"Document parser engine '{engineId}' is currently unavailable on this system.");

                if (!parser.SupportedExtensions.Contains(extension))
                    throw new NotSupportedException($"Parser '{parser.DisplayName}' ({engineId}) does not support format '{extension}' for file: '{Path.GetFileName(filePath)}'.");
            }
            else
            {
                parser = ResolveParser(filePath);

                if (parser == null)
                    throw new NotSupportedException($"No available parser engine registered to handle '{extension}' files: '{Path.GetFileName(filePath)}'.");
            }

            return await parser.ExtractTextAsync(filePath, cancellationToken);
        }

        public static string NormalizeExtension(string filePathOrExtension)
        {
            if (string.IsNullOrWhiteSpace(filePathOrExtension))
                return string.Empty;

            var trimmed = filePathOrExtension.Trim();

            // Try extracting extension via Path.GetExtension first
            var ext = Path.GetExtension(trimmed);

            // If Path.GetExtension is empty (e.g. input was "docx" without dot or path)
            if (string.IsNullOrEmpty(ext))
            {
                ext = trimmed;
            }

            if (string.IsNullOrWhiteSpace(ext))
                return string.Empty;

            if (!ext.StartsWith('.'))
                ext = "." + ext;

            return ext.ToLowerInvariant();
        }
    }
}

