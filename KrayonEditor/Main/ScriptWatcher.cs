using KrayonCore.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KrayonCore.Scripting
{
    public class ScriptWatcher : IDisposable
    {
        private FileSystemWatcher _watcher = null;
        private CancellationTokenSource _cts = null;
        private bool _buildPending = false;
        private bool _isBuilding = false;
        private readonly object _lock = new object();

        public event Action OnBuildSuccess;
        public event Action<string> OnBuildFailed;

        public void Start()
        {
            if (!Directory.Exists(AssetManager.BasePath))
            {
                Console.WriteLine($"[ScriptWatcher Error] No se encontró el directorio: {AssetManager.BasePath}");
                return;
            }

            if (!File.Exists(AssetManager.CSProj))
            {
                Console.WriteLine($"[ScriptWatcher Error] No se encontró el .csproj en: {AssetManager.CSProj}");
                return;
            }

            _cts = new CancellationTokenSource();

            _watcher = new FileSystemWatcher(AssetManager.BasePath)
            {
                Filter = "*.cs",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.EnableRaisingEvents = true;

            Console.WriteLine($"[ScriptWatcher] Vigilando .cs en: {AssetManager.BasePath}");
        }

        public void Stop()
        {
            _watcher?.Dispose();
            _watcher = null;
            _cts?.Cancel();
            Console.WriteLine("[ScriptWatcher] Detenido");
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"[ScriptWatcher] Cambio detectado: {e.Name}");
            ScheduleBuild();
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (e.Name.EndsWith(".cs") || e.OldName.EndsWith(".cs"))
            {
                Console.WriteLine($"[ScriptWatcher] Renombrado: {e.OldName} → {e.Name}");
                ScheduleBuild();
            }
        }

        private void ScheduleBuild()
        {
            lock (_lock)
            {
                if (_isBuilding)
                {
                    _buildPending = true;
                    return;
                }

                _buildPending = false;
                _isBuilding = true;
            }

            Task.Run(async () =>
            {
                // Delay para agrupar multiples cambios seguidos
                await Task.Delay(500, _cts.Token);
                await RunBuild();
            }, _cts.Token);
        }

        private async Task RunBuild()
        {
            Console.WriteLine("[ScriptWatcher] Compilando...");

            try
            {
                string fullCsProjPath = Path.GetFullPath(AssetManager.CSProj);
                string workingDir = Path.GetDirectoryName(fullCsProjPath);

                Console.WriteLine($"[ScriptWatcher] CsProj: {fullCsProjPath}");
                Console.WriteLine($"[ScriptWatcher] WorkingDir: {workingDir}");

                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"build \"{fullCsProjPath}\" -c Debug --nologo",
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                var output = new List<string>();
                var errors = new List<string>();

                process.OutputDataReceived += (s, e) => { if (e.Data != null) output.Add(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) errors.Add(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(_cts.Token);

                if (process.ExitCode == 0)
                {
                    Console.WriteLine("[ScriptWatcher] Build exitoso");
                    OnBuildSuccess?.Invoke();
                }
                else
                {
                    var errorMsg = string.Join("\n", output.Concat(errors).Where(l =>
                        l.Contains("error") || l.Contains("Error")));
                    Console.WriteLine($"[ScriptWatcher] Build fallido:\n{errorMsg}");
                    OnBuildFailed?.Invoke(errorMsg);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[ScriptWatcher] Build cancelado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptWatcher] Error al compilar: {ex.Message}");
                OnBuildFailed?.Invoke(ex.Message);
            }
            finally
            {
                bool pending;
                lock (_lock)
                {
                    pending = _buildPending;
                    _buildPending = false;
                    _isBuilding = pending;
                }

                if (pending)
                {
                    Console.WriteLine("[ScriptWatcher] Cambios detectados durante el build, recompilando...");
                    await RunBuild();
                }
            }
        }
    }
}