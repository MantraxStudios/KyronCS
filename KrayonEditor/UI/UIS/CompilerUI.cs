using ImGuiNET;
using KrayonCore.Core.Attributes;
using KrayonEditor.Compiler;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace KrayonEditor.UI
{
    public class CompilerUI : UIBehaviour
    {
        private enum BuildState { Idle, Building, Success, Failed }

        private readonly record struct LogEntry(string Message, BuildLogEventArgs.LogLevel Level, string Timestamp);

        private BuildState _state = BuildState.Idle;
        private BuildPlatform _platform = BuildPlatform.Windows;

        private float _progress = 0f;
        private string _currentStep = string.Empty;
        private int _warningCount = 0;
        private int _errorCount = 0;
        private bool _autoScroll = true;
        private bool _scrollBottom = false;

        private double _finalElapsedSeconds = 0.0;
        private double _liveElapsedSeconds = 0.0;

        private CancellationTokenSource _cts;
        private readonly BuildPipeline _buildPipeline = new();
        private readonly List<LogEntry> _log = new();
        private readonly object _logLock = new();

        private const float Padding = 16f;
        private const float ItemSpacing = 8f;
        private const float SectionSpacing = 12f;

        public CompilerUI()
        {
            _buildPipeline.ProgressChanged += OnBuildProgressChanged;
            _buildPipeline.LogAdded += OnBuildLogAdded;
            _buildPipeline.BuildCompleted += OnBuildCompleted;
        }

        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            if (_state == BuildState.Building)
            {
                _liveElapsedSeconds += ImGui.GetIO().DeltaTime;
            }

            ImGui.SetNextWindowSize(new Vector2(800, 600), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(600, 400), new Vector2(1400, 1000));

            if (ImGui.Begin("Krayon Compiler", ref _isVisible))
            {
                DrawMainContent();
            }
            ImGui.End();
        }

        private void DrawMainContent()
        {
            var avail = ImGui.GetContentRegionAvail();
            float leftPanelWidth = avail.X * 0.35f;
            float rightPanelWidth = avail.X - leftPanelWidth - ItemSpacing;

            if (ImGui.BeginChild("LeftPanel", new Vector2(leftPanelWidth, 0)))
            {
                DrawControlPanel();
            }
            ImGui.EndChild();

            ImGui.SameLine();

            if (ImGui.BeginChild("RightPanel", new Vector2(rightPanelWidth, 0)))
            {
                DrawLogPanel();
            }
            ImGui.EndChild();
        }

        private void DrawControlPanel()
        {
            DrawStatusSection();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            DrawBuildConfiguration();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            DrawBuildActions();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            DrawProgressSection();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            DrawStatistics();
        }

        private void DrawStatusSection()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12, 8));

            string statusLabel = "Status";
            ImGui.Text(statusLabel);
            ImGui.SameLine();

            float availWidth = ImGui.GetContentRegionAvail().X;
            string statusText = _state switch
            {
                BuildState.Building => "Building",
                BuildState.Success => "Success",
                BuildState.Failed => "Failed",
                _ => "Idle"
            };

            Vector4 statusColor = GetStateColor(_state);

            float statusWidth = ImGui.CalcTextSize(statusText).X + 24f;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + availWidth - statusWidth);

            ImGui.PushStyleColor(ImGuiCol.Button, statusColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, statusColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, statusColor);
            ImGui.Button(statusText, new Vector2(statusWidth, 0));
            ImGui.PopStyleColor(3);

            ImGui.PopStyleVar();
        }

        private void DrawBuildConfiguration()
        {
            ImGui.Text("Platform");
            ImGui.Spacing();

            bool isBuilding = _state == BuildState.Building;
            if (isBuilding) ImGui.BeginDisabled();

            float buttonWidth = (ImGui.GetContentRegionAvail().X - ItemSpacing) * 0.5f;

            if (DrawPlatformToggle("Windows", BuildPlatform.Windows, buttonWidth))
                _platform = BuildPlatform.Windows;

            ImGui.SameLine();

            if (DrawPlatformToggle("Android", BuildPlatform.Android, buttonWidth))
                _platform = BuildPlatform.Android;

            if (isBuilding) ImGui.EndDisabled();
        }

        private bool DrawPlatformToggle(string label, BuildPlatform platform, float width)
        {
            bool isSelected = _platform == platform;

            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
            }

            bool clicked = ImGui.Button(label, new Vector2(width, 32));

            if (isSelected)
            {
                ImGui.PopStyleColor();
            }

            return clicked;
        }

        private void DrawBuildActions()
        {
            ImGui.Text("Actions");
            ImGui.Spacing();

            bool isBuilding = _state == BuildState.Building;
            float buttonHeight = 36f;

            if (isBuilding) ImGui.BeginDisabled();

            Vector4 buildColor = new Vector4(0.2f, 0.6f, 0.3f, 1.0f);
            ImGui.PushStyleColor(ImGuiCol.Button, buildColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.25f, 0.7f, 0.35f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.15f, 0.5f, 0.25f, 1.0f));

            if (ImGui.Button("Build", new Vector2(-1, buttonHeight)))
                StartBuild();

            ImGui.PopStyleColor(3);
            if (isBuilding) ImGui.EndDisabled();

            ImGui.Spacing();

            if (!isBuilding) ImGui.BeginDisabled();

            Vector4 cancelColor = new Vector4(0.7f, 0.2f, 0.2f, 1.0f);
            ImGui.PushStyleColor(ImGuiCol.Button, cancelColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.15f, 0.15f, 1.0f));

            if (ImGui.Button("Cancel", new Vector2(-1, buttonHeight)))
                CancelBuild();

            ImGui.PopStyleColor(3);
            if (!isBuilding) ImGui.EndDisabled();

            ImGui.Spacing();

            if (ImGui.Button("Clear Log", new Vector2(-1, buttonHeight)))
                ClearLog();
        }

        private void DrawProgressSection()
        {
            ImGui.Text("Progress");
            ImGui.Spacing();

            string progressText = _state switch
            {
                BuildState.Building => _currentStep,
                BuildState.Success => $"Completed in {_finalElapsedSeconds:F2}s",
                BuildState.Failed => $"Failed after {_finalElapsedSeconds:F2}s",
                _ => "Ready"
            };

            ImGui.TextWrapped(progressText);
            ImGui.Spacing();

            ImGui.ProgressBar(_progress, new Vector2(-1, 24), $"{_progress * 100f:F0}%%");

            if (_state == BuildState.Building)
            {
                ImGui.Spacing();
                string elapsed = $"Elapsed: {_liveElapsedSeconds:F1}s";
                ImGui.TextDisabled(elapsed);
            }
        }

        private void DrawStatistics()
        {
            ImGui.Text("Statistics");
            ImGui.Spacing();

            ImGui.Columns(2, "stats", false);

            ImGui.Text("Platform:");
            ImGui.NextColumn();
            ImGui.Text(_platform.ToString());
            ImGui.NextColumn();

            ImGui.Text("Warnings:");
            ImGui.NextColumn();
            if (_warningCount > 0)
            {
                Vector4 warningColor = new Vector4(0.9f, 0.7f, 0.2f, 1.0f);
                ImGui.TextColored(warningColor, _warningCount.ToString());
            }
            else
            {
                ImGui.TextDisabled(_warningCount.ToString());
            }
            ImGui.NextColumn();

            ImGui.Text("Errors:");
            ImGui.NextColumn();
            if (_errorCount > 0)
            {
                Vector4 errorColor = new Vector4(0.9f, 0.3f, 0.2f, 1.0f);
                ImGui.TextColored(errorColor, _errorCount.ToString());
            }
            else
            {
                ImGui.TextDisabled(_errorCount.ToString());
            }
            ImGui.NextColumn();

            ImGui.Columns(1);
        }

        private void DrawLogPanel()
        {
            float headerHeight = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y * 2;

            if (ImGui.BeginChild("LogHeader", new Vector2(0, headerHeight)))
            {
                ImGui.Text("Build Output");
                ImGui.SameLine();

                float checkboxWidth = 120f;
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - checkboxWidth + ImGui.GetCursorPosX());
                ImGui.Checkbox("Auto-scroll", ref _autoScroll);
            }
            ImGui.EndChild();

            ImGui.Separator();

            if (ImGui.BeginChild("LogContent", new Vector2(0, 0)))
            {
                List<LogEntry> snapshot;
                lock (_logLock) { snapshot = new List<LogEntry>(_log); }

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4));

                foreach (var entry in snapshot)
                {
                    Vector4 color = GetLogLevelColor(entry.Level);

                    string prefix = entry.Level switch
                    {
                        BuildLogEventArgs.LogLevel.Warning => "[WARN]",
                        BuildLogEventArgs.LogLevel.Error => "[ERROR]",
                        BuildLogEventArgs.LogLevel.Success => "[OK]",
                        _ => "[INFO]"
                    };

                    ImGui.TextColored(color, $"{entry.Timestamp} {prefix}");
                    ImGui.SameLine();
                    ImGui.TextWrapped(entry.Message);
                }

                ImGui.PopStyleVar();

                if (_autoScroll && _scrollBottom)
                {
                    ImGui.SetScrollHereY(1f);
                    _scrollBottom = false;
                }
            }
            ImGui.EndChild();
        }

        private Vector4 GetStateColor(BuildState state)
        {
            return state switch
            {
                BuildState.Building => new Vector4(0.9f, 0.7f, 0.2f, 1.0f),
                BuildState.Success => new Vector4(0.2f, 0.8f, 0.3f, 1.0f),
                BuildState.Failed => new Vector4(0.9f, 0.3f, 0.2f, 1.0f),
                _ => ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]
            };
        }

        private Vector4 GetLogLevelColor(BuildLogEventArgs.LogLevel level)
        {
            return level switch
            {
                BuildLogEventArgs.LogLevel.Warning => new Vector4(0.9f, 0.7f, 0.2f, 1.0f),
                BuildLogEventArgs.LogLevel.Error => new Vector4(0.9f, 0.3f, 0.2f, 1.0f),
                BuildLogEventArgs.LogLevel.Success => new Vector4(0.2f, 0.8f, 0.3f, 1.0f),
                _ => ImGui.GetStyle().Colors[(int)ImGuiCol.Text]
            };
        }

        private void StartBuild()
        {
            _cts = new CancellationTokenSource();
            _state = BuildState.Building;
            _progress = 0f;
            _warningCount = 0;
            _errorCount = 0;
            _finalElapsedSeconds = 0.0;
            _liveElapsedSeconds = 0.0;

            lock (_logLock) { _log.Clear(); }

            _ = _buildPipeline.BuildAsync(_platform, _cts.Token);
        }

        private void CancelBuild()
        {
            _cts?.Cancel();
        }

        private void ClearLog()
        {
            lock (_logLock) { _log.Clear(); }
            _warningCount = 0;
            _errorCount = 0;
        }

        private void OnBuildProgressChanged(object sender, BuildProgressEventArgs e)
        {
            _progress = e.Progress;
            _currentStep = e.CurrentStep;
        }

        private void OnBuildLogAdded(object sender, BuildLogEventArgs e)
        {
            string timestamp = e.Timestamp.ToString("HH:mm:ss");

            lock (_logLock)
            {
                _log.Add(new LogEntry(e.Message, e.Level, timestamp));
            }

            _scrollBottom = true;

            if (e.Level == BuildLogEventArgs.LogLevel.Warning)
                _warningCount++;
            else if (e.Level == BuildLogEventArgs.LogLevel.Error)
                _errorCount++;
        }

        private void OnBuildCompleted(object sender, BuildInfo buildInfo)
        {
            _finalElapsedSeconds = buildInfo.ElapsedSeconds;
            _warningCount = buildInfo.WarningCount;
            _errorCount = buildInfo.ErrorCount;

            _state = buildInfo.Result switch
            {
                BuildResult.Success => BuildState.Success,
                BuildResult.Failed => BuildState.Failed,
                BuildResult.Cancelled => BuildState.Failed,
                _ => BuildState.Idle
            };

            _progress = buildInfo.Result == BuildResult.Success ? 1f : _progress;
        }
    }
}