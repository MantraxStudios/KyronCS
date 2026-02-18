using ImGuiNET;
using KrayonCore.Animation;
using KrayonCore.Core.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace KrayonEditor.UI
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Special built-in node names (never stored in AnimatorControllerData)
    // ─────────────────────────────────────────────────────────────────────────
    internal static class SpecialNodes
    {
        public const string Entry = "Entry";
        public const string AnyState = "Any State";
        public const string Exit = "Exit";
    }

    public class AnimatorEditorUI : UIBehaviour
    {
        // ── asset state ──────────────────────────────────────────────────────
        private Guid? _assetGuid;
        private string _assetPath;
        private AnimatorControllerData _data;
        private bool _isDirty;

        // ── canvas ───────────────────────────────────────────────────────────
        private Vector2 _canvasOffset = new(260, 140);
        private float _zoom = 1f;
        private const float ZOOM_MIN = 0.25f;
        private const float ZOOM_MAX = 3f;
        private bool _isPanning;

        // ── node layout ──────────────────────────────────────────────────────
        // Key: "layerIndex/stateName"  so each layer has independent positions
        private Dictionary<string, Vector2> _nodePositions = new();
        private const float NODE_W = 160f;
        private const float NODE_H = 44f;
        private const float HDR_H = 22f;
        private const float SNAP = 16f;

        // ── drag / select ────────────────────────────────────────────────────
        private string _draggingNode;
        private Vector2 _dragOffset;
        private string _selectedState;
        private bool _wasDragging;

        // ── connection drawing ───────────────────────────────────────────────
        private string _connectingFrom;
        private bool _isConnecting;

        // ── transition inspector ─────────────────────────────────────────────
        private string _selectedTransitionFrom;
        private int _selectedTransitionIdx = -1;

        // ── rename (double-click) ────────────────────────────────────────────
        private string _renamingState;
        private string _renameBuffer = "";
        private double _lastClickTime;
        private string _lastClickedNode;

        // ── popups ───────────────────────────────────────────────────────────
        private bool _showNewStatePopup;
        private string _newStateName = "New State";
        private string _newStateClipGuid = "";
        private string _newStateClipName = "";

        private bool _showNewParamPopup;
        private string _newParamName = "NewParam";
        private ParameterType _newParamType = ParameterType.Float;
        private float _newParamDefault;

        private bool _showNewLayerPopup;
        private string _newLayerName = "New Layer";

        private bool _showDeleteStateConfirm;
        private bool _showDeleteParamConfirm;
        private bool _showDeleteLayerConfirm;
        private string _itemToDelete = "";

        // ── right-click canvas ───────────────────────────────────────────────
        private bool _showCanvasCtx;
        private Vector2 _ctxCanvasPos;
        private Vector2 _pendingStatePos;
        private bool _usePendingPos;

        // ── layers ───────────────────────────────────────────────────────────
        private int _selectedLayer = 0;

        // ── helpers: current layer ───────────────────────────────────────────
        private AnimatorLayerData CurrentLayer
            => (_data?.Layers != null && _selectedLayer < _data.Layers.Count)
               ? _data.Layers[_selectedLayer]
               : null;

        private List<AnimatorStateData> CurrentStates
            => CurrentLayer?.States ?? new List<AnimatorStateData>();

        private string CurrentDefaultState
        {
            get => CurrentLayer?.DefaultState ?? "";
            set { if (CurrentLayer != null) CurrentLayer.DefaultState = value; }
        }

        // Node position key scoped to the selected layer
        private string PosKey(string stateName) => $"{_selectedLayer}/{stateName}";

        // ══════════════════════════════════════════════════════════════════════
        //  Colour palette  (Unity-accurate dark theme)
        // ══════════════════════════════════════════════════════════════════════
        private static readonly uint C_BG = Col(0.118f, 0.118f, 0.118f);
        private static readonly uint C_DOT = Col(0.22f, 0.22f, 0.22f);
        private static readonly uint C_DOT_MAJ = Col(0.30f, 0.30f, 0.30f);
        private static readonly uint C_NODE_BG = Col(0.27f, 0.27f, 0.27f);
        private static readonly uint C_NODE_HDR = Col(0.33f, 0.33f, 0.33f);
        private static readonly uint C_NODE_BOR = Col(0.12f, 0.12f, 0.12f);
        private static readonly uint C_DEF_HDR = Col(0.60f, 0.34f, 0.08f);
        private static readonly uint C_DEF_BOR = Col(0.78f, 0.44f, 0.10f);
        private static readonly uint C_SEL_HDR = Col(0.19f, 0.44f, 0.66f);
        private static readonly uint C_SEL_BOR = Col(0.38f, 0.70f, 1.00f);
        private static readonly uint C_ANY_HDR = Col(0.12f, 0.42f, 0.42f);
        private static readonly uint C_ANY_BOR = Col(0.20f, 0.66f, 0.66f);
        private static readonly uint C_ENT_HDR = Col(0.13f, 0.38f, 0.15f);
        private static readonly uint C_ENT_BOR = Col(0.22f, 0.60f, 0.26f);
        private static readonly uint C_EXT_HDR = Col(0.42f, 0.10f, 0.10f);
        private static readonly uint C_EXT_BOR = Col(0.70f, 0.18f, 0.18f);
        private static readonly uint C_ARROW = Col(0.55f, 0.55f, 0.55f);
        private static readonly uint C_ARROW_SEL = Col(1.00f, 0.75f, 0.15f);
        private static readonly uint C_ARROW_HOV = Col(0.95f, 0.95f, 0.95f);
        private static readonly uint C_CONNECT = Col(1.00f, 0.78f, 0.20f);
        private static readonly uint C_TEXT = Col(0.95f, 0.95f, 0.95f);
        private static readonly uint C_TEXT_SUB = Col(0.62f, 0.62f, 0.62f);
        private static readonly uint C_SHADOW = Col(0f, 0f, 0f, 0.45f);

        private static uint Col(float r, float g, float b, float a = 1f)
            => ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a));

        // ══════════════════════════════════════════════════════════════════════
        //  Open / Save
        // ══════════════════════════════════════════════════════════════════════
        public void OpenAsset(Guid guid)
        {
            _assetGuid = guid;
            var record = AssetManager.Get(guid);
            if (record == null) return;
            _assetPath = record.Path;
            _isDirty = false;
            _selectedState = null;
            _selectedTransitionFrom = null;
            _selectedTransitionIdx = -1;
            _selectedLayer = 0;
            _nodePositions.Clear();

            try
            {
                byte[] raw = AssetManager.GetBytes(guid);
                string json = System.Text.Encoding.UTF8.GetString(raw);
                _data = JsonSerializer.Deserialize<AnimatorControllerData>(json)
                        ?? new AnimatorControllerData();
            }
            catch { _data = new AnimatorControllerData(); }

            // ── CRITICAL: migrate legacy single-layer assets ──────────────────
            _data.EnsureBaseLayer();

            LoadNodePositions();
            _isVisible = true;
        }

        private string LayoutPath => _assetPath != null
            ? Path.Combine(AssetManager.BasePath, _assetPath + ".layout") : null;

        private void LoadNodePositions()
        {
            try
            {
                if (LayoutPath != null && File.Exists(LayoutPath))
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, float[]>>(
                                    File.ReadAllText(LayoutPath));
                    if (dict != null)
                        foreach (var kv in dict)
                            _nodePositions[kv.Key] = new Vector2(kv.Value[0], kv.Value[1]);
                }
            }
            catch { }

            // Default positions for special nodes (scoped to each layer)
            for (int li = 0; li < _data.Layers.Count; li++)
            {
                EnsurePos($"{li}/{SpecialNodes.Entry}", 80, 140);
                EnsurePos($"{li}/{SpecialNodes.AnyState}", 80, 260);
                EnsurePos($"{li}/{SpecialNodes.Exit}", 80, 380);

                int idx = 0;
                foreach (var state in _data.Layers[li].States)
                {
                    string key = $"{li}/{state.Name}";
                    if (!_nodePositions.ContainsKey(key))
                    {
                        int total = Math.Max(_data.Layers[li].States.Count, 1);
                        float angle = idx * MathF.PI * 2f / total;
                        _nodePositions[key] = new Vector2(
                            360f + MathF.Cos(angle) * 200f,
                            280f + MathF.Sin(angle) * 160f);
                        idx++;
                    }
                }
            }
        }

        private void EnsurePos(string key, float x, float y)
        { if (!_nodePositions.ContainsKey(key)) _nodePositions[key] = new Vector2(x, y); }

        private void SaveNodePositions()
        {
            try
            {
                if (LayoutPath == null) return;
                var dict = _nodePositions.ToDictionary(
                                kv => kv.Key,
                                kv => new float[] { kv.Value.X, kv.Value.Y });
                File.WriteAllText(LayoutPath, JsonSerializer.Serialize(dict));
            }
            catch { }
        }

        private void Save()
        {
            if (_assetPath == null || _data == null) return;
            try
            {
                File.WriteAllText(
                    Path.Combine(AssetManager.BasePath, _assetPath),
                    JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
                SaveNodePositions();
                _isDirty = false;
            }
            catch (Exception ex) { Console.WriteLine($"[AnimatorEditor] {ex.Message}"); }
        }

        private void MarkDirty() => _isDirty = true;

        // ══════════════════════════════════════════════════════════════════════
        //  Main draw
        // ══════════════════════════════════════════════════════════════════════
        public override void OnDrawUI()
        {
            if (!_isVisible || _data == null) return;

            string name = Path.GetFileNameWithoutExtension(_assetPath);
            string title = $"Animator  —  {name}{(_isDirty ? "  *" : "")}###AnimatorEditorWindow";

            ImGui.SetNextWindowSize(new Vector2(1280, 780), ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.14f, 0.14f, 0.14f, 1f));
            ImGui.Begin(title, ref _isVisible,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();

            DrawMenuBar();

            float totalH = ImGui.GetContentRegionAvail().Y;
            float leftW = 228f;
            float rightW = 300f;
            float midW = ImGui.GetContentRegionAvail().X - leftW - rightW - 2f;

            // ── Left: Layers + Parameters ─────────────────────────────────────
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.16f, 0.16f, 1f));
            ImGui.BeginChild("LeftPanel", new Vector2(leftW, totalH), ImGuiChildFlags.None);
            ImGui.PopStyleColor();
            DrawLayersPanel();
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.24f, 0.24f, 0.24f, 1f));
            ImGui.Separator();
            ImGui.PopStyleColor();
            DrawParametersPanel();
            ImGui.EndChild();

            ImGui.SameLine(0, 1);

            // ── Centre: Canvas ────────────────────────────────────────────────
            ImGui.BeginChild("AnimCanvas", new Vector2(midW, totalH), ImGuiChildFlags.None,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            DrawCanvas();
            ImGui.EndChild();

            ImGui.SameLine(0, 1);

            // ── Right: Inspector ──────────────────────────────────────────────
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.16f, 0.16f, 1f));
            ImGui.BeginChild("RightPanel", new Vector2(rightW, totalH), ImGuiChildFlags.None);
            ImGui.PopStyleColor();
            DrawInspectorPanel();
            ImGui.EndChild();

            DrawPopups();
            ImGui.End();
        }

        // ── Menu bar ─────────────────────────────────────────────────────────
        private void DrawMenuBar()
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.18f, 0.18f, 0.18f, 1f));
            ImGui.BeginChild("MenuBar", new Vector2(0, 32), ImGuiChildFlags.None);
            ImGui.PopStyleColor();
            ImGui.SetCursorPos(new Vector2(6, 5));

            bool dirty = _isDirty;
            ImGui.PushStyleColor(ImGuiCol.Button,
                dirty ? new Vector4(0.52f, 0.26f, 0.06f, 1f) : new Vector4(0.22f, 0.22f, 0.22f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered,
                dirty ? new Vector4(0.68f, 0.34f, 0.08f, 1f) : new Vector4(0.32f, 0.32f, 0.32f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
            if (ImGui.Button(dirty ? " Save * " : " Save ", new Vector2(0, 22))) Save();
            ImGui.PopStyleColor(3);

            ImGui.SameLine(0, 8);
            Btn(" Auto Layout ", new Vector2(0, 22), AutoLayout);
            ImGui.SameLine(0, 4);
            Btn(" Fit View ", new Vector2(0, 22), FitView);
            ImGui.SameLine(0, 4);
            Btn(" - ", new Vector2(26, 22), () => _zoom = Math.Clamp(_zoom - 0.1f, ZOOM_MIN, ZOOM_MAX));
            ImGui.SameLine(0, 2);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
            ImGui.Text($"{_zoom * 100:F0}%%");
            ImGui.PopStyleColor();
            ImGui.SameLine(0, 2);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 4);
            Btn(" + ", new Vector2(26, 22), () => _zoom = Math.Clamp(_zoom + 0.1f, ZOOM_MIN, ZOOM_MAX));

            ImGui.SameLine(0, 16);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.35f, 0.35f, 0.35f, 1f));
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4);
            ImGui.Text("Scroll: zoom  •  Middle-drag: pan  •  Right-click canvas: new state  •  Double-click: rename");
            ImGui.PopStyleColor();
            ImGui.EndChild();
        }

        private void Btn(string label, Vector2 size, Action action)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.22f, 0.22f, 0.22f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.32f, 0.32f, 0.32f, 1f));
            if (ImGui.Button(label, size)) action();
            ImGui.PopStyleColor(2);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Left panel — Layers
        // ══════════════════════════════════════════════════════════════════════
        private void DrawLayersPanel()
        {
            ImGui.SetCursorPos(new Vector2(8, 8));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
            ImGui.Text("Layers");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 28f);
            SmallIconBtn("+##addlayer", () => { _newLayerName = "New Layer"; _showNewLayerPopup = true; });

            ImGui.Dummy(new Vector2(0, 2));

            for (int i = 0; i < _data.Layers.Count; i++)
                DrawLayerRow(i);
        }

        private void DrawLayerRow(int idx)
        {
            var layer = _data.Layers[idx];
            bool isBase = idx == 0;
            bool isSel = idx == _selectedLayer;

            ImGui.PushID($"lr_{idx}");

            const float ROW_H = 24f;
            const float X_W = 22f;   // width of the × button
            const float MARGIN = 8f;

            float panelW = ImGui.GetContentRegionAvail().X;

            // ── row rect ─────────────────────────────────────────────────────
            // The invisible button takes all space left of the × (or all space for base layer).
            float btnW = isBase
                ? panelW - MARGIN * 2f
                : panelW - MARGIN * 2f - X_W - 4f;

            ImGui.SetCursorPosX(MARGIN);
            var p = ImGui.GetCursorScreenPos();
            var dl = ImGui.GetWindowDrawList();

            // ── background rect (selected only) ──────────────────────────────
            if (isSel)
                dl.AddRectFilled(p, p + new Vector2(btnW, ROW_H),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.19f, 0.40f, 0.62f, 1f)), 3f);

            // ── invisible selection button ────────────────────────────────────
            if (ImGui.InvisibleButton($"lrbtn{idx}", new Vector2(btnW, ROW_H)))
            {
                if (_selectedLayer != idx)
                {
                    _selectedLayer = idx;
                    _selectedState = null;
                    _selectedTransitionFrom = null;
                    _selectedTransitionIdx = -1;
                    _isConnecting = false;
                    EnsurePos(PosKey(SpecialNodes.Entry), 80, 140);
                    EnsurePos(PosKey(SpecialNodes.AnyState), 80, 260);
                    EnsurePos(PosKey(SpecialNodes.Exit), 80, 380);
                }
            }

            // ── label drawn via DrawList (doesn't disturb layout) ────────────
            string display = layer.Name.Length > 18 ? layer.Name[..18] + "…" : layer.Name;
            uint textCol = isSel
                ? ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f))
                : ImGui.ColorConvertFloat4ToU32(new Vector4(0.75f, 0.75f, 0.75f, 1f));
            dl.AddText(p + new Vector2(8f, (ROW_H - ImGui.GetTextLineHeight()) * 0.5f), textCol, display);

            // ── delete button (non-base): placed immediately after the inv. button ──
            if (!isBase)
            {
                ImGui.SameLine(0, 4f);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.50f, 0.20f, 0.20f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0f, 0f, 0f, 0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.30f, 0.10f, 0.10f, 1f));
                if (ImGui.Button($"×##xlr{idx}", new Vector2(X_W, ROW_H)))
                {
                    _itemToDelete = idx.ToString();
                    _showDeleteLayerConfirm = true;
                }
                ImGui.PopStyleColor(3);
            }

            // ── Weight + blending (non-base layers, only when selected) ───────
            if (!isBase && isSel)
            {
                ImGui.SetCursorPosX(MARGIN);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.50f, 0.50f, 0.50f, 1f));
                ImGui.Text("Weight");
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 10f);
                float w = layer.Weight;
                if (ImGui.SliderFloat("##lw", ref w, 0f, 1f))
                { layer.Weight = w; MarkDirty(); }

                ImGui.SetCursorPosX(MARGIN);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.50f, 0.50f, 0.50f, 1f));
                ImGui.Text("Blending");
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 10f);
                if (ImGui.BeginCombo("##lbm", layer.BlendingMode.ToString()))
                {
                    foreach (LayerBlendingMode m in Enum.GetValues<LayerBlendingMode>())
                        if (ImGui.Selectable(m.ToString(), layer.BlendingMode == m))
                        { layer.BlendingMode = m; MarkDirty(); }
                    ImGui.EndCombo();
                }

                ImGui.SetCursorPosX(MARGIN);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.50f, 0.50f, 0.50f, 1f));
                ImGui.Text("Mask GUID");
                ImGui.PopStyleColor();
                ImGui.SetCursorPosX(MARGIN);
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 10f);
                string mask = layer.AvatarMask ?? "";
                if (ImGui.InputText("##lmask", ref mask, 128)) { layer.AvatarMask = mask; MarkDirty(); }
            }

            ImGui.Dummy(new Vector2(0, 2));
            ImGui.PopID();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Left panel — Parameters
        // ══════════════════════════════════════════════════════════════════════
        private void DrawParametersPanel()
        {
            ImGui.Spacing();
            ImGui.SetCursorPosX(8);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
            ImGui.Text("Parameters");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 28f);
            SmallIconBtn("+##addparam", () =>
            { _newParamName = "NewParam"; _newParamType = ParameterType.Float; _newParamDefault = 0f; _showNewParamPopup = true; });

            ImGui.Dummy(new Vector2(0, 2));

            for (int i = 0; i < _data.Parameters.Count; i++)
            {
                var p = _data.Parameters[i];
                ImGui.PushID($"p_{i}");
                ImGui.SetCursorPosX(6);

                (string badge, Vector4 badgeCol) = p.Type switch
                {
                    ParameterType.Float => ("F", new Vector4(0.40f, 0.70f, 0.40f, 1f)),
                    ParameterType.Int => ("I", new Vector4(0.40f, 0.60f, 0.90f, 1f)),
                    ParameterType.Bool => ("B", new Vector4(0.90f, 0.65f, 0.25f, 1f)),
                    ParameterType.Trigger => ("T", new Vector4(0.80f, 0.35f, 0.35f, 1f)),
                    _ => ("?", new Vector4(0.5f, 0.5f, 0.5f, 1f))
                };
                ImGui.PushStyleColor(ImGuiCol.Text, badgeCol);
                ImGui.Text($"[{badge}]");
                ImGui.PopStyleColor();
                ImGui.SameLine();

                ImGui.SetNextItemWidth(80);
                string pname = p.Name;
                if (ImGui.InputText($"##pn{i}", ref pname, 64)) { p.Name = pname; MarkDirty(); }
                ImGui.SameLine();

                float dv = p.DefaultValue;
                switch (p.Type)
                {
                    case ParameterType.Float:
                        ImGui.SetNextItemWidth(42);
                        if (ImGui.DragFloat($"##pv{i}", ref dv, 0.01f)) { p.DefaultValue = dv; MarkDirty(); }
                        break;
                    case ParameterType.Int:
                        ImGui.SetNextItemWidth(42);
                        int iv = (int)dv;
                        if (ImGui.DragInt($"##pv{i}", ref iv)) { p.DefaultValue = iv; MarkDirty(); }
                        break;
                    case ParameterType.Bool:
                        bool bv = dv != 0f;
                        if (ImGui.Checkbox($"##pv{i}", ref bv)) { p.DefaultValue = bv ? 1f : 0f; MarkDirty(); }
                        break;
                    default:
                        ImGui.SetNextItemWidth(42);
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.4f, 0.4f, 1f));
                        ImGui.Text("---"); ImGui.PopStyleColor();
                        break;
                }
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.50f, 0.20f, 0.20f, 1f));
                if (ImGui.SmallButton($"×##xp{i}")) { _itemToDelete = p.Name; _showDeleteParamConfirm = true; }
                ImGui.PopStyleColor();
                ImGui.PopID();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Canvas
        // ══════════════════════════════════════════════════════════════════════
        private void DrawCanvas()
        {
            if (CurrentLayer == null) return;

            var dl = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var size = ImGui.GetContentRegionAvail();
            var io = ImGui.GetIO();

            dl.AddRectFilled(origin, origin + size, C_BG);
            DrawDotGrid(dl, origin, size);

            // Layer name watermark
            ImGui.GetWindowDrawList().AddText(
                origin + new Vector2(size.X - ImGui.CalcTextSize(CurrentLayer.Name).X - 12, size.Y - 24),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.28f, 0.28f, 0.28f, 1f)),
                CurrentLayer.Name);

            float dotR = 5.5f * _zoom;
            string outHovNode = null;

            foreach (var state in CurrentStates)
            {
                GetNodeDots(PosKey(state.Name), origin, out Vector2 outPt, out _);
                if (Vector2.Distance(io.MousePos, outPt) < dotR * 2.2f) outHovNode = state.Name;
            }

            if (outHovNode != null && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _isConnecting = true;
                _connectingFrom = outHovNode;
                _draggingNode = null;
            }

            ImGui.InvisibleButton("canvas_bg", size,
                ImGuiButtonFlags.MouseButtonLeft |
                ImGuiButtonFlags.MouseButtonMiddle |
                ImGuiButtonFlags.MouseButtonRight);

            bool canvasHov = ImGui.IsItemHovered() || outHovNode != null;

            // Pan
            if (canvasHov && ImGui.IsMouseClicked(ImGuiMouseButton.Middle)) _isPanning = true;
            if (_isPanning) { _canvasOffset += io.MouseDelta; if (!ImGui.IsMouseDown(ImGuiMouseButton.Middle)) _isPanning = false; }

            // Zoom
            if (canvasHov && io.MouseWheel != 0)
            {
                float oldZ = _zoom;
                _zoom = Math.Clamp(_zoom + io.MouseWheel * 0.08f, ZOOM_MIN, ZOOM_MAX);
                _canvasOffset -= ((io.MousePos - origin - _canvasOffset) / oldZ) * (_zoom - oldZ);
            }

            // Right-click empty canvas
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                if (!AllNodeKeys().Any(k => IsOverNodeByKey(k, origin)))
                { _ctxCanvasPos = ToCanvas(io.MousePos, origin); _showCanvasCtx = true; }
            }

            if (_showCanvasCtx) { ImGui.OpenPopup("CanvasCtxMenu"); _showCanvasCtx = false; }
            if (ImGui.BeginPopup("CanvasCtxMenu"))
            {
                if (ImGui.MenuItem("Create State"))
                {
                    _newStateName = "New State"; _newStateClipGuid = ""; _newStateClipName = "";
                    _pendingStatePos = _ctxCanvasPos; _usePendingPos = true;
                    _showNewStatePopup = true;
                }
                ImGui.EndPopup();
            }

            dl.PushClipRect(origin, origin + size, true);

            // Arrows
            foreach (var state in CurrentStates)
                for (int ti = 0; ti < state.Transitions.Count; ti++)
                {
                    bool sel = _selectedTransitionFrom == state.Name && _selectedTransitionIdx == ti;
                    DrawArrow(dl, origin, state.Name, state.Transitions[ti].ToState, sel);
                }

            // Live connection preview
            if (_isConnecting && _connectingFrom != null)
            {
                GetNodeDots(PosKey(_connectingFrom), origin, out Vector2 fromOut, out _);
                DrawBezier(dl, fromOut, io.MousePos, C_CONNECT, 2f);
                dl.AddCircleFilled(io.MousePos, 4f, C_CONNECT);
            }

            // Special nodes
            DrawSpecialNode(dl, origin, SpecialNodes.Entry, C_ENT_HDR, C_ENT_BOR, canvasHov);
            DrawSpecialNode(dl, origin, SpecialNodes.AnyState, C_ANY_HDR, C_ANY_BOR, canvasHov);
            DrawSpecialNode(dl, origin, SpecialNodes.Exit, C_EXT_HDR, C_EXT_BOR, canvasHov);

            // User states
            foreach (var state in CurrentStates)
                DrawNode(dl, origin, state, canvasHov);

            dl.PopClipRect();

            // Deselect on empty click
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && outHovNode == null)
                if (!AllNodeKeys().Any(k => IsOverNodeByKey(k, origin)))
                { _selectedState = null; _selectedTransitionFrom = null; _selectedTransitionIdx = -1; _isConnecting = false; }

            if (_isConnecting && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            { _isConnecting = false; _connectingFrom = null; }

            if (_isConnecting && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                foreach (var state in CurrentStates)
                {
                    if (state.Name == _connectingFrom) continue;
                    if (IsOverNodeByKey(PosKey(state.Name), origin))
                    { AddTransition(_connectingFrom, state.Name); break; }
                }
                _isConnecting = false; _connectingFrom = null;
            }
        }

        // ── Dot grid ─────────────────────────────────────────────────────────
        private void DrawDotGrid(ImDrawListPtr dl, Vector2 origin, Vector2 size)
        {
            float minor = 20f * _zoom, major = 100f * _zoom;
            float ox = ((_canvasOffset.X % minor) + minor) % minor;
            float oy = ((_canvasOffset.Y % minor) + minor) % minor;

            for (float x = ox; x < size.X; x += minor)
                for (float y = oy; y < size.Y; y += minor)
                {
                    bool isMaj = MathF.Abs((x - ox) % major) < 1f || MathF.Abs((y - oy) % major) < 1f;
                    dl.AddCircleFilled(origin + new Vector2(x, y), _zoom < 0.6f ? 0.8f : 1.2f,
                                       isMaj ? C_DOT_MAJ : C_DOT);
                }
        }

        // ── Coord helpers ─────────────────────────────────────────────────────
        private Vector2 ToScreen(Vector2 c, Vector2 origin) => origin + _canvasOffset + c * _zoom;
        private Vector2 ToCanvas(Vector2 s, Vector2 origin) => (s - origin - _canvasOffset) / _zoom;
        private Vector2 NodeScreenPosFromKey(string key, Vector2 origin)
        { _nodePositions.TryGetValue(key, out var p); return ToScreen(p, origin); }

        private void GetNodeDots(string posKey, Vector2 origin, out Vector2 outPt, out Vector2 inPt)
        {
            Vector2 sp = NodeScreenPosFromKey(posKey, origin);
            Vector2 sz = new(NODE_W * _zoom, NODE_H * _zoom);
            outPt = sp + new Vector2(sz.X, sz.Y * 0.5f);
            inPt = sp + new Vector2(0, sz.Y * 0.5f);
        }

        private bool IsOverNodeByKey(string posKey, Vector2 origin)
        {
            var sp = NodeScreenPosFromKey(posKey, origin);
            var mp = ImGui.GetIO().MousePos;
            return mp.X >= sp.X && mp.X <= sp.X + NODE_W * _zoom &&
                   mp.Y >= sp.Y && mp.Y <= sp.Y + NODE_H * _zoom;
        }

        private IEnumerable<string> AllNodeKeys()
        {
            yield return PosKey(SpecialNodes.Entry);
            yield return PosKey(SpecialNodes.AnyState);
            yield return PosKey(SpecialNodes.Exit);
            foreach (var s in CurrentStates) yield return PosKey(s.Name);
        }

        private Vector2 SnapToGrid(Vector2 v)
            => new(MathF.Round(v.X / SNAP) * SNAP, MathF.Round(v.Y / SNAP) * SNAP);

        private static void DrawBezier(ImDrawListPtr dl, Vector2 a, Vector2 b, uint col, float thick)
        {
            float dx = MathF.Abs(b.X - a.X) * 0.5f + 40f;
            dl.AddBezierCubic(a, a + new Vector2(dx, 0), b - new Vector2(dx, 0), b, col, thick, 0);
        }

        // ── Special node ──────────────────────────────────────────────────────
        private void DrawSpecialNode(ImDrawListPtr dl, Vector2 origin,
                                     string label, uint hdrCol, uint borCol, bool canvasHov)
        {
            string key = PosKey(label);
            Vector2 sp = NodeScreenPosFromKey(key, origin);
            Vector2 sz = new(NODE_W * _zoom, NODE_H * _zoom);
            float r = 5f * _zoom, hdr = HDR_H * _zoom;
            bool sel = _selectedState == label;
            if (sel) borCol = C_SEL_BOR;

            dl.AddRectFilled(sp + new Vector2(3, 4), sp + sz + new Vector2(3, 4), C_SHADOW, r);
            dl.AddRectFilled(sp, sp + sz, C_NODE_BG, r);
            dl.AddRectFilled(sp, sp + new Vector2(sz.X, hdr), hdrCol, r);
            dl.AddRectFilled(sp + new Vector2(0, hdr - r), sp + new Vector2(sz.X, hdr), hdrCol);
            dl.AddRect(sp, sp + sz, borCol, r, ImDrawFlags.None, sel ? 2f : 1f);
            Vector2 tsz = ImGui.CalcTextSize(label);
            dl.AddText(sp + new Vector2((sz.X - tsz.X) * 0.5f, (hdr - tsz.Y) * 0.5f), C_TEXT, label);

            if (canvasHov && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && IsOverNodeByKey(key, origin))
            {
                _selectedState = label; _draggingNode = label;
                _dragOffset = ImGui.GetIO().MousePos - sp; _wasDragging = false;
                _selectedTransitionFrom = null; _selectedTransitionIdx = -1;
            }
            if (_draggingNode == label)
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                { _nodePositions[key] = SnapToGrid(ToCanvas(ImGui.GetIO().MousePos - _dragOffset, origin)); _wasDragging = true; }
                else _draggingNode = null;
            }
        }

        // ── User state node ───────────────────────────────────────────────────
        private void DrawNode(ImDrawListPtr dl, Vector2 origin, AnimatorStateData state, bool canvasHov)
        {
            string name = state.Name;
            string posKey = PosKey(name);
            bool isDef = name == CurrentDefaultState;
            bool isSel = name == _selectedState;
            float dotR = 5.5f * _zoom;

            Vector2 sp = NodeScreenPosFromKey(posKey, origin);
            Vector2 sz = new(NODE_W * _zoom, NODE_H * _zoom);
            float r = 5f * _zoom, hdr = HDR_H * _zoom;

            uint hdrC = isSel ? C_SEL_HDR : (isDef ? C_DEF_HDR : C_NODE_HDR);
            uint bor = isSel ? C_SEL_BOR : (isDef ? C_DEF_BOR : C_NODE_BOR);

            dl.AddRectFilled(sp + new Vector2(3, 4), sp + sz + new Vector2(3, 4), C_SHADOW, r);
            dl.AddRectFilled(sp, sp + sz, C_NODE_BG, r);
            dl.AddRectFilled(sp, sp + new Vector2(sz.X, hdr), hdrC, r);
            dl.AddRectFilled(sp + new Vector2(0, hdr - r), sp + new Vector2(sz.X, hdr), hdrC);
            dl.AddRect(sp, sp + sz, bor, r, ImDrawFlags.None, isSel ? 2f : 1f);

            string label = name.Length > 19 ? name[..19] + "…" : name;
            Vector2 lsz = ImGui.CalcTextSize(label);
            dl.AddText(sp + new Vector2((sz.X - lsz.X) * 0.5f, (hdr - lsz.Y) * 0.5f), C_TEXT, label);

            if (!string.IsNullOrEmpty(state.ClipName))
            {
                string sub = state.ClipName.Length > 21 ? state.ClipName[..21] + "…" : state.ClipName;
                Vector2 ssz = ImGui.CalcTextSize(sub);
                dl.AddText(sp + new Vector2((sz.X - ssz.X) * 0.5f, hdr + (sz.Y - hdr - ssz.Y) * 0.5f), C_TEXT_SUB, sub);
            }

            if (isDef)
            {
                float cy = sp.Y + sz.Y * 0.5f;
                dl.AddTriangleFilled(new Vector2(sp.X - 12f * _zoom, cy),
                    new Vector2(sp.X - 3f * _zoom, cy - 5f * _zoom),
                    new Vector2(sp.X - 3f * _zoom, cy + 5f * _zoom), C_DEF_BOR);
            }

            GetNodeDots(posKey, origin, out Vector2 outPt, out Vector2 inPt);
            var io = ImGui.GetIO();
            dl.AddCircleFilled(outPt, dotR, Vector2.Distance(io.MousePos, outPt) < dotR * 2f ? C_CONNECT : C_ARROW);
            dl.AddCircleFilled(inPt, dotR, Vector2.Distance(io.MousePos, inPt) < dotR * 2f ? C_CONNECT : C_ARROW);

            if (canvasHov && IsOverNodeByKey(posKey, origin))
            {
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Vector2.Distance(io.MousePos, outPt) >= dotR * 2f)
                {
                    double now = ImGui.GetTime();
                    bool dbl = _lastClickedNode == name && (now - _lastClickTime) < 0.35;
                    _lastClickTime = now;
                    _lastClickedNode = name;

                    if (dbl) { _renamingState = name; _renameBuffer = name; }
                    else
                    {
                        _selectedState = name; _selectedTransitionFrom = null; _selectedTransitionIdx = -1;
                        _draggingNode = name; _dragOffset = io.MousePos - sp; _wasDragging = false;
                    }
                }
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                { _selectedState = name; ImGui.OpenPopup($"NodeCtx_{name}"); }
            }

            if (_draggingNode == name)
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                { _nodePositions[posKey] = io.KeyAlt ? ToCanvas(io.MousePos - _dragOffset, origin) : SnapToGrid(ToCanvas(io.MousePos - _dragOffset, origin)); _wasDragging = true; MarkDirty(); }
                else _draggingNode = null;
            }

            if (ImGui.BeginPopup($"NodeCtx_{name}"))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
                ImGui.Text(name); ImGui.Separator(); ImGui.PopStyleColor();
                if (name != CurrentDefaultState && ImGui.MenuItem("Set as Default State"))
                { CurrentDefaultState = name; MarkDirty(); }
                if (ImGui.MenuItem("Make Transition")) { _isConnecting = true; _connectingFrom = name; }
                if (ImGui.MenuItem("Rename")) { _renamingState = name; _renameBuffer = name; }
                ImGui.Separator();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.88f, 0.35f, 0.35f, 1f));
                if (ImGui.MenuItem("Delete State")) { _itemToDelete = name; _showDeleteStateConfirm = true; }
                ImGui.PopStyleColor();
                ImGui.EndPopup();
            }
        }

        // ── Arrow ─────────────────────────────────────────────────────────────
        private void DrawArrow(ImDrawListPtr dl, Vector2 origin,
                               string fromName, string toName, bool selected)
        {
            string fk = PosKey(fromName), tk = PosKey(toName);
            if (!_nodePositions.ContainsKey(fk) || !_nodePositions.ContainsKey(tk)) return;

            float dotR = 5.5f * _zoom;
            GetNodeDots(fk, origin, out Vector2 fromOut, out _);
            GetNodeDots(tk, origin, out _, out Vector2 toIn);

            bool bidir = CurrentStates.Any(s => s.Name == toName && s.Transitions.Any(t => t.ToState == fromName));
            Vector2 perp = Vector2.Normalize(new Vector2(-(toIn.Y - fromOut.Y), toIn.X - fromOut.X));
            float off = bidir ? 6f * _zoom : 0f;
            Vector2 fe = fromOut + perp * off;
            Vector2 te = toIn + perp * off;
            Vector2 mid = (fe + te) * 0.5f;
            bool hov = Vector2.Distance(ImGui.GetIO().MousePos, mid) < 12f;
            uint col = selected ? C_ARROW_SEL : (hov ? C_ARROW_HOV : C_ARROW);

            if (fromName == toName)
            { dl.AddCircle(fromOut + new Vector2(0, -28f * _zoom), 28f * _zoom, col, 32, selected ? 2.5f : 1.5f); return; }

            DrawBezier(dl, fe, te, col, selected ? 2.5f : 1.5f);
            Vector2 dir = Vector2.Normalize(te - fe);
            Vector2 p2 = te - dir * 10f * _zoom;
            dl.AddTriangleFilled(te, p2 + perp * 5f * _zoom, p2 - perp * 5f * _zoom, col);
            dl.AddCircleFilled(mid, 4f * _zoom, col);

            if (hov && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                var fs = CurrentStates.FirstOrDefault(s => s.Name == fromName);
                if (fs != null)
                    for (int i = 0; i < fs.Transitions.Count; i++)
                        if (fs.Transitions[i].ToState == toName)
                        { _selectedTransitionFrom = fromName; _selectedTransitionIdx = i; _selectedState = fromName; break; }
            }
        }

        private void AddTransition(string from, string to)
        {
            var state = CurrentStates.FirstOrDefault(s => s.Name == from);
            if (state == null || state.Transitions.Any(t => t.ToState == to)) return;
            state.Transitions.Add(new StateTransitionData { ToState = to, Duration = 0.25f, CanInterrupt = true, Conditions = new() });
            _selectedTransitionFrom = from; _selectedTransitionIdx = state.Transitions.Count - 1; _selectedState = from;
            MarkDirty();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Right panel — Inspector
        // ══════════════════════════════════════════════════════════════════════
        private void DrawInspectorPanel()
        {
            ImGui.Spacing();
            ImGui.SetCursorPosX(10);

            if (_selectedState == null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.40f, 0.40f, 0.40f, 1f));
                ImGui.TextWrapped("Select a state or transition to inspect it.");
                ImGui.PopStyleColor();
                return;
            }

            bool isSpecial = _selectedState is SpecialNodes.Entry or SpecialNodes.AnyState or SpecialNodes.Exit;
            if (isSpecial)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
                ImGui.Text(_selectedState); ImGui.PopStyleColor();
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.38f, 0.38f, 0.38f, 1f));
                ImGui.TextWrapped("Built-in state — not editable."); ImGui.PopStyleColor();
                return;
            }

            var sd = CurrentStates.FirstOrDefault(s => s.Name == _selectedState);
            if (sd == null) return;

            if (_selectedTransitionFrom == _selectedState && _selectedTransitionIdx >= 0 &&
                _selectedTransitionIdx < sd.Transitions.Count)
            {
                DrawTransitionInspector(sd);
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.24f, 0.24f, 0.24f, 1f));
                ImGui.Separator(); ImGui.PopStyleColor();
                ImGui.Spacing();
            }

            InspectorHeader("State", sd.Name);
            InspectorSection("Motion");

            ImGui.SetCursorPosX(10);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
            ImGui.Text("Clip GUID"); ImGui.PopStyleColor();
            ImGui.SetCursorPosX(10); ImGui.SetNextItemWidth(-10);
            string clipGuid = sd.ClipGuid ?? "";
            if (ImGui.InputText("##cguid", ref clipGuid, 128)) { sd.ClipGuid = clipGuid; MarkDirty(); }
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("ASSET_PATH");
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        byte[] bytes = new byte[payload.DataSize];
                        System.Runtime.InteropServices.Marshal.Copy(payload.Data, bytes, 0, payload.DataSize);
                        string guidStr = System.Text.Encoding.UTF8.GetString(bytes);
                        sd.ClipGuid = guidStr;
                        if (Guid.TryParse(guidStr, out Guid ag))
                        {
                            var rec = AssetManager.Get(ag);
                            if (rec != null) sd.ClipName = Path.GetFileNameWithoutExtension(rec.Path);
                        }
                        MarkDirty();
                    }
                }
                ImGui.EndDragDropTarget();
            }
            ImGui.SetCursorPosX(10);
            if (!string.IsNullOrEmpty(sd.ClipGuid) && Guid.TryParse(sd.ClipGuid, out Guid pg))
            {
                var rec = AssetManager.Get(pg);
                ImGui.PushStyleColor(ImGuiCol.Text, rec != null ? new Vector4(0.28f, 0.72f, 0.38f, 1f) : new Vector4(0.80f, 0.28f, 0.28f, 1f));
                ImGui.Text(rec != null ? $"  ✓  {Path.GetFileName(rec.Path)}" : "  ✗  Asset not found");
                ImGui.PopStyleColor();
            }
            else { ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.35f, 0.35f, 0.35f, 1f)); ImGui.Text("  Drag an FBX from Assets"); ImGui.PopStyleColor(); }

            ImGui.Spacing();
            ImGui.SetCursorPosX(10);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
            ImGui.Text("Clip Name"); ImGui.PopStyleColor();
            ImGui.SetCursorPosX(10); ImGui.SetNextItemWidth(-10);
            string clipName = sd.ClipName ?? "";
            if (ImGui.InputText("##cname", ref clipName, 128)) { sd.ClipName = clipName; MarkDirty(); }

            ImGui.Spacing();
            InspectorSection("Properties");
            ImGui.SetCursorPosX(10);
            bool loop = sd.Loop;
            if (ImGui.Checkbox("Loop Time", ref loop)) { sd.Loop = loop; MarkDirty(); }
            ImGui.SetCursorPosX(10);
            InspectorRow("Speed");
            ImGui.SetNextItemWidth(90);
            float speed = sd.Speed;
            if (ImGui.DragFloat("##spd", ref speed, 0.01f, 0f, 10f)) { sd.Speed = speed; MarkDirty(); }

            ImGui.Spacing();
            InspectorSection("Transitions");

            for (int ti = 0; ti < sd.Transitions.Count; ti++)
            {
                var t = sd.Transitions[ti];
                bool tsel = _selectedTransitionFrom == _selectedState && _selectedTransitionIdx == ti;
                ImGui.PushID($"tr_{ti}");
                float xW = 26f;
                float btnW = ImGui.GetContentRegionAvail().X - xW - 4f - 10f;
                ImGui.SetCursorPosX(10);

                uint bg = tsel
                    ? ImGui.ColorConvertFloat4ToU32(new Vector4(0.19f, 0.40f, 0.62f, 1f))
                    : ImGui.ColorConvertFloat4ToU32(new Vector4(0.20f, 0.20f, 0.20f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Button, bg);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.ColorConvertFloat4ToU32(new Vector4(0.26f, 0.46f, 0.70f, 1f)));
                if (ImGui.Button($"  → {t.ToState}##tr{ti}", new Vector2(btnW, 22)))
                { _selectedTransitionFrom = tsel ? null : _selectedState; _selectedTransitionIdx = tsel ? -1 : ti; }
                ImGui.PopStyleColor(2);

                ImGui.SameLine(0, 4);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.42f, 0.10f, 0.10f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.65f, 0.16f, 0.16f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.70f, 0.70f, 1f));
                if (ImGui.Button($"×##xtr{ti}", new Vector2(xW, 22)))
                {
                    sd.Transitions.RemoveAt(ti);
                    if (_selectedTransitionIdx == ti) { _selectedTransitionFrom = null; _selectedTransitionIdx = -1; }
                    MarkDirty();
                    ImGui.PopStyleColor(3); ImGui.PopID(); break;
                }
                ImGui.PopStyleColor(3);
                ImGui.PopID();
                ImGui.Spacing();
            }

            ImGui.SetCursorPosX(10);
            Btn("Make Transition", new Vector2(-10f, 0), () => { _isConnecting = true; _connectingFrom = _selectedState; });
        }

        private void DrawTransitionInspector(AnimatorStateData sd)
        {
            var t = sd.Transitions[_selectedTransitionIdx];
            InspectorHeader("Transition", $"{sd.Name}  →  {t.ToState}");

            ImGui.SetCursorPosX(10); InspectorRow("Blend Time");
            ImGui.SetNextItemWidth(80);
            float dur = t.Duration;
            if (ImGui.DragFloat("##dur", ref dur, 0.005f, 0f, 5f)) { t.Duration = dur; MarkDirty(); }

            ImGui.SetCursorPosX(10);
            bool ci = t.CanInterrupt;
            if (ImGui.Checkbox("Can Interrupt", ref ci)) { t.CanInterrupt = ci; MarkDirty(); }

            ImGui.SetCursorPosX(10);
            bool het = t.HasExitTime;
            if (ImGui.Checkbox("Has Exit Time", ref het)) { t.HasExitTime = het; MarkDirty(); }
            if (t.HasExitTime)
            {
                ImGui.SameLine(); ImGui.SetNextItemWidth(60);
                float et = t.ExitTime;
                if (ImGui.DragFloat("##et", ref et, 0.01f, 0f, 1f)) { t.ExitTime = et; MarkDirty(); }
            }

            ImGui.Spacing(); ImGui.SetCursorPosX(10);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
            ImGui.Text("Conditions"); ImGui.PopStyleColor();

            for (int ci2 = 0; ci2 < t.Conditions.Count; ci2++)
            {
                var cond = t.Conditions[ci2];
                ImGui.PushID($"c_{ci2}"); ImGui.SetCursorPosX(10);
                ImGui.SetNextItemWidth(80);
                if (ImGui.BeginCombo("##cp", cond.Parameter ?? "---"))
                {
                    foreach (var param in _data.Parameters)
                        if (ImGui.Selectable(param.Name, cond.Parameter == param.Name))
                        { cond.Parameter = param.Name; MarkDirty(); }
                    ImGui.EndCombo();
                }
                ImGui.SameLine(); ImGui.SetNextItemWidth(64);
                if (ImGui.BeginCombo("##cm", cond.Mode.ToString()))
                {
                    foreach (ConditionMode m in Enum.GetValues<ConditionMode>())
                        if (ImGui.Selectable(m.ToString(), cond.Mode == m)) { cond.Mode = m; MarkDirty(); }
                    ImGui.EndCombo();
                }
                if (cond.Mode is ConditionMode.Greater or ConditionMode.Less or ConditionMode.Equals)
                { ImGui.SameLine(); ImGui.SetNextItemWidth(48); float thr = cond.Threshold; if (ImGui.DragFloat("##ct", ref thr, 0.1f)) { cond.Threshold = thr; MarkDirty(); } }
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.50f, 0.20f, 0.20f, 1f));
                if (ImGui.SmallButton($"×##dc{ci2}")) { t.Conditions.RemoveAt(ci2); MarkDirty(); ImGui.PopStyleColor(); ImGui.PopID(); break; }
                ImGui.PopStyleColor();
                ImGui.PopID();
            }

            ImGui.Spacing(); ImGui.SetCursorPosX(10);
            SmallIconBtn("+ Add Condition", () =>
            {
                t.Conditions.Add(new TransitionConditionData
                { Parameter = _data.Parameters.Count > 0 ? _data.Parameters[0].Name : "", Mode = ConditionMode.Greater, Threshold = 0f });
                MarkDirty();
            });
        }

        // ── Inspector helpers ─────────────────────────────────────────────────
        private static void InspectorHeader(string type, string value)
        {
            ImGui.SetCursorPosX(10);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.50f, 0.50f, 0.50f, 1f)); ImGui.Text(type.ToUpper()); ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.95f, 0.95f, 1f)); ImGui.Text(value); ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.24f, 0.24f, 0.24f, 1f)); ImGui.Separator(); ImGui.PopStyleColor();
            ImGui.Spacing();
        }
        private static void InspectorSection(string label)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.50f, 0.50f, 0.50f, 1f)); ImGui.SetCursorPosX(10); ImGui.Text(label); ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.24f, 0.24f, 0.24f, 1f)); ImGui.Separator(); ImGui.PopStyleColor();
            ImGui.Spacing();
        }
        private static void InspectorRow(string label)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f)); ImGui.Text($"{label}  "); ImGui.PopStyleColor(); ImGui.SameLine();
        }
        private void SmallIconBtn(string id, Action action)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.22f, 0.22f, 0.22f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.32f, 0.32f, 0.32f, 1f));
            if (ImGui.SmallButton(id)) action();
            ImGui.PopStyleColor(2);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Popups
        // ══════════════════════════════════════════════════════════════════════
        private void DrawPopups()
        {
            DrawNewStatePopup();
            DrawNewParamPopup();
            DrawNewLayerPopup();
            DrawRenameStatePopup();
            DrawDeleteStateConfirm();
            DrawDeleteParamConfirm();
            DrawDeleteLayerConfirm();
        }

        private void DrawNewStatePopup()
        {
            if (_showNewStatePopup) { ImGui.OpenPopup("New State##P"); _showNewStatePopup = false; }
            CenterPopup(420, 0);
            bool open = true;
            if (ImGui.BeginPopupModal("New State##P", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing();
                ImGui.Text($"Layer:  {CurrentLayer?.Name ?? "—"}");
                ImGui.Spacing();
                ImGui.Text("State Name"); ImGui.SetNextItemWidth(-1);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                bool enter = ImGui.InputText("##ns", ref _newStateName, 128, ImGuiInputTextFlags.EnterReturnsTrue);
                bool valid = !string.IsNullOrWhiteSpace(_newStateName) &&
                             !CurrentStates.Any(s => s.Name == _newStateName);
                if (!valid && !string.IsNullOrWhiteSpace(_newStateName))
                { ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.90f, 0.35f, 0.35f, 1f)); ImGui.Text("Name already exists."); ImGui.PopStyleColor(); }
                ImGui.Spacing();
                ImGui.Text("Clip GUID  (optional)"); ImGui.SetNextItemWidth(-1); ImGui.InputText("##ng", ref _newStateClipGuid, 128);
                ImGui.Text("Clip Name  (optional)"); ImGui.SetNextItemWidth(-1); ImGui.InputText("##nc", ref _newStateClipName, 128);
                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                if (!valid) ImGui.BeginDisabled();
                if (ImGui.Button("Create", new Vector2(120, 0)) || (enter && valid))
                { CreateState(_newStateName, _newStateClipGuid, _newStateClipName); ImGui.CloseCurrentPopup(); }
                if (!valid) ImGui.EndDisabled();
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0))) { _usePendingPos = false; ImGui.CloseCurrentPopup(); }
                ImGui.EndPopup();
            }
        }

        private void CreateState(string name, string clipGuid, string clipName)
        {
            if (CurrentLayer == null) return;
            CurrentLayer.States.Add(new AnimatorStateData
            { Name = name, ClipGuid = clipGuid, ClipName = clipName, Loop = true, Speed = 1f, Transitions = new() });

            string key = PosKey(name);
            if (_usePendingPos) { _nodePositions[key] = _pendingStatePos; _usePendingPos = false; }
            else
            {
                var rng = new Random();
                _nodePositions[key] = new Vector2(300f + (float)rng.NextDouble() * 250f, 200f + (float)rng.NextDouble() * 180f);
            }

            if (CurrentLayer.States.Count == 1) CurrentLayer.DefaultState = name;
            _selectedState = name;
            MarkDirty();
        }

        private void DrawNewParamPopup()
        {
            if (_showNewParamPopup) { ImGui.OpenPopup("New Parameter##P"); _showNewParamPopup = false; }
            CenterPopup(360, 0);
            bool open = true;
            if (ImGui.BeginPopupModal("New Parameter##P", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing();
                ImGui.Text("Name"); ImGui.SetNextItemWidth(-1);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                bool enter = ImGui.InputText("##np", ref _newParamName, 128, ImGuiInputTextFlags.EnterReturnsTrue);
                ImGui.Text("Type"); ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##npt", _newParamType.ToString()))
                {
                    foreach (ParameterType pt in Enum.GetValues<ParameterType>())
                        if (ImGui.Selectable(pt.ToString(), _newParamType == pt)) _newParamType = pt;
                    ImGui.EndCombo();
                }
                if (_newParamType is ParameterType.Float or ParameterType.Int)
                { ImGui.Text("Default Value"); ImGui.SetNextItemWidth(-1); ImGui.DragFloat("##nd", ref _newParamDefault, 0.1f); }
                bool valid = !string.IsNullOrWhiteSpace(_newParamName) && !_data.Parameters.Any(p => p.Name == _newParamName);
                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                if (!valid) ImGui.BeginDisabled();
                if (ImGui.Button("Create", new Vector2(120, 0)) || (enter && valid))
                { _data.Parameters.Add(new AnimatorParameterData { Name = _newParamName, Type = _newParamType, DefaultValue = _newParamDefault }); MarkDirty(); ImGui.CloseCurrentPopup(); }
                if (!valid) ImGui.EndDisabled();
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private void DrawNewLayerPopup()
        {
            if (_showNewLayerPopup) { ImGui.OpenPopup("New Layer##P"); _showNewLayerPopup = false; }
            CenterPopup(360, 0);
            bool open = true;
            if (ImGui.BeginPopupModal("New Layer##P", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing();
                ImGui.Text("Layer Name"); ImGui.SetNextItemWidth(-1);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                bool enter = ImGui.InputText("##nl", ref _newLayerName, 128, ImGuiInputTextFlags.EnterReturnsTrue);
                bool valid = !string.IsNullOrWhiteSpace(_newLayerName) &&
                             !_data.Layers.Any(l => l.Name == _newLayerName);
                if (!valid && !string.IsNullOrWhiteSpace(_newLayerName))
                { ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.90f, 0.35f, 0.35f, 1f)); ImGui.Text("Name already exists."); ImGui.PopStyleColor(); }
                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                if (!valid) ImGui.BeginDisabled();
                if (ImGui.Button("Create", new Vector2(120, 0)) || (enter && valid))
                {
                    _data.Layers.Add(new AnimatorLayerData
                    { Name = _newLayerName, Weight = 1f, BlendingMode = LayerBlendingMode.Override, States = new() });
                    _selectedLayer = _data.Layers.Count - 1;
                    int li = _selectedLayer;
                    EnsurePos($"{li}/{SpecialNodes.Entry}", 80, 140);
                    EnsurePos($"{li}/{SpecialNodes.AnyState}", 80, 260);
                    EnsurePos($"{li}/{SpecialNodes.Exit}", 80, 380);
                    MarkDirty(); ImGui.CloseCurrentPopup();
                }
                if (!valid) ImGui.EndDisabled();
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private void DrawRenameStatePopup()
        {
            if (_renamingState == null) return;
            ImGui.OpenPopup("Rename State##P");
            CenterPopup(340, 0);
            bool open = true;
            if (ImGui.BeginPopupModal("Rename State##P", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing();
                ImGui.Text($"Rename  \"{_renamingState}\""); ImGui.SetNextItemWidth(-1);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                bool enter = ImGui.InputText("##rn", ref _renameBuffer, 128, ImGuiInputTextFlags.EnterReturnsTrue);
                bool valid = !string.IsNullOrWhiteSpace(_renameBuffer) &&
                             (_renameBuffer == _renamingState || !CurrentStates.Any(s => s.Name == _renameBuffer));
                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                if (!valid) ImGui.BeginDisabled();
                if (ImGui.Button("Rename", new Vector2(120, 0)) || (enter && valid))
                { RenameState(_renamingState, _renameBuffer); _renamingState = null; ImGui.CloseCurrentPopup(); }
                if (!valid) ImGui.EndDisabled();
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0))) { _renamingState = null; ImGui.CloseCurrentPopup(); }
                ImGui.EndPopup();
            }
        }

        private void RenameState(string oldName, string newName)
        {
            var state = CurrentStates.FirstOrDefault(s => s.Name == oldName);
            if (state == null) return;
            state.Name = newName;
            foreach (var s in CurrentStates)
                foreach (var t in s.Transitions)
                    if (t.ToState == oldName) t.ToState = newName;
            if (CurrentDefaultState == oldName) CurrentDefaultState = newName;
            string oldKey = PosKey(oldName), newKey = PosKey(newName);
            if (_nodePositions.TryGetValue(oldKey, out var pos))
            { _nodePositions.Remove(oldKey); _nodePositions[newKey] = pos; }
            if (_selectedState == oldName) _selectedState = newName;
            MarkDirty();
        }

        private void DrawDeleteStateConfirm()
        {
            if (_showDeleteStateConfirm) { ImGui.OpenPopup("Delete State##C"); _showDeleteStateConfirm = false; }
            CenterPopup(340, 0);
            bool open = true;
            if (ImGui.BeginPopupModal("Delete State##C", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing(); ImGui.Text($"Delete  \"{_itemToDelete}\" ?");
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.82f, 0.42f, 0.42f, 1f));
                ImGui.Text("All transitions to and from it will be removed."); ImGui.PopStyleColor();
                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.52f, 0.12f, 0.12f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.70f, 0.18f, 0.18f, 1f));
                if (ImGui.Button("Delete", new Vector2(100, 0))) { DeleteState(_itemToDelete); ImGui.CloseCurrentPopup(); }
                ImGui.PopStyleColor(2);
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(100, 0))) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private void DeleteState(string name)
        {
            if (CurrentLayer == null) return;
            CurrentLayer.States.RemoveAll(s => s.Name == name);
            foreach (var s in CurrentLayer.States) s.Transitions.RemoveAll(t => t.ToState == name);
            _nodePositions.Remove(PosKey(name));
            if (CurrentDefaultState == name)
                CurrentDefaultState = CurrentLayer.States.Count > 0 ? CurrentLayer.States[0].Name : "";
            if (_selectedState == name) { _selectedState = null; _selectedTransitionFrom = null; _selectedTransitionIdx = -1; }
            MarkDirty();
        }

        private void DrawDeleteParamConfirm()
        {
            if (_showDeleteParamConfirm) { ImGui.OpenPopup("Delete Parameter##C"); _showDeleteParamConfirm = false; }
            CenterPopup(340, 0);
            bool open = true;
            if (ImGui.BeginPopupModal("Delete Parameter##C", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing(); ImGui.Text($"Delete parameter  \"{_itemToDelete}\" ?");
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.82f, 0.62f, 0.22f, 1f));
                ImGui.Text("Conditions referencing it will become orphaned."); ImGui.PopStyleColor();
                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.52f, 0.12f, 0.12f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.70f, 0.18f, 0.18f, 1f));
                if (ImGui.Button("Delete", new Vector2(100, 0)))
                { _data.Parameters.RemoveAll(p => p.Name == _itemToDelete); MarkDirty(); ImGui.CloseCurrentPopup(); }
                ImGui.PopStyleColor(2);
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(100, 0))) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private void DrawDeleteLayerConfirm()
        {
            if (_showDeleteLayerConfirm) { ImGui.OpenPopup("Delete Layer##C"); _showDeleteLayerConfirm = false; }
            CenterPopup(360, 0);
            bool open = true;
            if (ImGui.BeginPopupModal("Delete Layer##C", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                if (!int.TryParse(_itemToDelete, out int layerIdx) || layerIdx <= 0 || layerIdx >= _data.Layers.Count)
                { ImGui.CloseCurrentPopup(); }
                else
                {
                    string layerName = _data.Layers[layerIdx].Name;
                    ImGui.Spacing(); ImGui.Text($"Delete layer  \"{layerName}\" ?");
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.82f, 0.42f, 0.42f, 1f));
                    ImGui.Text("All states and transitions in it will be removed."); ImGui.PopStyleColor();
                    ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.52f, 0.12f, 0.12f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.70f, 0.18f, 0.18f, 1f));
                    if (ImGui.Button("Delete", new Vector2(100, 0)))
                    {
                        // Remove position keys for this layer; re-key layers above it
                        var keysToRemove = _nodePositions.Keys.Where(k => k.StartsWith($"{layerIdx}/")).ToList();
                        foreach (var k in keysToRemove) _nodePositions.Remove(k);

                        // Re-index position keys for all layers after the deleted one
                        for (int li = layerIdx + 1; li < _data.Layers.Count; li++)
                        {
                            var oldKeys = _nodePositions.Keys.Where(k => k.StartsWith($"{li}/")).ToList();
                            foreach (var ok in oldKeys)
                            {
                                var pos = _nodePositions[ok];
                                _nodePositions.Remove(ok);
                                _nodePositions[$"{li - 1}/{ok[(ok.IndexOf('/') + 1)..]}"] = pos;
                            }
                        }

                        _data.Layers.RemoveAt(layerIdx);
                        if (_selectedLayer >= _data.Layers.Count) _selectedLayer = _data.Layers.Count - 1;
                        _selectedState = null; _selectedTransitionFrom = null; _selectedTransitionIdx = -1;
                        MarkDirty(); ImGui.CloseCurrentPopup();
                    }
                    ImGui.PopStyleColor(2);
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel", new Vector2(100, 0))) ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Auto-layout / Fit View
        // ══════════════════════════════════════════════════════════════════════
        private void AutoLayout()
        {
            _nodePositions[PosKey(SpecialNodes.Entry)] = new Vector2(60, 80);
            _nodePositions[PosKey(SpecialNodes.AnyState)] = new Vector2(60, 200);
            _nodePositions[PosKey(SpecialNodes.Exit)] = new Vector2(60, 320);

            var states = CurrentStates;
            int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(states.Count)));
            for (int i = 0; i < states.Count; i++)
            {
                _nodePositions[PosKey(states[i].Name)] = new Vector2(
                    280f + (i % cols) * (NODE_W + 60),
                    80f + (i / cols) * (NODE_H + 60));
            }
            MarkDirty();
        }

        private void FitView()
        {
            var keys = AllNodeKeys().ToList();
            if (keys.Count == 0) return;
            float minX = float.MaxValue, minY = float.MaxValue;
            foreach (var k in keys)
            {
                if (!_nodePositions.TryGetValue(k, out var p)) continue;
                minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
            }
            _zoom = 1f; _canvasOffset = new Vector2(100, 80) - new Vector2(minX, minY);
        }

        private static void CenterPopup(float w, float h)
        {
            var vp = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (w > 0 || h > 0) ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Appearing);
        }
    }
}