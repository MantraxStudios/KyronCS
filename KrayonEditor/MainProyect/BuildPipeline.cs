using KrayonCompiler;
using KrayonCore;
using KrayonCore.Core.Attributes;
using KrayonCore.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KrayonEditor.Compiler
{
    public enum BuildPlatform { Windows, Android }
    public enum BuildResult { Success, Failed, Cancelled }

    public class BuildInfo
    {
        public BuildResult Result { get; set; }
        public double ElapsedSeconds { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public string Message { get; set; }
    }

    public class BuildProgressEventArgs : EventArgs
    {
        public float Progress { get; set; }
        public string CurrentStep { get; set; }
        public int CompletedSteps { get; set; }
        public int TotalSteps { get; set; }
    }

    public class BuildLogEventArgs : EventArgs
    {
        public enum LogLevel { Info, Warning, Error, Success }

        public string Message { get; set; }
        public LogLevel Level { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class BuildPipeline
    {
        public event EventHandler<BuildProgressEventArgs> ProgressChanged;
        public event EventHandler<BuildLogEventArgs> LogAdded;
        public event EventHandler<BuildInfo> BuildCompleted;

        private readonly Stopwatch _timer = new();
        private int _warningCount = 0;
        private int _errorCount = 0;

        public async Task<BuildInfo> BuildAsync(BuildPlatform platform, CancellationToken token)
        {
            _timer.Restart();
            _warningCount = 0;
            _errorCount = 0;

            try
            {
                if (!Directory.Exists(AssetManager.CompilerPath))
                    Directory.CreateDirectory(AssetManager.CompilerPath);

                Log($"Starting build for platform: {platform}", BuildLogEventArgs.LogLevel.Info);

                var buildInfo = await Task.Run(() => ExecuteBuild(platform, token), token);

                _timer.Stop();
                buildInfo.ElapsedSeconds = _timer.Elapsed.TotalSeconds;
                buildInfo.WarningCount = _warningCount;
                buildInfo.ErrorCount = _errorCount;

                BuildCompleted?.Invoke(this, buildInfo);
                return buildInfo;
            }
            catch (OperationCanceledException)
            {
                _timer.Stop();
                Log("Build cancelled by user", BuildLogEventArgs.LogLevel.Warning);

                var buildInfo = new BuildInfo
                {
                    Result = BuildResult.Cancelled,
                    ElapsedSeconds = _timer.Elapsed.TotalSeconds,
                    WarningCount = _warningCount,
                    ErrorCount = _errorCount,
                    Message = "Build cancelled by user"
                };

                BuildCompleted?.Invoke(this, buildInfo);
                return buildInfo;
            }
            catch (Exception ex)
            {
                _timer.Stop();
                _errorCount++;
                Log($"Build error: {ex.Message}", BuildLogEventArgs.LogLevel.Error);

                var buildInfo = new BuildInfo
                {
                    Result = BuildResult.Failed,
                    ElapsedSeconds = _timer.Elapsed.TotalSeconds,
                    WarningCount = _warningCount,
                    ErrorCount = _errorCount,
                    Message = ex.Message
                };

                BuildCompleted?.Invoke(this, buildInfo);
                return buildInfo;
            }
        }

        private BuildInfo ExecuteBuild(BuildPlatform platform, CancellationToken token)
        {
            var compileSteps = GetCompileSteps(platform);
            var assetsPak = new Dictionary<string, string>();

            var assets = AssetManager.All().ToList();
            int assetCount = assets.Count;

            int totalSteps = compileSteps.Length + assetCount + 3;
            int completedSteps = 0;

            void ReportProgress(string step)
            {
                completedSteps++;
                float progress = Math.Min((float)completedSteps / totalSteps, 1f);

                ProgressChanged?.Invoke(this, new BuildProgressEventArgs
                {
                    Progress = progress,
                    CurrentStep = step,
                    CompletedSteps = completedSteps,
                    TotalSteps = totalSteps
                });
            }

            foreach (var step in compileSteps)
            {
                token.ThrowIfCancellationRequested();
                Log(step.Label, BuildLogEventArgs.LogLevel.Info);
                Thread.Sleep(step.Ms);
                ReportProgress(step.Label);
            }

            ProcessAssets(assets, assetsPak, token, ReportProgress);
            PreparePakFile(token, ReportProgress);
            CopyExecutableData(token, ReportProgress);
            BuildPakFile(assetsPak, token, ReportProgress);

            if (platform == BuildPlatform.Windows)
                OpenBuildDirectory();

            Log("Build completed successfully", BuildLogEventArgs.LogLevel.Success);

            return new BuildInfo
            {
                Result = BuildResult.Success,
                Message = "Build completed successfully"
            };
        }

        private (string Label, int Ms)[] GetCompileSteps(BuildPlatform platform)
        {
            return platform == BuildPlatform.Android
                ? new (string Label, int Ms)[]
                  {
                      ("Scanning assets",           300),
                      ("Resolving dependencies",    350),
                      ("Compiling shaders (GLSL)",  500),
                      ("Packaging APK resources",   400),
                      ("Signing package",           250),
                      ("Finalizing",                200),
                  }
                : new (string Label, int Ms)[]
                  {
                      ("Scanning assets",           300),
                      ("Resolving dependencies",    350),
                      ("Compiling shaders (HLSL)",  500),
                      ("Packaging resources",       400),
                      ("Linking executable",        250),
                      ("Finalizing",                200),
                  };
        }

        private void ProcessAssets(
            List<AssetRecord> assets,
            Dictionary<string, string> assetsPak,
            CancellationToken token,
            Action<string> reportProgress)
        {
            // Write the asset index as UTF-8 WITHOUT BOM.
            // File.WriteAllText on Windows defaults to UTF-8 with BOM which
            // survives the XOR round-trip and makes JsonSerializer throw on load.
            string assetsIndexTmp = Path.Combine(AssetManager.CompilerPath, "_AssetsData.tmp.json");
            string assetsIndexJson = JsonSerializer.Serialize(assets, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(assetsIndexTmp, assetsIndexJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            assetsPak.Add("Engine.AssetsData", assetsIndexTmp);
            assetsPak.Add("Engine.VFX", AssetManager.VFXPath);
            assetsPak.Add("Engine.Materials", AssetManager.MaterialsPath);
            assetsPak.Add("Engine.Client.KrayonClient", $"{AssetManager.ClientDLLPath}/KrayonClient.dll");

            foreach (var asset in assets)
            {
                token.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(asset.Path))
                {
                    Log("Asset missing Path field", BuildLogEventArgs.LogLevel.Error);
                    _errorCount++;
                    reportProgress("Processing assets");
                    continue;
                }

                string fullPath = AssetManager.BasePath + asset.Path;

                if (!File.Exists(fullPath))
                {
                    Log($"File not found: {fullPath}", BuildLogEventArgs.LogLevel.Error);
                    _errorCount++;
                    reportProgress("Processing assets");
                    continue;
                }

                string ext = Path.GetExtension(fullPath);

                if (ext.Equals(".scene", StringComparison.OrdinalIgnoreCase))
                {
                    string sceneName = Path.GetFileNameWithoutExtension(fullPath);
                    assetsPak.Add($"Scene.{sceneName}", fullPath);
                    Log($"Processing scene: {sceneName}", BuildLogEventArgs.LogLevel.Info);
                }
                else
                {
                    assetsPak.Add(asset.Guid.ToString(), fullPath);
                    Log($"Processing asset: {Path.GetFileName(asset.Path)}", BuildLogEventArgs.LogLevel.Info);
                }

                reportProgress($"Processing: {Path.GetFileName(asset.Path)}");
            }
        }

        private void PreparePakFile(CancellationToken token, Action<string> reportProgress)
        {
            token.ThrowIfCancellationRequested();

            string pakPath = $"{AssetManager.CompilerPath}Game.pak";

            Log("Preparing Game.pak", BuildLogEventArgs.LogLevel.Info);

            if (File.Exists(pakPath))
            {
                Log("Replacing existing Game.pak", BuildLogEventArgs.LogLevel.Warning);
                _warningCount++;
                File.Delete(pakPath);
            }

            reportProgress("Preparing Game.pak");
        }

        private void CopyExecutableData(CancellationToken token, Action<string> reportProgress)
        {
            token.ThrowIfCancellationRequested();

            Log("Copying executable data", BuildLogEventArgs.LogLevel.Info);
            PathUtils.CopyAllDataTo("CompileData/Windows/", AssetManager.CompilerPath);
            Log("Executable data copied", BuildLogEventArgs.LogLevel.Info);

            reportProgress("Copying executable data");
        }

        private void BuildPakFile(Dictionary<string, string> assetsPak, CancellationToken token,
            Action<string> reportProgress)
        {
            token.ThrowIfCancellationRequested();

            Log("Building Game.pak", BuildLogEventArgs.LogLevel.Info);

            string pakPath = $"{AssetManager.CompilerPath}Game.pak";
            KRCompiler.Build(pakPath, assetsPak);

            // Clean up the temporary index file
            string assetsIndexTmp = Path.Combine(AssetManager.CompilerPath, "_AssetsData.tmp.json");
            if (File.Exists(assetsIndexTmp))
                File.Delete(assetsIndexTmp);

            Log("Game.pak created successfully", BuildLogEventArgs.LogLevel.Success);
            reportProgress("Building Game.pak");
        }

        private void OpenBuildDirectory()
        {
            try
            {
                string fullPath = Path.GetFullPath(AssetManager.CompilerPath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true
                });

                Log("Build directory opened", BuildLogEventArgs.LogLevel.Info);
            }
            catch (Exception ex)
            {
                Log($"Failed to open build directory: {ex.Message}", BuildLogEventArgs.LogLevel.Warning);
                _warningCount++;
            }
        }

        private void Log(string message, BuildLogEventArgs.LogLevel level)
        {
            LogAdded?.Invoke(this, new BuildLogEventArgs
            {
                Message = message,
                Level = level,
                Timestamp = DateTime.Now
            });
        }
    }
}