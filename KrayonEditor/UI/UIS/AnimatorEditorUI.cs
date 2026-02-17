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
    public class AnimatorEditorUI : UIBehaviour
    {
        private Guid? _assetGuid;
        private string _assetPath;
        private AnimatorControllerData _data;
        private bool _isDirty = false;

        private Vector2 _canvasOffset = new Vector2(200, 100);
        private float _zoom = 1f;
        private const float ZOOM_MIN = 0.3f;
        private const float ZOOM_MAX = 2.5f;
        private bool _isPanning = false;

        private Dictionary<string, Vector2> _nodePositions = new();
        private const float NODE_W = 180f;
        private const float NODE_H = 56f;

        private string _draggingNode = null;
        private Vector2 _dragOffset;
        private string _selectedState = null;

        private string _connectingFrom = null;
        private bool _isConnecting = false;

        private string _selectedTransitionFrom = null;
        private int _selectedTransitionIdx = -1;

        private bool _showNewStatePopup = false;
        private string _newStateName = "NewState";
        private string _newStateClipGuid = "";
        private string _newStateClipName = "";

        private bool _showNewParamPopup = false;
        private string _newParamName = "NewParam";
        private ParameterType _newParamType = ParameterType.Float;
        private float _newParamDefault = 0f;

        private bool _showDeleteStateConfirm = false;
        private bool _showDeleteParamConfirm = false;
        private string _itemToDelete = "";

        private string _renamingState = null;
        private string _renameBuffer = "";

        private static readonly uint COL_BG = ImGui.ColorConvertFloat4ToU32(new Vector4(0.11f, 0.11f, 0.13f, 1f));
        private static readonly uint COL_GRID_MINOR = ImGui.ColorConvertFloat4ToU32(new Vector4(0.17f, 0.17f, 0.19f, 1f));
        private static readonly uint COL_GRID_MAJOR = ImGui.ColorConvertFloat4ToU32(new Vector4(0.22f, 0.22f, 0.26f, 1f));
        private static readonly uint COL_NODE_FILL = ImGui.ColorConvertFloat4ToU32(new Vector4(0.19f, 0.23f, 0.32f, 1f));
        private static readonly uint COL_NODE_HDR = ImGui.ColorConvertFloat4ToU32(new Vector4(0.25f, 0.32f, 0.48f, 1f));
        private static readonly uint COL_DEF_FILL = ImGui.ColorConvertFloat4ToU32(new Vector4(0.13f, 0.38f, 0.24f, 1f));
        private static readonly uint COL_DEF_HDR = ImGui.ColorConvertFloat4ToU32(new Vector4(0.18f, 0.52f, 0.32f, 1f));
        private static readonly uint COL_SEL_FILL = ImGui.ColorConvertFloat4ToU32(new Vector4(0.22f, 0.38f, 0.60f, 1f));
        private static readonly uint COL_SEL_HDR = ImGui.ColorConvertFloat4ToU32(new Vector4(0.30f, 0.50f, 0.80f, 1f));
        private static readonly uint COL_BORDER = ImGui.ColorConvertFloat4ToU32(new Vector4(0.35f, 0.42f, 0.60f, 1f));
        private static readonly uint COL_SEL_BOR = ImGui.ColorConvertFloat4ToU32(new Vector4(0.50f, 0.72f, 1.00f, 1f));
        private static readonly uint COL_DEF_BOR = ImGui.ColorConvertFloat4ToU32(new Vector4(0.28f, 0.78f, 0.50f, 1f));
        private static readonly uint COL_ARROW = ImGui.ColorConvertFloat4ToU32(new Vector4(0.50f, 0.56f, 0.72f, 1f));
        private static readonly uint COL_ARROW_SEL = ImGui.ColorConvertFloat4ToU32(new Vector4(1.00f, 0.75f, 0.15f, 1f));
        private static readonly uint COL_ARROW_HOV = ImGui.ColorConvertFloat4ToU32(new Vector4(0.80f, 0.85f, 1.00f, 1f));
        private static readonly uint COL_CONNECT = ImGui.ColorConvertFloat4ToU32(new Vector4(1.00f, 0.55f, 0.15f, 1f));
        private static readonly uint COL_TEXT_NODE = ImGui.ColorConvertFloat4ToU32(new Vector4(0.95f, 0.95f, 1.00f, 1f));
        private static readonly uint COL_TEXT_SUB = ImGui.ColorConvertFloat4ToU32(new Vector4(0.55f, 0.62f, 0.78f, 1f));
        private static readonly uint COL_CONN_DOT = ImGui.ColorConvertFloat4ToU32(new Vector4(0.48f, 0.64f, 1.00f, 1f));
        private static readonly uint COL_SHADOW = ImGui.ColorConvertFloat4ToU32(new Vector4(0.00f, 0.00f, 0.00f, 0.40f));

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
            _nodePositions.Clear();
            try
            {
                byte[] raw = AssetManager.GetBytes(guid);
                string json = System.Text.Encoding.UTF8.GetString(raw);
                _data = JsonSerializer.Deserialize<AnimatorControllerData>(json) ?? new AnimatorControllerData();
            }
            catch { _data = new AnimatorControllerData(); }
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
                    var dict = JsonSerializer.Deserialize<Dictionary<string, float[]>>(File.ReadAllText(LayoutPath));
                    if (dict != null)
                        foreach (var kv in dict)
                            _nodePositions[kv.Key] = new Vector2(kv.Value[0], kv.Value[1]);
                }
            }
            catch { }
            int idx = 0;
            foreach (var state in _data.States)
            {
                if (!_nodePositions.ContainsKey(state.Name))
                {
                    float angle = idx * MathF.PI * 2f / Math.Max(_data.States.Count, 1);
                    _nodePositions[state.Name] = new Vector2(400f + MathF.Cos(angle) * 200f, 300f + MathF.Sin(angle) * 200f);
                    idx++;
                }
            }
        }

        private void SaveNodePositions()
        {
            try
            {
                if (LayoutPath == null) return;
                var dict = _nodePositions.ToDictionary(kv => kv.Key, kv => new float[] { kv.Value.X, kv.Value.Y });
                File.WriteAllText(LayoutPath, JsonSerializer.Serialize(dict));
            }
            catch { }
        }

        private void Save()
        {
            if (_assetPath == null || _data == null) return;
            try
            {
                File.WriteAllText(Path.Combine(AssetManager.BasePath, _assetPath),
                    JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
                SaveNodePositions();
                _isDirty = false;
            }
            catch (Exception ex) { Console.WriteLine($"[AnimatorEditor] {ex.Message}"); }
        }

        private void MarkDirty() => _isDirty = true;

        public override void OnDrawUI()
        {
            if (!_isVisible || _data == null) return;
            string name = Path.GetFileNameWithoutExtension(_assetPath);
            string title = _isDirty
                ? $"Animator  -  {name}  [unsaved]###AnimatorEditorWindow"
                : $"Animator  -  {name}###AnimatorEditorWindow";

            ImGui.SetNextWindowSize(new Vector2(1200, 750), ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.Begin(title, ref _isVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            ImGui.PopStyleVar();

            DrawToolbar();

            float panelW = 280f;
            float canvasW = ImGui.GetContentRegionAvail().X - panelW - 1f;
            float height = ImGui.GetContentRegionAvail().Y;

            ImGui.BeginChild("AnimCanvas", new Vector2(canvasW, height), ImGuiChildFlags.None,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            DrawCanvas();
            ImGui.EndChild();

            ImGui.SameLine();

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.13f, 0.14f, 0.17f, 1f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 12));
            ImGui.BeginChild("AnimPanel", new Vector2(panelW, height), ImGuiChildFlags.None);
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
            DrawSidePanel();
            ImGui.EndChild();

            DrawPopups();
            ImGui.End();
        }

        private void DrawToolbar()
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.15f, 0.16f, 0.19f, 1f));
            ImGui.BeginChild("Toolbar", new Vector2(0, 38), ImGuiChildFlags.None);
            ImGui.PopStyleColor();
            ImGui.SetCursorPos(new Vector2(8, 6));

            if (_isDirty)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.28f, 0.08f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.70f, 0.36f, 0.10f, 1f));
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.16f, 0.36f, 0.18f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.22f, 0.48f, 0.24f, 1f));
            }
            if (ImGui.Button(_isDirty ? "  Save  *  " : "  Save  ", new Vector2(0, 26))) Save();
            ImGui.PopStyleColor(2);

            ImGui.SameLine(0, 8);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.25f, 0.38f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.28f, 0.35f, 0.52f, 1f));

            if (ImGui.Button("  New State  ", new Vector2(0, 26)))
            { _newStateName = "NewState"; _showNewStatePopup = true; }
            ImGui.SameLine(0, 4);
            if (ImGui.Button("  New Parameter  ", new Vector2(0, 26)))
            { _newParamName = "NewParam"; _newParamType = ParameterType.Float; _newParamDefault = 0f; _showNewParamPopup = true; }
            ImGui.SameLine(0, 4);
            if (ImGui.Button("  Reset View  ", new Vector2(0, 26)))
            { _canvasOffset = new Vector2(200, 100); _zoom = 1f; }

            ImGui.PopStyleColor(2);
            ImGui.SameLine(0, 16);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.35f, 0.38f, 0.48f, 1f));
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5f);
            ImGui.Text($"Zoom {_zoom:F2}x   |   Middle-drag: pan   |   Scroll: zoom   |   Right-click node: options");
            ImGui.PopStyleColor();
            ImGui.EndChild();
        }

        private void DrawCanvas()
        {
            var dl = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var size = ImGui.GetContentRegionAvail();

            dl.AddRectFilled(origin, origin + size, COL_BG);
            DrawGrid(dl, origin, size);

            // Detectar hover de bolitas ANTES del InvisibleButton
            var io = ImGui.GetIO();
            float dotR = 6f * _zoom;
            string hoveredOutNode = null;

            foreach (var state in _data.States)
            {
                Vector2 sp = NodePos(state.Name, origin);
                Vector2 sz = new Vector2(NODE_W * _zoom, NODE_H * _zoom);
                Vector2 outPt = sp + new Vector2(sz.X + dotR, sz.Y * 0.5f);
                if (Vector2.Distance(io.MousePos, outPt) < dotR * 2f)
                {
                    hoveredOutNode = state.Name;
                    break;
                }
            }

            // Iniciar conexión desde bolita output
            if (hoveredOutNode != null && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _isConnecting = true;
                _connectingFrom = hoveredOutNode;
                _draggingNode = null;
            }

            ImGui.InvisibleButton("canvas_bg", size,
                ImGuiButtonFlags.MouseButtonLeft |
                ImGuiButtonFlags.MouseButtonMiddle |
                ImGuiButtonFlags.MouseButtonRight);

            bool canvasHovered = ImGui.IsItemHovered() || hoveredOutNode != null;

            if (canvasHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Middle)) _isPanning = true;
            if (_isPanning)
            {
                _canvasOffset += io.MouseDelta;
                if (!ImGui.IsMouseDown(ImGuiMouseButton.Middle)) _isPanning = false;
            }

            if (canvasHovered && io.MouseWheel != 0)
            {
                float oldZ = _zoom;
                _zoom = Math.Clamp(_zoom + io.MouseWheel * 0.08f, ZOOM_MIN, ZOOM_MAX);
                Vector2 mc = (io.MousePos - origin - _canvasOffset) / oldZ;
                _canvasOffset -= mc * (_zoom - oldZ);
            }

            dl.PushClipRect(origin, origin + size, true);

            foreach (var state in _data.States)
                foreach (var t in state.Transitions)
                    DrawArrow(dl, origin, state.Name, t.ToState,
                        _selectedTransitionFrom == state.Name &&
                        state.Transitions.IndexOf(t) == _selectedTransitionIdx);

            if (_isConnecting && _connectingFrom != null)
            {
                Vector2 sp = NodePos(_connectingFrom, origin);
                Vector2 sz = new Vector2(NODE_W * _zoom, NODE_H * _zoom);
                Vector2 outPt = sp + new Vector2(sz.X + dotR, sz.Y * 0.5f);
                dl.AddLine(outPt, io.MousePos, COL_CONNECT, 2f);
                dl.AddCircleFilled(io.MousePos, 4f, COL_CONNECT);
            }

            foreach (var state in _data.States)
                DrawNode(dl, origin, state, canvasHovered);

            dl.PopClipRect();

            // Clic en fondo vacío → deseleccionar
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && hoveredOutNode == null)
            {
                if (!_data.States.Any(s => IsOverNode(s.Name, origin)))
                {
                    _selectedState = null;
                    _selectedTransitionFrom = null;
                    _selectedTransitionIdx = -1;
                    _isConnecting = false;
                }
            }

            if (_isConnecting && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                _isConnecting = false;
                _connectingFrom = null;
            }

            if (_isConnecting && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                foreach (var state in _data.States)
                {
                    if (state.Name == _connectingFrom) continue;
                    Vector2 sp = NodePos(state.Name, origin);
                    Vector2 sz = new Vector2(NODE_W * _zoom, NODE_H * _zoom);
                    Vector2 inPt = sp + new Vector2(-dotR, sz.Y * 0.5f);
                    bool onInput = Vector2.Distance(io.MousePos, inPt) < dotR * 2.5f;
                    bool onNode = IsOverNode(state.Name, origin);
                    if (onInput || onNode)
                    { AddTransition(_connectingFrom, state.Name); break; }
                }
                _isConnecting = false;
                _connectingFrom = null;
            }
        }

        private void DrawGrid(ImDrawListPtr dl, Vector2 origin, Vector2 size)
        {
            float minor = 24f * _zoom;
            float major = 120f * _zoom;
            float ox = _canvasOffset.X % minor; float oy = _canvasOffset.Y % minor;
            float Ox = _canvasOffset.X % major; float Oy = _canvasOffset.Y % major;
            for (float x = ox; x < size.X; x += minor) dl.AddLine(origin + new Vector2(x, 0), origin + new Vector2(x, size.Y), COL_GRID_MINOR, 0.5f);
            for (float y = oy; y < size.Y; y += minor) dl.AddLine(origin + new Vector2(0, y), origin + new Vector2(size.X, y), COL_GRID_MINOR, 0.5f);
            for (float x = Ox; x < size.X; x += major) dl.AddLine(origin + new Vector2(x, 0), origin + new Vector2(x, size.Y), COL_GRID_MAJOR, 1f);
            for (float y = Oy; y < size.Y; y += major) dl.AddLine(origin + new Vector2(0, y), origin + new Vector2(size.X, y), COL_GRID_MAJOR, 1f);
        }

        private Vector2 ToScreen(Vector2 c, Vector2 origin) => origin + _canvasOffset + c * _zoom;
        private Vector2 ToCanvas(Vector2 s, Vector2 origin) => (s - origin - _canvasOffset) / _zoom;
        private Vector2 NodePos(string n, Vector2 origin) { _nodePositions.TryGetValue(n, out var p); return ToScreen(p, origin); }
        private Vector2 NodeCenter(string n, Vector2 origin) => NodePos(n, origin) + new Vector2(NODE_W * _zoom * 0.5f, NODE_H * _zoom * 0.5f);
        private bool IsOverNode(string n, Vector2 origin) { var sp = NodePos(n, origin); var mp = ImGui.GetIO().MousePos; return mp.X >= sp.X && mp.X <= sp.X + NODE_W * _zoom && mp.Y >= sp.Y && mp.Y <= sp.Y + NODE_H * _zoom; }

        private void DrawNode(ImDrawListPtr dl, Vector2 origin, AnimatorStateData state, bool canvasHovered)
        {
            string name = state.Name;
            bool isDef = name == _data.DefaultState;
            bool isSel = name == _selectedState;

            Vector2 sp = NodePos(name, origin);
            Vector2 sz = new Vector2(NODE_W * _zoom, NODE_H * _zoom);
            float hdrH = 22f * _zoom;
            float r = 5f * _zoom;

            uint fill = isSel ? COL_SEL_FILL : (isDef ? COL_DEF_FILL : COL_NODE_FILL);
            uint hdr = isSel ? COL_SEL_HDR : (isDef ? COL_DEF_HDR : COL_NODE_HDR);
            uint border = isSel ? COL_SEL_BOR : (isDef ? COL_DEF_BOR : COL_BORDER);

            dl.AddRectFilled(sp + new Vector2(4, 5), sp + sz + new Vector2(4, 5), COL_SHADOW, r);
            dl.AddRectFilled(sp, sp + sz, fill, r);
            dl.AddRectFilled(sp, sp + new Vector2(sz.X, hdrH), hdr, r);
            dl.AddRectFilled(sp + new Vector2(0, hdrH - r), sp + new Vector2(sz.X, hdrH), hdr);
            dl.AddRect(sp, sp + sz, border, r, ImDrawFlags.None, isSel ? 2f : 1f);

            string label = name.Length > 20 ? name[..20] + "..." : name;
            Vector2 lsz = ImGui.CalcTextSize(label);
            dl.AddText(sp + new Vector2((sz.X - lsz.X) * 0.5f, (hdrH - lsz.Y) * 0.5f), COL_TEXT_NODE, label);

            if (!string.IsNullOrEmpty(state.ClipName))
            {
                string sub = state.ClipName.Length > 22 ? state.ClipName[..22] + "..." : state.ClipName;
                Vector2 ssz = ImGui.CalcTextSize(sub);
                dl.AddText(sp + new Vector2((sz.X - ssz.X) * 0.5f, hdrH + (sz.Y - hdrH - ssz.Y) * 0.5f), COL_TEXT_SUB, sub);
            }

            if (isDef)
            {
                float cy = sp.Y + sz.Y * 0.5f;
                dl.AddTriangleFilled(
                    new Vector2(sp.X - 14f * _zoom, cy),
                    new Vector2(sp.X - 4f * _zoom, cy - 6f * _zoom),
                    new Vector2(sp.X - 4f * _zoom, cy + 6f * _zoom), COL_DEF_BOR);
            }

            // Bolita OUTPUT (derecha)
            float dotR = 6f * _zoom;
            Vector2 outPt = sp + new Vector2(sz.X + dotR, sz.Y * 0.5f);
            // Bolita INPUT (izquierda)
            Vector2 inPt = sp + new Vector2(-dotR, sz.Y * 0.5f);

            bool outHov = Vector2.Distance(ImGui.GetIO().MousePos, outPt) < dotR * 1.8f;
            bool inHov = Vector2.Distance(ImGui.GetIO().MousePos, inPt) < dotR * 1.8f;

            uint outCol = outHov ? COL_CONNECT : COL_CONN_DOT;
            uint inCol = inHov ? COL_CONNECT : COL_CONN_DOT;

            dl.AddCircleFilled(outPt, dotR, outCol);
            dl.AddCircle(outPt, dotR, border, 16, 1.5f);
            dl.AddCircleFilled(inPt, dotR, inCol);
            dl.AddCircle(inPt, dotR, border, 16, 1.5f);

            var io = ImGui.GetIO();
            bool over = IsOverNode(name, origin);

            if (canvasHovered)
            {
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    // Solo mover/seleccionar — la bolita output ya se maneja en DrawCanvas
                    if (over && Vector2.Distance(io.MousePos, outPt) >= dotR * 2f)
                    {
                        _selectedState = name;
                        _selectedTransitionFrom = null;
                        _selectedTransitionIdx = -1;
                        _draggingNode = name;
                        _dragOffset = io.MousePos - sp;
                    }
                    else if (over)
                    {
                        _selectedState = name;
                        _selectedTransitionFrom = null;
                        _selectedTransitionIdx = -1;
                    }
                }

                if (over && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                { _selectedState = name; ImGui.OpenPopup($"NodeCtx_{name}"); }
            }

            if (_draggingNode == name)
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                { _nodePositions[name] = ToCanvas(io.MousePos - _dragOffset, origin); MarkDirty(); }
                else _draggingNode = null;
            }

            if (ImGui.BeginPopup($"NodeCtx_{name}"))
            {
                ImGui.TextDisabled(name);
                ImGui.Separator();
                if (name != _data.DefaultState && ImGui.MenuItem("Set as Default")) { _data.DefaultState = name; MarkDirty(); }
                if (ImGui.MenuItem("Connect Transition")) { _isConnecting = true; _connectingFrom = name; }
                if (ImGui.MenuItem("Rename")) { _renamingState = name; _renameBuffer = name; }
                ImGui.Separator();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.38f, 0.38f, 1f));
                if (ImGui.MenuItem("Delete")) { _itemToDelete = name; _showDeleteStateConfirm = true; }
                ImGui.PopStyleColor();
                ImGui.EndPopup();
            }
        }

        private void DrawArrow(ImDrawListPtr dl, Vector2 origin, string fromName, string toName, bool selected)
        {
            if (!_nodePositions.ContainsKey(fromName) || !_nodePositions.ContainsKey(toName)) return;

            float dotR = 6f * _zoom;

            Vector2 spFrom = NodePos(fromName, origin);
            Vector2 szFrom = new Vector2(NODE_W * _zoom, NODE_H * _zoom);
            Vector2 spTo = NodePos(toName, origin);
            Vector2 szTo = new Vector2(NODE_W * _zoom, NODE_H * _zoom);

            Vector2 outPt = spFrom + new Vector2(szFrom.X + dotR, szFrom.Y * 0.5f);
            Vector2 inPt = spTo + new Vector2(-dotR, szTo.Y * 0.5f);

            if (fromName == toName)
            {
                float lr = 28f * _zoom;
                dl.AddCircle(outPt + new Vector2(0, -lr), lr, selected ? COL_ARROW_SEL : COL_ARROW, 32, 2f);
                return;
            }

            Vector2 dir = Vector2.Normalize(inPt - outPt);
            Vector2 perp = new Vector2(-dir.Y, dir.X);

            bool bidir = _data.States.Any(s => s.Name == toName && s.Transitions.Any(t => t.ToState == fromName));
            float off = bidir ? 7f * _zoom : 0f;

            Vector2 fe = outPt + perp * off;
            Vector2 te = inPt + perp * off;

            float dist = DistToSeg(ImGui.GetIO().MousePos, fe, te);
            bool hov = dist < 8f;
            uint col = selected ? COL_ARROW_SEL : (hov ? COL_ARROW_HOV : COL_ARROW);

            dl.AddLine(fe, te, col, selected ? 2.5f : 1.5f);

            float as2 = 10f * _zoom;
            Vector2 arrowDir = Vector2.Normalize(te - fe);
            Vector2 ab = te - arrowDir * as2;
            dl.AddTriangleFilled(te, ab + perp * as2 * 0.42f, ab - perp * as2 * 0.42f, col);

            if (hov && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                var fs = _data.States.FirstOrDefault(s => s.Name == fromName);
                if (fs != null)
                    for (int i = 0; i < fs.Transitions.Count; i++)
                        if (fs.Transitions[i].ToState == toName)
                        { _selectedTransitionFrom = fromName; _selectedTransitionIdx = i; _selectedState = fromName; break; }
            }
        }

        private static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab), 0f, 1f);
            return Vector2.Distance(p, a + t * ab);
        }

        private void AddTransition(string from, string to)
        {
            var state = _data.States.FirstOrDefault(s => s.Name == from);
            if (state == null || state.Transitions.Any(t => t.ToState == to)) return;
            state.Transitions.Add(new StateTransitionData { ToState = to, Duration = 0.2f, CanInterrupt = true, Conditions = new() });
            MarkDirty();
        }

        private void DrawSidePanel()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.72f, 1.00f, 1f));
            ImGui.Text("PARAMETERS");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 34f);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.25f, 0.38f, 1f));
            if (ImGui.SmallButton(" + ##addp")) { _newParamName = "NewParam"; _showNewParamPopup = true; }
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.25f, 0.28f, 0.38f, 1f));
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();

            for (int i = 0; i < _data.Parameters.Count; i++)
            {
                var p = _data.Parameters[i];
                ImGui.PushID($"param_{i}");
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.18f, 0.23f, 1f));
                ImGui.BeginChild($"pc_{i}", new Vector2(-1, 52));
                ImGui.PopStyleColor();
                ImGui.SetCursorPos(new Vector2(8, 6));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.42f, 0.52f, 0.80f, 1f));
                ImGui.Text($"[{p.Type}]");
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.Text(p.Name);
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - 22f);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.25f, 0.25f, 1f));
                if (ImGui.SmallButton("x##dp")) { _itemToDelete = p.Name; _showDeleteParamConfirm = true; }
                ImGui.PopStyleColor();
                ImGui.SetCursorPos(new Vector2(8, 30));
                ImGui.SetNextItemWidth(-8);
                float dv = p.DefaultValue;
                switch (p.Type)
                {
                    case ParameterType.Float:
                        if (ImGui.DragFloat("##pv", ref dv, 0.01f)) { p.DefaultValue = dv; MarkDirty(); }
                        break;
                    case ParameterType.Int:
                        int iv = (int)dv;
                        if (ImGui.DragInt("##pv", ref iv)) { p.DefaultValue = iv; MarkDirty(); }
                        break;
                    case ParameterType.Bool:
                        bool bv = dv != 0f;
                        if (ImGui.Checkbox("##pv", ref bv)) { p.DefaultValue = bv ? 1f : 0f; MarkDirty(); }
                        break;
                    case ParameterType.Trigger:
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.45f, 0.45f, 0.55f, 1f));
                        ImGui.Text("trigger");
                        ImGui.PopStyleColor();
                        break;
                }
                ImGui.EndChild();
                ImGui.PopID();
                ImGui.Spacing();
            }

            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.25f, 0.28f, 0.38f, 1f));
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();

            if (_selectedState == null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.32f, 0.35f, 0.45f, 1f));
                ImGui.Text("Select a state to edit it.");
                ImGui.PopStyleColor();
                return;
            }

            var sd = _data.States.FirstOrDefault(s => s.Name == _selectedState);
            if (sd == null) return;

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.50f, 0.85f, 0.60f, 1f));
            ImGui.Text("STATE");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 0.50f, 1f));
            ImGui.Text(sd.Name);
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.25f, 0.28f, 0.38f, 1f));
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.18f, 0.23f, 1f));
            ImGui.BeginChild("clipBlock", new Vector2(-1, 90));
            ImGui.PopStyleColor();
            ImGui.SetCursorPos(new Vector2(8, 8));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.48f, 0.54f, 0.72f, 1f));
            ImGui.Text("Clip GUID  (drag FBX here)");
            ImGui.PopStyleColor();
            ImGui.SetCursorPos(new Vector2(8, 28));
            ImGui.SetNextItemWidth(-8);
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
            ImGui.SetCursorPos(new Vector2(8, 56));
            if (!string.IsNullOrEmpty(sd.ClipGuid) && Guid.TryParse(sd.ClipGuid, out Guid pg))
            {
                var rec = AssetManager.Get(pg);
                ImGui.PushStyleColor(ImGuiCol.Text, rec != null ? new Vector4(0.32f, 0.78f, 0.42f, 1f) : new Vector4(0.78f, 0.28f, 0.28f, 1f));
                ImGui.Text(rec != null ? $"  {Path.GetFileName(rec.Path)}" : "  Asset not found");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.32f, 0.35f, 0.45f, 1f));
                ImGui.Text("  Drag an FBX from the Assets panel");
                ImGui.PopStyleColor();
            }
            ImGui.EndChild();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.18f, 0.23f, 1f));
            ImGui.BeginChild("clipNameBlock", new Vector2(-1, 52));
            ImGui.PopStyleColor();
            ImGui.SetCursorPos(new Vector2(8, 6));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.48f, 0.54f, 0.72f, 1f));
            ImGui.Text("Clip Name  (animation inside FBX)");
            ImGui.PopStyleColor();
            ImGui.SetCursorPos(new Vector2(8, 28));
            ImGui.SetNextItemWidth(-8);
            string clipName = sd.ClipName ?? "";
            if (ImGui.InputText("##cname", ref clipName, 128)) { sd.ClipName = clipName; MarkDirty(); }
            ImGui.EndChild();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.18f, 0.23f, 1f));
            ImGui.BeginChild("propsBlock", new Vector2(-1, 38));
            ImGui.PopStyleColor();
            ImGui.SetCursorPos(new Vector2(8, 10));
            bool loop = sd.Loop;
            if (ImGui.Checkbox("Loop", ref loop)) { sd.Loop = loop; MarkDirty(); }
            ImGui.SameLine(100);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.48f, 0.54f, 0.72f, 1f));
            ImGui.Text("Speed");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(70);
            float speed = sd.Speed;
            if (ImGui.DragFloat("##spd", ref speed, 0.01f, 0f, 10f)) { sd.Speed = speed; MarkDirty(); }
            ImGui.EndChild();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.72f, 1.00f, 1f));
            ImGui.Text("TRANSITIONS");
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.25f, 0.28f, 0.38f, 1f));
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();

            for (int ti = 0; ti < sd.Transitions.Count; ti++)
            {
                var t = sd.Transitions[ti];
                ImGui.PushID($"tr_{ti}");

                bool tsel = _selectedTransitionFrom == _selectedState && _selectedTransitionIdx == ti;

                // Fila manual: flecha expandir + label + X
                bool isOpen = tsel;
                string arrow = isOpen ? "v" : ">";

                ImGui.PushStyleColor(ImGuiCol.Button, tsel
                    ? new Vector4(0.24f, 0.32f, 0.54f, 1f)
                    : new Vector4(0.18f, 0.20f, 0.28f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.30f, 0.38f, 0.60f, 1f));

                float xBtnW = 28f;
                float rowW = ImGui.GetContentRegionAvail().X - xBtnW - 4f;

                if (ImGui.Button($"{arrow}  to  {t.ToState}##trbtn{ti}", new Vector2(rowW, 0)))
                {
                    if (tsel)
                    {
                        _selectedTransitionFrom = null;
                        _selectedTransitionIdx = -1;
                    }
                    else
                    {
                        _selectedTransitionFrom = _selectedState;
                        _selectedTransitionIdx = ti;
                    }
                }
                ImGui.PopStyleColor(2);

                ImGui.SameLine(0, 4);

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.50f, 0.12f, 0.12f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.75f, 0.18f, 0.18f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.75f, 0.75f, 1f));

                if (ImGui.Button($"X##xtr{ti}", new Vector2(xBtnW, 0)))
                {
                    sd.Transitions.RemoveAt(ti);
                    if (_selectedTransitionIdx == ti)
                    {
                        _selectedTransitionFrom = null;
                        _selectedTransitionIdx = -1;
                    }
                    MarkDirty();
                    ImGui.PopStyleColor(3);
                    ImGui.PopID();
                    break;
                }
                ImGui.PopStyleColor(3);

                // Cuerpo expandido
                if (tsel)
                {
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.14f, 0.16f, 0.20f, 1f));
                    ImGui.BeginChild($"trb_{ti}", new Vector2(-1, 0), ImGuiChildFlags.Borders | ImGuiChildFlags.AutoResizeY);
                    ImGui.PopStyleColor();
                    ImGui.Spacing();
                    ImGui.SetCursorPosX(8);

                    float dur = t.Duration;
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.48f, 0.54f, 0.72f, 1f));
                    ImGui.Text("Blend");
                    ImGui.PopStyleColor();
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(70);
                    if (ImGui.DragFloat("##dur", ref dur, 0.01f, 0f, 5f)) { t.Duration = dur; MarkDirty(); }
                    ImGui.SameLine(0, 16);
                    bool ci = t.CanInterrupt;
                    if (ImGui.Checkbox("Interrupt", ref ci)) { t.CanInterrupt = ci; MarkDirty(); }

                    ImGui.SetCursorPosX(8);
                    bool het = t.HasExitTime;
                    if (ImGui.Checkbox("Exit Time", ref het)) { t.HasExitTime = het; MarkDirty(); }
                    if (t.HasExitTime)
                    {
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(70);
                        float et = t.ExitTime;
                        if (ImGui.DragFloat("##et", ref et, 0.01f, 0f, 1f)) { t.ExitTime = et; MarkDirty(); }
                    }

                    if (t.Conditions.Count > 0)
                    {
                        ImGui.Spacing();
                        ImGui.SetCursorPosX(8);
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.78f, 0.68f, 0.35f, 1f));
                        ImGui.Text("Conditions");
                        ImGui.PopStyleColor();
                    }

                    for (int ci2 = 0; ci2 < t.Conditions.Count; ci2++)
                    {
                        var cond = t.Conditions[ci2];
                        ImGui.PushID($"c_{ci2}");
                        ImGui.SetCursorPosX(8);
                        ImGui.SetNextItemWidth(88);
                        if (ImGui.BeginCombo("##cp", cond.Parameter ?? "---"))
                        {
                            foreach (var param in _data.Parameters)
                                if (ImGui.Selectable(param.Name, cond.Parameter == param.Name))
                                { cond.Parameter = param.Name; MarkDirty(); }
                            ImGui.EndCombo();
                        }
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(68);
                        if (ImGui.BeginCombo("##cm", cond.Mode.ToString()))
                        {
                            foreach (ConditionMode m in Enum.GetValues<ConditionMode>())
                                if (ImGui.Selectable(m.ToString(), cond.Mode == m))
                                { cond.Mode = m; MarkDirty(); }
                            ImGui.EndCombo();
                        }
                        if (cond.Mode is ConditionMode.Greater or ConditionMode.Less or ConditionMode.Equals)
                        {
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(48);
                            float thr = cond.Threshold;
                            if (ImGui.DragFloat("##ct", ref thr, 0.1f)) { cond.Threshold = thr; MarkDirty(); }
                        }
                        ImGui.SameLine();
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.25f, 0.25f, 1f));
                        if (ImGui.SmallButton($"x##dc{ci2}"))
                        {
                            t.Conditions.RemoveAt(ci2);
                            MarkDirty();
                            ImGui.PopStyleColor();
                            ImGui.PopID();
                            break;
                        }
                        ImGui.PopStyleColor();
                        ImGui.PopID();
                    }

                    ImGui.Spacing();
                    ImGui.SetCursorPosX(8);
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.18f, 0.22f, 0.35f, 1f));
                    if (ImGui.SmallButton("+ Add Condition"))
                    {
                        t.Conditions.Add(new TransitionConditionData
                        {
                            Parameter = _data.Parameters.Count > 0 ? _data.Parameters[0].Name : "",
                            Mode = ConditionMode.Greater,
                            Threshold = 0f
                        });
                        MarkDirty();
                    }
                    ImGui.PopStyleColor();
                    ImGui.Spacing();
                    ImGui.EndChild();
                }

                ImGui.PopID();
                ImGui.Spacing();
            }

            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.25f, 0.38f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.28f, 0.35f, 0.52f, 1f));
            if (ImGui.Button("+ Connect Transition", new Vector2(-1, 0)))
            { _isConnecting = true; _connectingFrom = _selectedState; }
            ImGui.PopStyleColor(2);
        }

        private void DrawPopups()
        {
            DrawNewStatePopup();
            DrawNewParamPopup();
            DrawDeleteStateConfirm();
            DrawDeleteParamConfirm();
            DrawRenameStatePopup();
        }

        private void DrawNewStatePopup()
        {
            if (_showNewStatePopup) { ImGui.OpenPopup("New State##P"); _showNewStatePopup = false; }
            CenterPopup(420, 0);
            bool open = true;
            if (ImGui.BeginPopupModal("New State##P", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing();
                ImGui.Text("State name:");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                bool enter = ImGui.InputText("##ns", ref _newStateName, 128, ImGuiInputTextFlags.EnterReturnsTrue);
                bool valid = !string.IsNullOrWhiteSpace(_newStateName) && !_data.States.Any(s => s.Name == _newStateName);
                ImGui.Spacing();
                ImGui.Text("Clip GUID (optional):");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##ng", ref _newStateClipGuid, 128);
                ImGui.Text("Clip Name (optional):");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##nc", ref _newStateClipName, 128);
                if (!valid && !string.IsNullOrWhiteSpace(_newStateName))
                { ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.38f, 0.38f, 1f)); ImGui.Text("Name already exists."); ImGui.PopStyleColor(); }
                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                if (!valid) ImGui.BeginDisabled();
                if (ImGui.Button("Create", new Vector2(120, 0)) || (enter && valid))
                { CreateState(_newStateName, _newStateClipGuid, _newStateClipName); ImGui.CloseCurrentPopup(); }
                if (!valid) ImGui.EndDisabled();
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private void CreateState(string name, string clipGuid, string clipName)
        {
            _data.States.Add(new AnimatorStateData { Name = name, ClipGuid = clipGuid, ClipName = clipName, Loop = true, Speed = 1f, Transitions = new() });
            var rng = new Random();
            _nodePositions[name] = new Vector2(300f + (float)rng.NextDouble() * 200f, 200f + (float)rng.NextDouble() * 200f);
            if (_data.States.Count == 1) _data.DefaultState = name;
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
                ImGui.Text("Name:");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                bool enter = ImGui.InputText("##np", ref _newParamName, 128, ImGuiInputTextFlags.EnterReturnsTrue);
                ImGui.Text("Type:");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##npt", _newParamType.ToString()))
                {
                    foreach (ParameterType pt in Enum.GetValues<ParameterType>())
                        if (ImGui.Selectable(pt.ToString(), _newParamType == pt)) _newParamType = pt;
                    ImGui.EndCombo();
                }
                if (_newParamType is ParameterType.Float or ParameterType.Int)
                { ImGui.Text("Default value:"); ImGui.SetNextItemWidth(-1); ImGui.DragFloat("##nd", ref _newParamDefault, 0.1f); }
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

        private void DrawRenameStatePopup()
        {
            if (_renamingState == null) return;
            ImGui.OpenPopup("Rename State##P");
            CenterPopup(340, 0);
            bool open = true;
            if (ImGui.BeginPopupModal("Rename State##P", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing();
                ImGui.Text($"Rename  \"{_renamingState}\"  to:");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                bool enter = ImGui.InputText("##rn", ref _renameBuffer, 128, ImGuiInputTextFlags.EnterReturnsTrue);
                bool valid = !string.IsNullOrWhiteSpace(_renameBuffer) && (_renameBuffer == _renamingState || !_data.States.Any(s => s.Name == _renameBuffer));
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
            var state = _data.States.FirstOrDefault(s => s.Name == oldName);
            if (state == null) return;
            state.Name = newName;
            foreach (var s in _data.States) foreach (var t in s.Transitions) if (t.ToState == oldName) t.ToState = newName;
            if (_data.DefaultState == oldName) _data.DefaultState = newName;
            if (_nodePositions.TryGetValue(oldName, out var pos)) { _nodePositions.Remove(oldName); _nodePositions[newName] = pos; }
            if (_selectedState == oldName) _selectedState = newName;
            MarkDirty();
        }

        private void DrawDeleteStateConfirm()
        {
            if (_showDeleteStateConfirm) { ImGui.OpenPopup("Delete State##C"); _showDeleteStateConfirm = false; }
            CenterPopup(320, 0);
            bool open = true;
            if (ImGui.BeginPopupModal("Delete State##C", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing();
                ImGui.Text($"Delete state  \"{_itemToDelete}\" ?");
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.85f, 0.42f, 0.42f, 1f));
                ImGui.Text("All transitions to and from it will be removed.");
                ImGui.PopStyleColor();
                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.58f, 0.14f, 0.14f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.76f, 0.20f, 0.20f, 1f));
                if (ImGui.Button("Delete", new Vector2(100, 0))) { DeleteState(_itemToDelete); ImGui.CloseCurrentPopup(); }
                ImGui.PopStyleColor(2);
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(100, 0))) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private void DeleteState(string name)
        {
            _data.States.RemoveAll(s => s.Name == name);
            foreach (var s in _data.States) s.Transitions.RemoveAll(t => t.ToState == name);
            _nodePositions.Remove(name);
            if (_data.DefaultState == name) _data.DefaultState = _data.States.Count > 0 ? _data.States[0].Name : "";
            if (_selectedState == name) { _selectedState = null; _selectedTransitionFrom = null; _selectedTransitionIdx = -1; }
            MarkDirty();
        }

        private void DrawDeleteParamConfirm()
        {
            if (_showDeleteParamConfirm) { ImGui.OpenPopup("Delete Parameter##C"); _showDeleteParamConfirm = false; }
            CenterPopup(320, 0);
            bool open = true;
            if (ImGui.BeginPopupModal("Delete Parameter##C", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing();
                ImGui.Text($"Delete parameter  \"{_itemToDelete}\" ?");
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.85f, 0.65f, 0.22f, 1f));
                ImGui.Text("Conditions referencing it will be orphaned.");
                ImGui.PopStyleColor();
                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.58f, 0.14f, 0.14f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.76f, 0.20f, 0.20f, 1f));
                if (ImGui.Button("Delete", new Vector2(100, 0))) { _data.Parameters.RemoveAll(p => p.Name == _itemToDelete); MarkDirty(); ImGui.CloseCurrentPopup(); }
                ImGui.PopStyleColor(2);
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(100, 0))) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private static void CenterPopup(float w, float h)
        {
            var vp = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (w > 0 || h > 0) ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Appearing);
        }
    }
}