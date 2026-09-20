using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Parsing.Node
{
    /// <summary>
    /// Contract for discovering and evaluating the Node.js runtime and NodeHost subsystem.
    /// </summary>
    public interface INodeJsRuntimeService
    {
        /// <summary>
        /// Indicates whether Node.js and NodeHost dependencies are available on the machine.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Absolute path to the resolved node.exe binary, if available.
        /// </summary>
        string? NodeExecutablePath { get; }

        /// <summary>
        /// Absolute path to the resolved NodeHost/index.js dispatcher script.
        /// </summary>
        string? NodeHostScriptPath { get; }

        /// <summary>
        /// Performs or retrieves a preflight health check probing runtime version and registered engines.
        /// </summary>
        Task<NodeJsRuntimeInfo> GetRuntimeInfoAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Allows setting a user-specified custom path to the Node.js executable.
        /// </summary>
        void SetCustomNodeExecutablePath(string? customPath);
    }
}

