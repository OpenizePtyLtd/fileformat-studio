using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing.Node
{
    /// <summary>
    /// Base class for all document parser engines executed via the NodeHost subsystem.
    /// Provides standardized availability checks, extension verification, and IPC delegation.
    /// </summary>
    public abstract class NodeJsDocumentParserBase : IDocumentParser
    {
        protected readonly INodeJsHostService NodeHost;

        protected NodeJsDocumentParserBase(INodeJsHostService nodeHost)
        {
            NodeHost = nodeHost ?? throw new ArgumentNullException(nameof(nodeHost));
        }

        public abstract DocumentCategory Category { get; }

        public abstract string EngineId { get; }

        public abstract string DisplayName { get; }

        public abstract int Priority { get; }

        public virtual bool IsAvailable => NodeHost.IsAvailable;

        public abstract IReadOnlySet<string> SupportedExtensions { get; }

        public virtual async Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !SupportedExtensions.Contains(ext))
            {
                throw new NotSupportedException($"Parser '{DisplayName}' ({EngineId}) does not support format '{ext}' for file '{Path.GetFileName(filePath)}'.");
            }

            if (!IsAvailable)
            {
                throw new InvalidOperationException($"Parser '{DisplayName}' is unavailable because the Node.js runtime or NodeHost could not be located.");
            }

            var response = await NodeHost.ExecuteAsync(EngineId, "extractText", filePath, cancellationToken);

            if (!response.Success)
            {
                throw new InvalidOperationException($"Node.js parsing failed for '{Path.GetFileName(filePath)}' using engine '{EngineId}': {response.Error}");
            }

            return response.Text ?? string.Empty;
        }
    }
}

