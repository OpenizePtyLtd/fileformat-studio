using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FileFormatAIStudio.Services.Parsing.Node
{
    /// <summary>
    /// Discovers, validates, and probes the local Node.js executable and NodeHost subsystem.
    /// </summary>
    public class NodeJsRuntimeService : INodeJsRuntimeService
    {
        private string? _customNodePath;
        private string? _resolvedNodePath;
        private string? _resolvedNodeHostScriptPath;
        private NodeJsRuntimeInfo? _cachedRuntimeInfo;
        private readonly object _lock = new();

        public bool IsAvailable
        {
            get
            {
                var info = _cachedRuntimeInfo ?? ProbeSynchronousQuickCheck();
                return info.IsAvailable;
            }
        }

        public string? NodeExecutablePath => _resolvedNodePath ?? LocateNodeExecutable();

        public string? NodeHostScriptPath => _resolvedNodeHostScriptPath ?? LocateNodeHostScript();

        public void SetCustomNodeExecutablePath(string? customPath)
        {
            lock (_lock)
            {
                _customNodePath = string.IsNullOrWhiteSpace(customPath) ? null : customPath.Trim();
                _resolvedNodePath = null;
                _cachedRuntimeInfo = null;
            }
        }

        public async Task<NodeJsRuntimeInfo> GetRuntimeInfoAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (!forceRefresh && _cachedRuntimeInfo != null)
            {
                return _cachedRuntimeInfo;
            }

            var nodePath = LocateNodeExecutable();
            var scriptPath = LocateNodeHostScript();

            if (string.IsNullOrEmpty(nodePath) || !File.Exists(nodePath))
            {
                var missingInfo = new NodeJsRuntimeInfo
                {
                    IsAvailable = false,
                    ExecutablePath = null,
                    NodeHostScriptPath = scriptPath,
                    StatusMessage = "Node.js executable was not found on PATH or standard install paths."
                };
                lock (_lock) { _cachedRuntimeInfo = missingInfo; }
                return missingInfo;
            }

            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                var missingScriptInfo = new NodeJsRuntimeInfo
                {
                    IsAvailable = false,
                    ExecutablePath = nodePath,
                    NodeHostScriptPath = null,
                    StatusMessage = "NodeHost/index.js dispatcher script was not found."
                };
                lock (_lock) { _cachedRuntimeInfo = missingScriptInfo; }
                return missingScriptInfo;
            }

            // Probe via node index.js --probe
            try
            {
                var scriptDir = Path.GetDirectoryName(scriptPath)!;
                var startInfo = new ProcessStartInfo
                {
                    FileName = nodePath,
                    Arguments = $"\"{scriptPath}\" --probe",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = scriptDir
                };

                var nodeModulesDir = Path.Combine(scriptDir, "node_modules");
                if (Directory.Exists(nodeModulesDir))
                {
                    startInfo.EnvironmentVariables["NODE_PATH"] = nodeModulesDir;
                }

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                await process.WaitForExitAsync(cts.Token);
                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
                {
                    var response = JsonSerializer.Deserialize<NodeJsResponse>(stdout);
                    if (response != null && response.Success && response.Metadata != null)
                    {
                        var version = response.Metadata.TryGetValue("nodeVersion", out var ver) ? ver.ToString() : null;
                        var engines = new List<NodeJsEngineMetadata>();

                        if (response.Metadata.TryGetValue("engines", out var enginesObj))
                        {
                            var enginesJson = JsonSerializer.Serialize(enginesObj);
                            var parsed = JsonSerializer.Deserialize<List<NodeJsEngineMetadata>>(enginesJson);
                            if (parsed != null) engines.AddRange(parsed);
                        }

                        var result = new NodeJsRuntimeInfo
                        {
                            IsAvailable = true,
                            NodeVersion = version,
                            ExecutablePath = nodePath,
                            NodeHostScriptPath = scriptPath,
                            InstalledEngines = engines,
                            StatusMessage = $"Node.js {version} ready with {engines.Count} engine(s) registered."
                        };

                        lock (_lock) { _cachedRuntimeInfo = result; }
                        return result;
                    }
                }

                var failedInfo = new NodeJsRuntimeInfo
                {
                    IsAvailable = false,
                    ExecutablePath = nodePath,
                    NodeHostScriptPath = scriptPath,
                    StatusMessage = $"NodeHost probe exited with code {process.ExitCode}: {stderr}"
                };
                lock (_lock) { _cachedRuntimeInfo = failedInfo; }
                return failedInfo;
            }
            catch (Exception ex)
            {
                var errInfo = new NodeJsRuntimeInfo
                {
                    IsAvailable = false,
                    ExecutablePath = nodePath,
                    NodeHostScriptPath = scriptPath,
                    StatusMessage = $"Failed to execute Node probe: {ex.Message}"
                };
                lock (_lock) { _cachedRuntimeInfo = errInfo; }
                return errInfo;
            }
        }

        private NodeJsRuntimeInfo ProbeSynchronousQuickCheck()
        {
            var nodePath = LocateNodeExecutable();
            var scriptPath = LocateNodeHostScript();

            var available = !string.IsNullOrEmpty(nodePath) && File.Exists(nodePath) &&
                            !string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath);

            var info = new NodeJsRuntimeInfo
            {
                IsAvailable = available,
                ExecutablePath = nodePath,
                NodeHostScriptPath = scriptPath,
                StatusMessage = available ? "Node runtime detected." : "Node runtime not available."
            };

            lock (_lock) { _cachedRuntimeInfo = info; }
            return info;
        }

        private string? LocateNodeExecutable()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_resolvedNodePath) && File.Exists(_resolvedNodePath))
                {
                    return _resolvedNodePath;
                }

                // 1. User custom path
                if (!string.IsNullOrWhiteSpace(_customNodePath))
                {
                    _resolvedNodePath = _customNodePath;
                    return _resolvedNodePath;
                }

                // 2. Search on PATH
                var pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    var dirs = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var dir in dirs)
                    {
                        try
                        {
                            var candidate = Path.Combine(dir.Trim('"', ' '), "node.exe");
                            if (File.Exists(candidate))
                            {
                                _resolvedNodePath = Path.GetFullPath(candidate);
                                return _resolvedNodePath;
                            }
                        }
                        catch
                        {
                            // Ignore invalid paths in PATH env var
                        }
                    }
                }

                // 3. Known standard locations on Windows
                var standardPaths = new[]
                {
                    @"C:\Program Files\nodejs\node.exe",
                    @"C:\Program Files (x86)\nodejs\node.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\node\node.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"nvm\current\node.exe")
                };

                foreach (var path in standardPaths)
                {
                    if (File.Exists(path))
                    {
                        _resolvedNodePath = Path.GetFullPath(path);
                        return _resolvedNodePath;
                    }
                }

                return null;
            }
        }

        private string? LocateNodeHostScript()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_resolvedNodeHostScriptPath) && File.Exists(_resolvedNodeHostScriptPath))
                {
                    return _resolvedNodeHostScriptPath;
                }

                var baseDir = AppDomain.CurrentDomain.BaseDirectory;

                // 1. Search repository source tree for NodeHost with populated node_modules (development mode)
                var current = new DirectoryInfo(baseDir);
                for (int i = 0; i < 8 && current != null; i++)
                {
                    var devCandidate = Path.Combine(current.FullName, "FileFormatAIStudio", "FileFormatAIStudio", "NodeHost", "index.js");
                    var devModules = Path.Combine(current.FullName, "FileFormatAIStudio", "FileFormatAIStudio", "NodeHost", "node_modules");
                    if (File.Exists(devCandidate) && Directory.Exists(devModules) && File.Exists(Path.Combine(devModules, "officeparser", "package.json")))
                    {
                        _resolvedNodeHostScriptPath = Path.GetFullPath(devCandidate);
                        return _resolvedNodeHostScriptPath;
                    }

                    var altCandidate = Path.Combine(current.FullName, "NodeHost", "index.js");
                    var altModules = Path.Combine(current.FullName, "NodeHost", "node_modules");
                    if (File.Exists(altCandidate) && Directory.Exists(altModules) && File.Exists(Path.Combine(altModules, "officeparser", "package.json")))
                    {
                        _resolvedNodeHostScriptPath = Path.GetFullPath(altCandidate);
                        return _resolvedNodeHostScriptPath;
                    }

                    current = current.Parent;
                }

                // 2. Check if BaseDirectory has NodeHost WITH populated node_modules (packaged / published mode)
                var outputNodeHost = Path.Combine(baseDir, "NodeHost", "index.js");
                var outputModules = Path.Combine(baseDir, "NodeHost", "node_modules");
                if (File.Exists(outputNodeHost) && Directory.Exists(outputModules))
                {
                    _resolvedNodeHostScriptPath = Path.GetFullPath(outputNodeHost);
                    return _resolvedNodeHostScriptPath;
                }

                // 3. Fallback to outputNodeHost even if node_modules hasn't been copied
                if (File.Exists(outputNodeHost))
                {
                    _resolvedNodeHostScriptPath = Path.GetFullPath(outputNodeHost);
                    return _resolvedNodeHostScriptPath;
                }

                return null;
            }
        }
    }
}

