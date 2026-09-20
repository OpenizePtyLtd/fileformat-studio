using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FileFormatAIStudio.Services.Parsing.Node
{
    /// <summary>
    /// Outgoing request payload sent to the NodeHost process.
    /// </summary>
    public class NodeJsRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("engineId")]
        public string? EngineId { get; set; }

        [JsonPropertyName("command")]
        public string Command { get; set; } = "extractText";

        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } = string.Empty;

        [JsonPropertyName("options")]
        public Dictionary<string, object>? Options { get; set; }
    }

    /// <summary>
    /// Incoming response payload received from the NodeHost process.
    /// </summary>
    public class NodeJsResponse
    {
        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("characterCount")]
        public int CharacterCount { get; set; }

        [JsonPropertyName("executionMs")]
        public double ExecutionMs { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// Metadata for an engine reported by NodeHost.
    /// </summary>
    public class NodeJsEngineMetadata
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("supportedExtensions")]
        public List<string> SupportedExtensions { get; set; } = new();
    }

    /// <summary>
    /// Diagnostics and health status for the Node.js runtime and NodeHost subsystem.
    /// </summary>
    public class NodeJsRuntimeInfo
    {
        public bool IsAvailable { get; set; }
        public string? NodeVersion { get; set; }
        public string? ExecutablePath { get; set; }
        public string? NodeHostScriptPath { get; set; }
        public IReadOnlyList<NodeJsEngineMetadata> InstalledEngines { get; set; } = Array.Empty<NodeJsEngineMetadata>();
        public string StatusMessage { get; set; } = string.Empty;
    }
}

