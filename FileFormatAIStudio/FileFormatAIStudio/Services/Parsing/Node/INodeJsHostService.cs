using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Parsing.Node
{
    /// <summary>
    /// Service contract for executing NodeHost subprocess requests and managing IPC communication.
    /// </summary>
    public interface INodeJsHostService
    {
        /// <summary>
        /// Indicates whether NodeHost and the Node.js runtime are ready for execution.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Executes a request against the NodeHost process.
        /// </summary>
        Task<NodeJsResponse> ExecuteAsync(NodeJsRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Convenience method to execute a command on a specific engine and document path.
        /// </summary>
        Task<NodeJsResponse> ExecuteAsync(string? engineId, string command, string filePath, CancellationToken cancellationToken = default);
    }
}

