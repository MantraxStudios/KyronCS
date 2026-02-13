using Assimp;
using ImGuiNET;
using KrayonCompiler;
using KrayonCore;
using KrayonCore.Core.Attributes;
using KrayonCore.Utilities;
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

        // Stores the final elapsed time once the build finishes (success or fail/cancel)
        private double _finalElapsedSeconds = 0.0;

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
            DrawToolbar();
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

            ImGui.SetCursorPos(new Vector2(Pad, centerY));
            ImGui.TextColored(TextNormal, "Krayon Compiler");

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

        // ─── Toolbar ─────────────────────────────────────────────────────────
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

            // ── Elapsed timer right-aligned ──────────────────────────────────
            // While building: live counter. After finish: frozen total time.
            string elapsed = _state switch
            {
                BuildState.Building => $"{_timer.Elapsed.TotalSeconds:F1}s",
                BuildState.Success => $"{_finalElapsedSeconds:F1}s",
                BuildState.Failed => $"{_finalElapsedSeconds:F1}s",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(elapsed))
            {
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
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Bg1);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Rounding);
            ImGui.BeginChild("##progress_area", new Vector2(0, 60));

            string stepText = _state switch
            {
                BuildState.Building => _currentStep,
                BuildState.Success => $"Build succeeded in {_finalElapsedSeconds:F2}s",
                BuildState.Failed => $"Build failed after {_finalElapsedSeconds:F2}s",
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

            float bodyWidth = ImGui.GetContentRegionAvail().X;

            foreach (var entry in snapshot)
            {
                Vector4 col = entry.Level switch
                {
                    LogLevel.Warning => Yellow,
                    LogLevel.Error => Danger,
                    LogLevel.Success => Green,
                    _ => TextNormal
                };

                // Mensaje a la izquierda, timestamp a la derecha en la misma línea
                float tsWidth = ImGui.CalcTextSize(entry.Timestamp).X;
                float cursorY = ImGui.GetCursorPosY();

                ImGui.TextColored(col, entry.Message);

                // Sobreimprimir timestamp alineado al borde derecho de la misma fila
                ImGui.SetCursorPos(new Vector2(bodyWidth - tsWidth, cursorY));
                ImGui.TextColored(TextMuted, entry.Timestamp);
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

        // ─── Build logic ─────────────────────────────────────────────────────
        private void StartBuild()
        {
            if (!Directory.Exists(AssetManager.CompilerPath))
                Directory.CreateDirectory(AssetManager.CompilerPath);

            _cts = new CancellationTokenSource();
            _state = BuildState.Building;
            _progress = 0f;
            _warningCount = 0;
            _errorCount = 0;
            _finalElapsedSeconds = 0.0;
            lock (_logLock) { _log.Clear(); }
            _timer.Restart();

            AppendLog($"Starting build for platform: {_platform}", LogLevel.Info);
            Task.Run(() => RunBuildPipeline(_cts.Token), _cts.Token);
        }

        private void CancelBuild()
        {
            _cts?.Cancel();
            _finalElapsedSeconds = _timer.Elapsed.TotalSeconds;
            _timer.Stop();
            _state = BuildState.Failed;
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
                // ── Definir pasos de compilación ─────────────────────────────
                var compileSteps = _platform == BuildPlatform.Android
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

                // ── Pre-cargar assets para calcular total de pasos ────────────
                // Se hace ANTES de avanzar progreso para que la barra sea exacta.
                string jsonData = File.ReadAllText($"{AssetManager.DataBase}");
                JObject dataJson = JObject.Parse(jsonData);
                JArray assets = (JArray)dataJson["Assets"];
                int assetCount = assets.Count;

                // Desglose total de pasos:
                //   compileSteps.Length  → fase 1: compilación
                //   assetCount           → fase 2: un paso por asset
                //   1                    → fase 3: preparar pak
                //   1                    → fase 4: copiar ejecutable
                //   1                    → fase 5: construir pak
                int totalSteps = compileSteps.Length + assetCount + 3;
                int completedSteps = 0;

                void Advance()
                {
                    completedSteps++;
                    _progress = Math.Min((float)completedSteps / totalSteps, 1f);
                }

                // ── Fase 1: pasos de compilación ─────────────────────────────
                for (int i = 0; i < compileSteps.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    _currentStep = compileSteps[i].Label;
                    AppendLog(compileSteps[i].Label, LogLevel.Info);
                    Thread.Sleep(compileSteps[i].Ms);
                    Advance();
                }

                // ── Fase 2: procesamiento de assets ──────────────────────────
                var assetsPak = new Dictionary<string, string>();

                assetsPak.Add("Engine.AssetsData", AssetManager.DataBase);
                assetsPak.Add("Engine.VFX", AssetManager.VFXPath);
                assetsPak.Add("Engine.Materials", AssetManager.MaterialsPath);
                assetsPak.Add("Engine.Client.KrayonClient", $"{AssetManager.ClientDLLPath}/KrayonClient.dll");

                foreach (JToken asset in assets)
                {
                    token.ThrowIfCancellationRequested();

                    string? path = asset["Path"]?.ToString();
                    if (path == null)
                    {
                        AppendLog("Asset sin campo 'Path'", LogLevel.Error);
                        Advance();
                        continue;
                    }

                    string fullPath = AssetManager.BasePath + path;

                    if (File.Exists(fullPath))
                    {
                        string ext = Path.GetExtension(fullPath);

                        if (ext.Equals(".scene", StringComparison.OrdinalIgnoreCase))
                        {
                            string sceneName = Path.GetFileNameWithoutExtension(fullPath);
                            assetsPak.Add($"Scene.{sceneName}", fullPath);
                            AppendLog($"Working On Scene: {sceneName}", LogLevel.Info);
                        }
                        else
                        {
                            assetsPak.Add(asset["Guid"]?.ToString(), fullPath);
                            AppendLog($"Working On Asset: {path}", LogLevel.Info);
                        }
                    }
                    else
                    {
                        AppendLog($"Error on try compile file: {fullPath}", LogLevel.Error);
                    }

                    _currentStep = $"Processing asset: {path}";
                    Advance();
                }



                // ── Fase 3: preparar pak ─────────────────────────────────────
                token.ThrowIfCancellationRequested();
                _currentStep = "Preparing Game.pak...";
                AppendLog("Work On Game Assets Pak (Please Wait...)", LogLevel.Info);

                if (File.Exists($"{AssetManager.CompilerPath}Game.pak"))
                {
                    AppendLog("Replacing previous Game.pak (Please Wait...)", LogLevel.Warning);
                    File.Delete($"{AssetManager.CompilerPath}Game.pak");
                }
                else
                {
                    AppendLog("Generating new Game.pak (Please Wait...)", LogLevel.Info);
                }
                Advance();

                // ── Fase 4: copiar datos del ejecutable ──────────────────────
                token.ThrowIfCancellationRequested();
                _currentStep = "Copying executable data...";
                AppendLog("Copying executable data (Please Wait...)", LogLevel.Info);

                PathUtils.CopyAllDataTo("CompileData/Windows/", AssetManager.CompilerPath);
                // PathUtils.CopyAllDataTo(AssetManager.BasePath, AssetManager.CompilerPath + "/Content");
                // File.Copy(AssetManager.DataBase, AssetManager.CompilerPath + "/DataBaseFromAssets.json", true);
                // File.Copy($"{AssetManager.ClientDLLPath}/KrayonClient.dll", $"{AssetManager.CompilerPath}/KrayonClient.dll", true);
                AppendLog("Executable data copied.", LogLevel.Info);
                Advance();

                // ── Fase 5: compilar pak ─────────────────────────────────────
                token.ThrowIfCancellationRequested();
                _currentStep = "Compiling Game.pak...";
                AppendLog("Starting compilation of Game.pak (Please Wait...)", LogLevel.Info);
                KRCompiler.Build($"{AssetManager.CompilerPath}Game.pak", assetsPak);
                AppendLog("Game.pak successfully compiled.", LogLevel.Info);
                Advance(); // _progress llega a 1.0 aquí

                string url = Path.GetFullPath(AssetManager.CompilerPath);

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                // ── Finalizado con éxito ──────────────────────────────────────
                _finalElapsedSeconds = _timer.Elapsed.TotalSeconds;
                _timer.Stop();
                _state = BuildState.Success;
                _progress = 1f;

                AppendLog($"Build succeeded in {_finalElapsedSeconds:F2}s", LogLevel.Success);
                EngineEditor.LogMessage("[Compiler] Build succeeded.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _errorCount++;
                _finalElapsedSeconds = _timer.Elapsed.TotalSeconds;
                _timer.Stop();
                _state = BuildState.Failed;
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