using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Parsing
{
    /// <summary>
    /// Built-in zero-dependency parser for standard plain text and markup files
    /// (.txt, .md, .markdown, .json, .csv, .xml, .log, .yaml, .yml).
    /// </summary>
    public class PlainTextParser : IDocumentParser
    {
        public const string ParserEngineId = "plaintext";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt",
            ".md",
            ".markdown",
            ".json",
            ".csv",
            ".tsv",
            ".xml",
            ".log",
            ".yaml",
            ".yml",
            ".ini",
            ".conf",
            ".config",
            ".sql",
            ".cs",
            ".js",
            ".ts",
            ".py"
        };

        public string EngineId => ParserEngineId;

        public string DisplayName => "Plain Text / Source Files";

        public int Priority => 10;

        public bool IsAvailable => true;

        public IReadOnlySet<string> SupportedExtensions => Extensions;

        public async Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            return await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        }
    }
}

