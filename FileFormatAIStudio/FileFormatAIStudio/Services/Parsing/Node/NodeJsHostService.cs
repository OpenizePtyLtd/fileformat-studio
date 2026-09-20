using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Parsing.Node
{
    /// <summary>
    /// Executes requests against the NodeHost subsystem via process execution and JSON IPC.
    /// </summary>
    public class NodeJsHostService : INodeJsHostService
    {
        private readonly INodeJsRuntimeService _runtimeService;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(60);

        public NodeJsHostService(INodeJsRuntimeService runtimeService)
        {
            _runtimeService = runtimeService ?? throw new ArgumentNullException(nameof(runtimeService));
        }

        public bool IsAvailable => _runtimeService.IsAvailable;

        public Task<NodeJsResponse> ExecuteAsync(string? engineId, string command, string filePath, CancellationToken cancellationToken = default)
        {
            var req = new NodeJsRequest
            {
                EngineId = engineId,
                Command = command,
                FilePath = filePath
            };
            return ExecuteAsync(req, cancellationToken);
        }

        public async Task<NodeJsResponse> ExecuteAsync(NodeJsRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var nodeExe = _runtimeService.NodeExecutablePath;
            var scriptPath = _runtimeService.NodeHostScriptPath;

            if (string.IsNullOrEmpty(nodeExe) || !File.Exists(nodeExe))
            {
                return new NodeJsResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = "Node.js executable could not be found. Please ensure Node.js is installed or specify its path in Settings."
                };
            }

            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                return new NodeJsResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = "NodeHost dispatcher script (NodeHost/index.js) was not found."
                };
            }

            string? tempFilePath = null;
            string cliArgs;

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(request);
            if (jsonBytes.Length > 4000)
            {
                // Write payload to temporary file to avoid command-line length limits
                tempFilePath = Path.Combine(Path.GetTempPath(), $"ff_req_{Guid.NewGuid():N}.json");
                await File.WriteAllBytesAsync(tempFilePath, jsonBytes, cancellationToken);
                cliArgs = $"\"{scriptPath}\" --request-file \"{tempFilePath}\"";
            }
            else
            {
                var base64 = Convert.ToBase64String(jsonBytes);
                cliArgs = $"\"{scriptPath}\" --request {base64}";
            }

            try
            {
                var scriptDir = Path.GetDirectoryName(scriptPath)!;
                var startInfo = new ProcessStartInfo
                {
                    FileName = nodeExe,
                    Arguments = cliArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = scriptDir
                };

                var nodeModulesDir = Path.Combine(scriptDir, "node_modules");
                if (Directory.Exists(nodeModulesDir))
                {
                    startInfo.EnvironmentVariables["NODE_PATH"] = nodeModulesDir;
                }

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                // Guarantee clean child process termination
                JobObjectHelper.AssociateProcess(process);

                using var timeoutCts = new CancellationTokenSource(_defaultTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
                var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

                // Register process kill on cancellation
                using var cancelReg = linkedCts.Token.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                        // Best effort process termination
                    }
                });

                string stdout;
                string stderr;
                try
                {
                    await process.WaitForExitAsync(linkedCts.Token);
                    stdout = await stdoutTask;
                    stderr = await stderrTask;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return new NodeJsResponse
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Error = "Operation was cancelled by user."
                    };
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    return new NodeJsResponse
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Error = $"NodeHost execution timed out after {_defaultTimeout.TotalSeconds} seconds."
                    };
                }

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    try
                    {
                        var response = JsonSerializer.Deserialize<NodeJsResponse>(stdout);
                        if (response != null)
                        {
                            return response;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        return new NodeJsResponse
                        {
                            RequestId = request.RequestId,
                            Success = false,
                            Error = $"Failed to parse NodeHost JSON response: {jsonEx.Message}\nOutput: {stdout}\nStderr: {stderr}"
                        };
                    }
                }

                return new NodeJsResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = $"Node process exited with code {process.ExitCode}. {stderr}".Trim()
                };
            }
            finally
            {
                if (tempFilePath != null && File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { }
                }
            }
        }
    }
}

