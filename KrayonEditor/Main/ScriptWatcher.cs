using KrayonCore.Core.Attributes;
using KrayonEditor.UI;
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
        private DateTime _lastBuildTime = DateTime.MinValue;
        private const int MIN_BUILD_INTERVAL_MS = 2000;

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
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileChanged;
                _watcher.Created -= OnFileChanged;
                _watcher.Renamed -= OnFileRenamed;
                _watcher?.Dispose();
                _watcher = null;
            }

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
            // Ignorar archivos temporales, de backup y bin/obj
            string fileName = e.Name ?? "";
            if (fileName.Contains("~") ||
                fileName.EndsWith(".tmp") ||
                fileName.EndsWith(".swp") ||
                fileName.Contains("\\bin\\") ||
                fileName.Contains("\\obj\\") ||
                fileName.Contains("/bin/") ||
                fileName.Contains("/obj/"))
                return;

            Console.WriteLine($"[ScriptWatcher] Cambio detectado: {e.Name}");
            EditorNotifications.Info("CSharpLogic detected changes, starting recompilation...");
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
                // Verificar si ha pasado suficiente tiempo desde el último build
                var timeSinceLastBuild = (DateTime.Now - _lastBuildTime).TotalMilliseconds;
                if (timeSinceLastBuild < MIN_BUILD_INTERVAL_MS && _lastBuildTime != DateTime.MinValue)
                {
                    _buildPending = true;
                    return;
                }

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
                try
                {
                    // Delay para agrupar múltiples cambios seguidos
                    await Task.Delay(1000, _cts.Token);
                    await RunBuild();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[ScriptWatcher] Build programado cancelado");
                }
            }, _cts.Token);
        }

        private async Task RunBuild()
        {
            Console.WriteLine("[ScriptWatcher] ═══════════════════════════════════");
            Console.WriteLine("[ScriptWatcher] Iniciando compilación...");

            try
            {
                // PASO 1: Liberar recursos antes de compilar
                // Invocar evento de pre-build para que el sistema descargue assemblies si es necesario
                Console.WriteLine("[ScriptWatcher] Liberando recursos antes de compilar...");

                // Forzar garbage collection para liberar cualquier referencia
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Esperar a que se liberen los archivos
                await Task.Delay(500, _cts.Token);

                string fullCsProjPath = Path.GetFullPath(AssetManager.CSProj);
                string workingDir = Path.GetDirectoryName(fullCsProjPath);

                Console.WriteLine($"[ScriptWatcher] CsProj: {fullCsProjPath}");
                Console.WriteLine($"[ScriptWatcher] WorkingDir: {workingDir}");

                // PASO 2: Intentar compilar con reintentos
                bool buildSuccess = false;
                string buildError = "";
                int maxRetries = 3;

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    if (attempt > 1)
                    {
                        Console.WriteLine($"[ScriptWatcher] Reintentando... (intento {attempt}/{maxRetries})");
                        await Task.Delay(1000 * attempt, _cts.Token); // Esperar más tiempo en cada reintento

                        // Forzar GC de nuevo
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        // Agregar --force para forzar rebuild y --no-incremental
                        Arguments = $"build \"{fullCsProjPath}\" -c Debug --nologo --no-incremental",
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
                        buildSuccess = true;
                        break;
                    }
                    else
                    {
                        var allOutput = output.Concat(errors).ToList();

                        // Verificar si es un error de archivo bloqueado
                        bool isFileLocked = allOutput.Any(l =>
                            l.Contains("being used by another process") ||
                            l.Contains("cannot access the file") ||
                            l.Contains("proceso no puede obtener acceso al archivo") ||
                            l.Contains("MSB3021"));

                        if (isFileLocked && attempt < maxRetries)
                        {
                            Console.WriteLine($"[ScriptWatcher] ⚠ Archivo bloqueado, esperando...");
                            continue;
                        }

                        buildError = string.Join("\n", allOutput.Where(l =>
                            l.Contains("error") || l.Contains("Error")));

                        if (attempt >= maxRetries)
                            break;
                    }
                }

                // PASO 3: Actualizar tiempo del último build
                _lastBuildTime = DateTime.Now;

                // PASO 4: Notificar resultado
                if (buildSuccess)
                {
                    Console.WriteLine("[ScriptWatcher] ✓ Build exitoso");
                    Console.WriteLine("[ScriptWatcher] ═══════════════════════════════════");

                    // Pequeño delay antes de invocar el evento
                    await Task.Delay(200, _cts.Token);

                    OnBuildSuccess?.Invoke();
                }
                else
                {
                    Console.WriteLine($"[ScriptWatcher] ✗ Build fallido después de {maxRetries} intentos");
                    Console.WriteLine($"[ScriptWatcher] Error:\n{buildError}");
                    Console.WriteLine("[ScriptWatcher] ═══════════════════════════════════");
                    OnBuildFailed?.Invoke(buildError);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[ScriptWatcher] Build cancelado");
                Console.WriteLine("[ScriptWatcher] ═══════════════════════════════════");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptWatcher] ✗ Error inesperado al compilar: {ex.Message}");
                Console.WriteLine($"[ScriptWatcher] StackTrace: {ex.StackTrace}");
                Console.WriteLine("[ScriptWatcher] ═══════════════════════════════════");
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
                    Console.WriteLine("[ScriptWatcher] Cambios pendientes detectados, programando nuevo build...");
                    // Esperar más tiempo antes de rebuild
                    await Task.Delay(2000, _cts.Token);
                    await RunBuild();
                }
            }
        }
    }
}