using ImGuiNET;
using KrayonCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace KrayonEditor.UI
{
    public class CompilerUI : UIBehaviour
    {
        private enum BuildState { Idle, Building, Success, Failed }

        private BuildState _state = BuildState.Idle;
        private float _progress = 0f;
        private string _currentStep = string.Empty;
        private readonly List<LogEntry> _log = new();
        private CancellationTokenSource _cts = null;
        private readonly Stopwatch _buildTimer = new();
        private int _warningCount = 0;
        private int _errorCount = 0;
        private bool _scrollToBottom = false;
        private bool _autoScroll = true;

        private static readonly Vector4 ColorSuccess = new(0.20f, 0.80f, 0.40f, 1f);
        private static readonly Vector4 ColorError = new(0.90f, 0.25f, 0.25f, 1f);
        private static readonly Vector4 ColorWarning = new(0.95f, 0.75f, 0.10f, 1f);
        private static readonly Vector4 ColorInfo = new(0.65f, 0.65f, 0.65f, 1f);
        private static readonly Vector4 ColorDim = new(0.40f, 0.40f, 0.40f, 1f);

        private readonly record struct LogEntry(string Message, LogLevel Level, string Timestamp);
        private enum LogLevel { Info, Warning, Error, Success }

        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            ImGui.SetNextWindowSize(new Vector2(620, 420), ImGuiCond.FirstUseEver);
            ImGui.Begin("Compiler", ref _isVisible);

            DrawToolbar();
            ImGui.Separator();
            DrawProgressSection();
            ImGui.Separator();
            DrawLog();

            ImGui.End();
        }

        private void DrawToolbar()
        {
            bool isBuilding = _state == BuildState.Building;

            if (isBuilding)
            {
                ImGui.BeginDisabled();
            }

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.50f, 0.25f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.20f, 0.65f, 0.35f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.10f, 0.40f, 0.20f, 1f));

            if (ImGui.Button("  Build  "))
                StartBuild();

            ImGui.PopStyleColor(3);

            if (isBuilding)
                ImGui.EndDisabled();

            ImGui.SameLine();

            if (!isBuilding)
                ImGui.BeginDisabled();

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.15f, 0.15f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.70f, 0.20f, 0.20f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.45f, 0.10f, 0.10f, 1f));

            if (ImGui.Button("  Cancel  "))
                CancelBuild();

            ImGui.PopStyleColor(3);

            if (!isBuilding)
                ImGui.EndDisabled();

            ImGui.SameLine();

            if (ImGui.Button("  Clear Log  "))
                ClearLog();

            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 140f);

            Vector4 stateColor = _state switch
            {
                BuildState.Idle => ColorDim,
                BuildState.Building => ColorWarning,
                BuildState.Success => ColorSuccess,
                BuildState.Failed => ColorError,
                _ => ColorDim
            };

            string stateLabel = _state switch
            {
                BuildState.Idle => "●  Idle",
                BuildState.Building => "●  Building...",
                BuildState.Success => "●  Success",
                BuildState.Failed => "●  Failed",
                _ => "●  Idle"
            };

            ImGui.TextColored(stateColor, stateLabel);
        }

        private void DrawProgressSection()
        {
            float elapsed = _buildTimer.IsRunning ? (float)_buildTimer.Elapsed.TotalSeconds : 0f;

            Vector2 progressSize = new(ImGui.GetContentRegionAvail().X, 18f);
            ImGui.ProgressBar(_progress, progressSize, "");

            ImGui.SameLine(10f);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 18f);

            if (_state == BuildState.Building)
                ImGui.TextColored(ColorInfo, $"{_currentStep}  ({_progress * 100f:F0}%)");
            else if (_state == BuildState.Success)
                ImGui.TextColored(ColorSuccess, $"Build succeeded in {_buildTimer.Elapsed.TotalSeconds:F2}s");
            else if (_state == BuildState.Failed)
                ImGui.TextColored(ColorError, $"Build failed after {_buildTimer.Elapsed.TotalSeconds:F2}s");
            else
                ImGui.TextColored(ColorDim, "Ready");

            ImGui.Spacing();

            ImGui.TextColored(ColorWarning, $"⚠  {_warningCount}");
            ImGui.SameLine();
            ImGui.TextColored(ColorError, $"✕  {_errorCount}");

            if (_state == BuildState.Building)
            {
                ImGui.SameLine();
                ImGui.TextColored(ColorDim, $"  {elapsed:F1}s");
            }
        }

        private void DrawLog()
        {
            ImGui.Checkbox("Auto-scroll", ref _autoScroll);

            float logHeight = ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing();
            ImGui.BeginChild("##compiler_log", new Vector2(0, logHeight));

            foreach (var entry in _log)
            {
                Vector4 color = entry.Level switch
                {
                    LogLevel.Warning => ColorWarning,
                    LogLevel.Error => ColorError,
                    LogLevel.Success => ColorSuccess,
                    _ => ColorInfo
                };

                ImGui.TextColored(ColorDim, entry.Timestamp);
                ImGui.SameLine();
                ImGui.TextColored(color, entry.Message);
            }

            if (_autoScroll && _scrollToBottom)
            {
                ImGui.SetScrollHereY(1f);
                _scrollToBottom = false;
            }

            ImGui.EndChild();
        }

        private void StartBuild()
        {
            _cts = new CancellationTokenSource();
            _state = BuildState.Building;
            _progress = 0f;
            _warningCount = 0;
            _errorCount = 0;
            _log.Clear();
            _buildTimer.Restart();

            AppendLog("Build started.", LogLevel.Info);

            Task.Run(() => RunBuildPipeline(_cts.Token), _cts.Token);
        }

        private void CancelBuild()
        {
            _cts?.Cancel();
            _state = BuildState.Failed;
            _buildTimer.Stop();
            AppendLog("Build cancelled by user.", LogLevel.Warning);
        }

        private void ClearLog()
        {
            _log.Clear();
            _warningCount = 0;
            _errorCount = 0;
        }

        private void RunBuildPipeline(CancellationToken token)
        {
            try
            {
                var steps = new (string Label, Action Step)[]
                {
                    ("Scanning assets...",       () => Thread.Sleep(300)),
                    ("Resolving dependencies...", () => Thread.Sleep(400)),
                    ("Compiling shaders...",      () => Thread.Sleep(500)),
                    ("Packaging resources...",    () => Thread.Sleep(350)),
                    ("Linking output...",         () => Thread.Sleep(250)),
                    ("Finalizing...",             () => Thread.Sleep(200)),
                };

                for (int i = 0; i < steps.Length; i++)
                {
                    token.ThrowIfCancellationRequested();

                    var (label, action) = steps[i];
                    _currentStep = label;
                    AppendLog(label, LogLevel.Info);

                    action();

                    _progress = (i + 1f) / steps.Length;
                }

                _state = BuildState.Success;
                _buildTimer.Stop();
                AppendLog($"Build succeeded in {_buildTimer.Elapsed.TotalSeconds:F2}s", LogLevel.Success);
                EngineEditor.LogMessage("[Compiler] Build succeeded.");
            }
            catch (OperationCanceledException)
            {
                // Handled by CancelBuild
            }
            catch (Exception ex)
            {
                _errorCount++;
                _state = BuildState.Failed;
                _buildTimer.Stop();
                AppendLog($"[ERROR] {ex.Message}", LogLevel.Error);
                EngineEditor.LogMessage($"[Compiler] Build failed: {ex.Message}");
            }
        }

        private void AppendLog(string message, LogLevel level)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss");
            _log.Add(new LogEntry(message, level, $"[{ts}]"));
            if (level == LogLevel.Warning) _warningCount++;
            if (level == LogLevel.Error) _errorCount++;
            _scrollToBottom = true;
        }
    }
}