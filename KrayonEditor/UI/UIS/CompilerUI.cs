using ImGuiNET;
using KrayonCompiler;
using KrayonCore;
using KrayonCore.Core.Attributes;
using Newtonsoft.Json.Linq;
using OpenTK.Graphics.ES11;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KrayonEditor.UI
{
    public class CompilerUI : UIBehaviour
    {
        private enum BuildState { Idle, Building, Success, Failed }
        private enum BuildPlatform { Windows, Android }
        private enum LogLevel { Info, Warning, Error, Success }

        private readonly record struct LogEntry(string Message, LogLevel Level, string Timestamp);

        private BuildState _state = BuildState.Idle;
        private BuildPlatform _platform = BuildPlatform.Windows;

        private float _progress = 0f;
        private string _currentStep = string.Empty;
        private int _warningCount = 0;
        private int _errorCount = 0;
        private bool _autoScroll = true;
        private bool _scrollBottom = false;

        private CancellationTokenSource _cts;
        private readonly Stopwatch _timer = new();
        private readonly List<LogEntry> _log = new();
        private readonly object _logLock = new();

        private static readonly Vector4 Bg0 = new(0.09f, 0.09f, 0.10f, 1f);
        private static readonly Vector4 Bg1 = new(0.12f, 0.12f, 0.13f, 1f);
        private static readonly Vector4 Bg2 = new(0.16f, 0.16f, 0.18f, 1f);
        private static readonly Vector4 Accent = new(0.20f, 0.55f, 1.00f, 1f);
        private static readonly Vector4 AccentHover = new(0.30f, 0.65f, 1.00f, 1f);
        private static readonly Vector4 AccentPress = new(0.15f, 0.45f, 0.90f, 1f);
        private static readonly Vector4 Danger = new(0.85f, 0.25f, 0.25f, 1f);
        private static readonly Vector4 DangerHover = new(0.95f, 0.35f, 0.35f, 1f);
        private static readonly Vector4 DangerPress = new(0.70f, 0.18f, 0.18f, 1f);
        private static readonly Vector4 Green = new(0.20f, 0.75f, 0.40f, 1f);
        private static readonly Vector4 Yellow = new(0.90f, 0.75f, 0.20f, 1f);
        private static readonly Vector4 TextMuted = new(0.45f, 0.45f, 0.50f, 1f);
        private static readonly Vector4 TextNormal = new(0.80f, 0.80f, 0.82f, 1f);
        private static readonly Vector4 Separator = new(0.20f, 0.20f, 0.22f, 1f);

        // ─── layout constants ────────────────────────────────────────────────
        private const float Pad = 12f;
        private const float Rounding = 6f;
        private const float SectionGap = 6f;

        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            PushWindowStyle();
            ImGui.SetNextWindowSize(new Vector2(700, 520), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(520, 380), new Vector2(1280, 960));
            ImGui.Begin("  Krayon Compiler", ref _isVisible);
            PopWindowStyle();

            DrawHeader();
            ImGui.Dummy(new Vector2(0, SectionGap));
            DrawToolbar();          // platform + actions in one row
            ImGui.Dummy(new Vector2(0, SectionGap));
            DrawProgressSection();
            ImGui.Dummy(new Vector2(0, SectionGap));
            DrawLogPanel();

            ImGui.End();
        }

        // ─── Header ──────────────────────────────────────────────────────────
        private void DrawHeader()
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Bg1);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Rounding);
            ImGui.BeginChild("##header", new Vector2(0, 46));

            float winW = ImGui.GetWindowWidth();
            float textH = ImGui.GetTextLineHeight();
            float centerY = (46f - textH) * 0.5f;

            // Title
            ImGui.SetCursorPos(new Vector2(Pad, centerY));
            ImGui.TextColored(TextNormal, "Krayon Compiler");

            // State badge
            string stateText = _state switch
            {
                BuildState.Building => "● Building",
                BuildState.Success => "● Success",
                BuildState.Failed => "● Failed",
                _ => "● Idle"
            };
            Vector4 stateCol = _state switch
            {
                BuildState.Building => Yellow,
                BuildState.Success => Green,
                BuildState.Failed => Danger,
                _ => TextMuted
            };

            float badgeW = ImGui.CalcTextSize(stateText).X;
            ImGui.SetCursorPos(new Vector2(winW - badgeW - Pad, centerY));
            ImGui.TextColored(stateCol, stateText);

            ImGui.EndChild();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }

        // ─── Toolbar  (platform selector + build buttons side by side) ───────
        private void DrawToolbar()
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Bg1);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Rounding);
            ImGui.BeginChild("##toolbar", new Vector2(0, 46));

            bool isBuilding = _state == BuildState.Building;
            float childH = 46f;
            float btnH = 26f;
            float btnY = (childH - btnH) * 0.5f;

            ImGui.SetCursorPos(new Vector2(Pad, btnY));

            // ── Platform toggle ──────────────────────────────────────────────
            if (isBuilding) ImGui.BeginDisabled();
            DrawPlatformButton(BuildPlatform.Windows, "  Windows  ");
            ImGui.SameLine(0, 4);
            DrawPlatformButton(BuildPlatform.Android, "  Android  ");
            if (isBuilding) ImGui.EndDisabled();

            // ── Divider ──────────────────────────────────────────────────────
            ImGui.SameLine(0, 14);
            float divX = ImGui.GetCursorPosX();
            ImGui.SetCursorPosY(8f);
            ImGui.PushStyleColor(ImGuiCol.Separator, Separator);
            ImGui.PopStyleColor();
            ImGui.SameLine(divX + 14f, 0);
            ImGui.SetCursorPosY(btnY);

            // ── Action buttons ───────────────────────────────────────────────
            if (isBuilding) ImGui.BeginDisabled();
            PushButtonStyle(Accent, AccentHover, AccentPress);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Rounding);
            if (ImGui.Button("  Build  ", new Vector2(0, btnH))) StartBuild();
            ImGui.PopStyleVar();
            PopButtonStyle();
            if (isBuilding) ImGui.EndDisabled();

            ImGui.SameLine(0, 4);

            if (!isBuilding) ImGui.BeginDisabled();
            PushButtonStyle(Danger, DangerHover, DangerPress);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Rounding);
            if (ImGui.Button("  Cancel  ", new Vector2(0, btnH))) CancelBuild();
            ImGui.PopStyleVar();
            PopButtonStyle();
            if (!isBuilding) ImGui.EndDisabled();

            ImGui.SameLine(0, 4);

            ImGui.PushStyleColor(ImGuiCol.Button, Bg2);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.22f, 0.22f, 0.25f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.18f, 0.18f, 0.20f, 1f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Rounding);
            if (ImGui.Button("  Clear Log  ", new Vector2(0, btnH))) ClearLog();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);

            // ── Elapsed timer (right-aligned) ────────────────────────────────
            if (isBuilding)
            {
                string elapsed = $"{_timer.Elapsed.TotalSeconds:F1}s";
                float elapsedW = ImGui.CalcTextSize(elapsed).X;
                float winW2 = ImGui.GetWindowWidth();
                ImGui.SetCursorPos(new Vector2(winW2 - elapsedW - Pad, btnY + 4f));
                ImGui.TextColored(TextMuted, elapsed);
            }

            ImGui.EndChild();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }

        private void DrawPlatformButton(BuildPlatform platform, string label)
        {
            bool active = _platform == platform;

            ImGui.PushStyleColor(ImGuiCol.Button, active ? Accent : Bg2);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, active ? AccentHover : new Vector4(0.22f, 0.22f, 0.25f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, active ? AccentPress : new Vector4(0.18f, 0.18f, 0.20f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, active ? Vector4.One : TextNormal);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Rounding);

            if (ImGui.Button(label, new Vector2(0, 26f)))
                _platform = platform;

            ImGui.PopStyleVar();
            ImGui.PopStyleColor(4);
        }

        // ─── Progress ────────────────────────────────────────────────────────
        private void DrawProgressSection()
        {
            // ── Step label + bar ─────────────────────────────────────────────
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Bg1);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Rounding);
            ImGui.BeginChild("##progress_area", new Vector2(0, 60));

            string stepText = _state switch
            {
                BuildState.Building => _currentStep,
                BuildState.Success => $"Build succeeded in {_timer.Elapsed.TotalSeconds:F2}s",
                BuildState.Failed => $"Build failed after {_timer.Elapsed.TotalSeconds:F2}s",
                _ => "Ready to build"
            };
            Vector4 stepColor = _state switch
            {
                BuildState.Success => Green,
                BuildState.Failed => Danger,
                _ => TextNormal
            };

            ImGui.SetCursorPos(new Vector2(Pad, 10f));
            ImGui.TextColored(stepColor, stepText);

            ImGui.SetCursorPos(new Vector2(Pad, 32f));
            float barWidth = ImGui.GetContentRegionAvail().X - Pad;

            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Accent);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Bg2);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Rounding);
            ImGui.ProgressBar(_progress, new Vector2(barWidth, 16f), $"{_progress * 100f:F0}%");
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(2);

            ImGui.EndChild();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();

            ImGui.Dummy(new Vector2(0, 2f));

            // ── Stats row ────────────────────────────────────────────────────
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Bg1);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Rounding);
            ImGui.BeginChild("##build_stats", new Vector2(0, 30));

            float statH = ImGui.GetTextLineHeight();
            float statY = (30f - statH) * 0.5f;
            ImGui.SetCursorPos(new Vector2(Pad, statY));

            ImGui.TextColored(TextMuted, "Warnings");
            ImGui.SameLine(0, 5);
            ImGui.TextColored(_warningCount > 0 ? Yellow : TextMuted, $"{_warningCount}");

            ImGui.SameLine(0, 18);
            ImGui.TextColored(Separator, "|");
            ImGui.SameLine(0, 18);

            ImGui.TextColored(TextMuted, "Errors");
            ImGui.SameLine(0, 5);
            ImGui.TextColored(_errorCount > 0 ? Danger : TextMuted, $"{_errorCount}");

            ImGui.SameLine(0, 18);
            ImGui.TextColored(Separator, "|");
            ImGui.SameLine(0, 18);

            ImGui.TextColored(TextMuted, "Platform");
            ImGui.SameLine(0, 5);
            ImGui.TextColored(TextNormal, _platform.ToString());

            ImGui.EndChild();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }

        // ─── Log Panel ───────────────────────────────────────────────────────
        private void DrawLogPanel()
        {
            // Header bar
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Bg1);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Rounding);
            ImGui.BeginChild("##log_header", new Vector2(0, 30));

            float winW = ImGui.GetWindowWidth();
            float frameH = ImGui.GetFrameHeight();
            float textH = ImGui.GetTextLineHeight();
            float checkW = ImGui.CalcTextSize("Auto-scroll").X + frameH + ImGui.GetStyle().ItemInnerSpacing.X;

            ImGui.SetCursorPos(new Vector2(Pad, (30f - textH) * 0.5f));
            ImGui.TextColored(TextMuted, "Output");

            ImGui.SetCursorPos(new Vector2(winW - checkW - Pad, (30f - frameH) * 0.5f));
            ImGui.Checkbox("Auto-scroll", ref _autoScroll);

            ImGui.EndChild();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();

            ImGui.Dummy(new Vector2(0, 2f));

            // Body
            float logHeight = ImGui.GetContentRegionAvail().Y - 4f;

            ImGui.PushStyleColor(ImGuiCol.ChildBg, Bg0);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Rounding);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Pad, 8f));
            ImGui.BeginChild("##log_body", new Vector2(0, logHeight));

            List<LogEntry> snapshot;
            lock (_logLock) { snapshot = new List<LogEntry>(_log); }

            foreach (var entry in snapshot)
            {
                Vector4 col = entry.Level switch
                {
                    LogLevel.Warning => Yellow,
                    LogLevel.Error => Danger,
                    LogLevel.Success => Green,
                    _ => TextNormal
                };

                ImGui.TextColored(TextMuted, entry.Timestamp);
                ImGui.SameLine(0, 8);
                ImGui.TextColored(col, entry.Message);
            }

            if (_autoScroll && _scrollBottom)
            {
                ImGui.SetScrollHereY(1f);
                _scrollBottom = false;
            }

            ImGui.EndChild();
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor();
        }

        // ─── Build logic (sin cambios) ────────────────────────────────────────
        private void StartBuild()
        {
            if (!Directory.Exists(AssetManager.CompilerPath))
                Directory.CreateDirectory(AssetManager.CompilerPath);

            _cts = new CancellationTokenSource();
            _state = BuildState.Building;
            _progress = 0f;
            _warningCount = 0;
            _errorCount = 0;
            lock (_logLock) { _log.Clear(); }
            _timer.Restart();

            AppendLog($"Starting build for platform: {_platform}", LogLevel.Info);
            Task.Run(() => RunBuildPipeline(_cts.Token), _cts.Token);
        }

        private void CancelBuild()
        {
            _cts?.Cancel();
            _state = BuildState.Failed;
            _timer.Stop();
            AppendLog("Build cancelled by user.", LogLevel.Warning);
        }

        private void ClearLog()
        {
            lock (_logLock) { _log.Clear(); }
            _warningCount = 0;
            _errorCount = 0;
        }

        private void RunBuildPipeline(CancellationToken token)
        {
            try
            {
                var steps = _platform == BuildPlatform.Android
                    ? new (string Label, int Ms)[]
                      {
                          ("Scanning assets",          300),
                          ("Resolving dependencies",   350),
                          ("Compiling shaders (GLSL)", 500),
                          ("Packaging APK resources",  400),
                          ("Signing package",          250),
                          ("Finalizing",               200),
                      }
                    : new (string Label, int Ms)[]
                      {
                          ("Scanning assets",          300),
                          ("Resolving dependencies",   350),
                          ("Compiling shaders (HLSL)", 500),
                          ("Packaging resources",      400),
                          ("Linking executable",       250),
                          ("Finalizing",               200),
                      };

                for (int i = 0; i < steps.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    _currentStep = steps[i].Label;
                    AppendLog(steps[i].Label, LogLevel.Info);
                    Thread.Sleep(steps[i].Ms);
                    _progress = (i + 1f) / steps.Length;
                }

                _state = BuildState.Success;
                _timer.Stop();

                string jsonData = File.ReadAllText($"{AssetManager.DataBase}");
                JObject dataJson = JObject.Parse(jsonData);
                JArray assets = (JArray)dataJson["Assets"];
                var assetsPak = new Dictionary<string, string>();

                foreach (JToken asset in assets)
                {
                    string? path = asset["Path"]?.ToString();
                    if (path == null) { AppendLog("Asset sin campo 'Path'", LogLevel.Error); continue; }

                    string fullPath = AssetManager.BasePath + path;
                    if (File.Exists(fullPath))
                        assetsPak.Add(asset["Guid"]?.ToString(), fullPath);
                    else
                        AppendLog($"Error on try compile file: {fullPath}", LogLevel.Error);

                    AppendLog($"Working On Asset: {path}", LogLevel.Info);
                }

                AppendLog("Work On Game Assets Pak (Please Wait...)", LogLevel.Success);

                if (File.Exists($"{AssetManager.CompilerPath}Game.pak"))
                {
                    AppendLog("Replacing previous Game.Pak (Please Wait...)", LogLevel.Warning);
                    File.Delete($"{AssetManager.CompilerPath}Game.pak");
                }
                else
                {
                    AppendLog("Generating new Game.pak (Please Wait...)", LogLevel.Success);
                }

                KRCompiler.Build($"{AssetManager.CompilerPath}Game.pak", assetsPak);
                AppendLog($"Build succeeded in {_timer.Elapsed.TotalSeconds:F2}s", LogLevel.Success);
                EngineEditor.LogMessage("[Compiler] Build succeeded.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _errorCount++;
                _state = BuildState.Failed;
                _timer.Stop();
                AppendLog($"Error: {ex.Message}", LogLevel.Error);
                EngineEditor.LogMessage($"[Compiler] Build failed: {ex.Message}");
            }
        }

        private void AppendLog(string message, LogLevel level)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss");
            lock (_logLock) { _log.Add(new LogEntry(message, level, $"[{ts}]")); }
            _scrollBottom = true;
        }

        // ─── Style helpers ────────────────────────────────────────────────────
        private static void PushWindowStyle()
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, Bg0);
            ImGui.PushStyleColor(ImGuiCol.TitleBg, Bg0);
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Bg1);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 12));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Rounding);
        }

        private static void PopWindowStyle()
        {
            ImGui.PopStyleColor(3);
            ImGui.PopStyleVar(3);
        }

        private static void PushButtonStyle(Vector4 bg, Vector4 hover, Vector4 press)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, bg);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, press);
        }

        private static void PopButtonStyle() => ImGui.PopStyleColor(3);
    }
}