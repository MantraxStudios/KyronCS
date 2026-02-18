using ImGuiNET;
using KrayonCore.Core.Attributes;
using KrayonCore.Graphics.GameUI;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Alias explícitos para evitar ambigüedad total
using NVec2 = System.Numerics.Vector2;
using NVec4 = System.Numerics.Vector4;

namespace KrayonCore.Editor.Panels
{
    public class UICanvasEditor
    {
        // ── Estado ────────────────────────────────────────────────────────
        private Guid _assetGuid = Guid.Empty;
        private string _assetPath = string.Empty;
        private bool _isDirty = false;
        private string _statusMessage = string.Empty;
        private float _statusTimer = 0f;

        // ── Canvas settings ───────────────────────────────────────────────
        private string _canvasName = "canvas";
        private int _sortOrder = 0;
        private bool _canvasVisible = true;
        private int _scaleModeIdx = 0;
        private float _refWidth = 1920f;
        private float _refHeight = 1080f;

        private static readonly string[] ScaleModes = { "Fit", "Fill", "Stretch" };

        // ── Elementos ─────────────────────────────────────────────────────
        private readonly List<ElemData> _elements = new();
        private int _selectedIndex = -1;

        // ── API pública ───────────────────────────────────────────────────

        public bool IsOpen => _assetGuid != Guid.Empty;

        public void OpenAsset(Guid assetGuid)
        {
            _assetGuid = assetGuid;
            var record = AssetManager.Get(assetGuid);
            if (record is null) { SetStatus("Asset no encontrado"); return; }
            _assetPath = System.IO.Path.Combine(AssetManager.BasePath, record.Path);
            LoadFromDisk();
        }

        public void NewCanvas(Guid assetGuid, string canvasName = "canvas")
        {
            _assetGuid = assetGuid;
            var record = AssetManager.Get(assetGuid);
            _assetPath = record is not null
                ? System.IO.Path.Combine(AssetManager.BasePath, record.Path)
                : string.Empty;
            _canvasName = canvasName;
            _sortOrder = 0;
            _canvasVisible = true;
            _scaleModeIdx = 0;
            _refWidth = 1920f;
            _refHeight = 1080f;
            _elements.Clear();
            _selectedIndex = -1;
            _isDirty = true;
            SetStatus("Nuevo canvas creado");
        }

        public void Close()
        {
            _assetGuid = Guid.Empty;
            _elements.Clear();
            _selectedIndex = -1;
        }

        // ── Render ────────────────────────────────────────────────────────

        public void OnImGui(float deltaTime)
        {
            if (!IsOpen) return;
            _statusTimer -= deltaTime;

            DrawToolbar();
            ImGui.Separator();

            ImGui.Columns(2, "uiEditorCols", true);
            ImGui.SetColumnWidth(0, 230f);
            DrawHierarchy();
            ImGui.NextColumn();
            DrawInspector();
            ImGui.Columns(1);
        }

        // ── Toolbar ───────────────────────────────────────────────────────

        private void DrawToolbar()
        {
            ImGui.Text($"UI Editor — {_canvasName}{(_isDirty ? " *" : "")}");
            ImGui.SameLine();

            float btnX = ImGui.GetWindowWidth() - 160f;
            ImGui.SetCursorPosX(btnX);

            if (ImGui.Button("Save", new NVec2(70, 0)))
                SaveToDisk();

            ImGui.SameLine();
            if (ImGui.Button("Reload", new NVec2(70, 0)))
                LoadFromDisk();

            if (_statusTimer > 0f && !string.IsNullOrEmpty(_statusMessage))
            {
                ImGui.SameLine();
                ImGui.TextColored(new NVec4(0.4f, 1f, 0.4f, 1f), _statusMessage);
            }
        }

        // ── Jerarquía ─────────────────────────────────────────────────────

        private void DrawHierarchy()
        {
            ImGui.Text("Elements");
            ImGui.Separator();

            if (ImGui.Button("+ Add"))
                ImGui.OpenPopup("addElemPopup");

            if (ImGui.BeginPopup("addElemPopup"))
            {
                foreach (var t in new[] { "Label", "Image", "Button", "Slider", "InputText" })
                    if (ImGui.MenuItem(t)) { AddElement(t); ImGui.CloseCurrentPopup(); }
                ImGui.EndPopup();
            }

            ImGui.SameLine();
            bool canOp = _selectedIndex >= 0 && _selectedIndex < _elements.Count;

            if (!canOp) ImGui.BeginDisabled();
            if (ImGui.Button("Delete") && canOp)
            {
                _elements.RemoveAt(_selectedIndex);
                _selectedIndex = Math.Clamp(_selectedIndex, -1, _elements.Count - 1);
                _isDirty = true;
            }
            if (!canOp) ImGui.EndDisabled();

            ImGui.SameLine();
            if (!canOp || _selectedIndex == 0) ImGui.BeginDisabled();
            if (ImGui.ArrowButton("up", ImGuiDir.Up) && canOp && _selectedIndex > 0)
            {
                (_elements[_selectedIndex], _elements[_selectedIndex - 1]) =
                    (_elements[_selectedIndex - 1], _elements[_selectedIndex]);
                _selectedIndex--;
                _isDirty = true;
            }
            if (!canOp || _selectedIndex == 0) ImGui.EndDisabled();

            ImGui.SameLine();
            if (!canOp || _selectedIndex == _elements.Count - 1) ImGui.BeginDisabled();
            if (ImGui.ArrowButton("dn", ImGuiDir.Down) && canOp && _selectedIndex < _elements.Count - 1)
            {
                (_elements[_selectedIndex], _elements[_selectedIndex + 1]) =
                    (_elements[_selectedIndex + 1], _elements[_selectedIndex]);
                _selectedIndex++;
                _isDirty = true;
            }
            if (!canOp || _selectedIndex == _elements.Count - 1) ImGui.EndDisabled();

            ImGui.BeginChild("elemList", new NVec2(0, 0));
            for (int i = 0; i < _elements.Count; i++)
            {
                var e = _elements[i];
                bool sel = i == _selectedIndex;
                if (ImGui.Selectable($"[{e.Type}] {e.Name}##{i}", sel))
                    _selectedIndex = i;
            }
            ImGui.EndChild();
        }

        // ── Inspector ─────────────────────────────────────────────────────

        private void DrawInspector()
        {
            if (ImGui.CollapsingHeader("Canvas Settings", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (InputText("Canvas Name", ref _canvasName)) _isDirty = true;
                if (ImGui.InputInt("Sort Order", ref _sortOrder)) _isDirty = true;
                if (ImGui.Checkbox("Visible##cvs", ref _canvasVisible)) _isDirty = true;
                if (ImGui.Combo("Scale Mode", ref _scaleModeIdx, ScaleModes, ScaleModes.Length)) _isDirty = true;
                if (ImGui.InputFloat("Ref Width", ref _refWidth, 0, 0, "%.0f")) _isDirty = true;
                if (ImGui.InputFloat("Ref Height", ref _refHeight, 0, 0, "%.0f")) _isDirty = true;
            }

            if (_selectedIndex < 0 || _selectedIndex >= _elements.Count) return;

            ImGui.Separator();
            var elem = _elements[_selectedIndex];

            if (ImGui.CollapsingHeader("Element##base", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (InputText("Name##el", ref elem.Name)) _isDirty = true;
                if (ImGui.InputInt("ZOrder", ref elem.ZOrder)) _isDirty = true;
                if (ImGui.Checkbox("Visible##el", ref elem.Visible)) _isDirty = true;
                if (ImGui.Checkbox("Enabled##el", ref elem.Enabled)) _isDirty = true;
                if (ImGui.InputFloat2("Position", ref elem.Position)) _isDirty = true;
                if (ImGui.InputFloat2("Size", ref elem.Size)) _isDirty = true;
                if (ImGui.ColorEdit4("Color##el", ref elem.Color)) _isDirty = true;
            }

            switch (elem.Type.ToLowerInvariant())
            {
                case "label": DrawLabel(elem); break;
                case "image": DrawImage(elem); break;
                case "button": DrawButton(elem); break;
                case "slider": DrawSlider(elem); break;
                case "inputtext": DrawInputText(elem); break;
            }
        }

        private void DrawLabel(ElemData e)
        {
            if (!ImGui.CollapsingHeader("Label##lbl", ImGuiTreeNodeFlags.DefaultOpen)) return;
            if (InputText("Text##lbl", ref e.Text)) _isDirty = true;
            if (InputText("FontName##lbl", ref e.FontName)) _isDirty = true;
            if (ImGui.InputFloat("FontSize##lbl", ref e.FontSize)) _isDirty = true;
            if (ImGui.Checkbox("AutoSize", ref e.AutoSize)) _isDirty = true;
            if (ImGui.InputFloat("Rotation##lbl", ref e.Rotation)) _isDirty = true;
            if (ImGui.ColorEdit4("TextColor##lbl", ref e.TextColor)) _isDirty = true;
        }

        private void DrawImage(ElemData e)
        {
            if (!ImGui.CollapsingHeader("Image##img", ImGuiTreeNodeFlags.DefaultOpen)) return;
            if (InputText("TexturePath", ref e.TexturePath)) _isDirty = true;
            if (ImGui.InputFloat("Rotation##img", ref e.Rotation)) _isDirty = true;
        }

        private void DrawButton(ElemData e)
        {
            if (!ImGui.CollapsingHeader("Button##btn", ImGuiTreeNodeFlags.DefaultOpen)) return;
            if (InputText("Text##btn", ref e.Text)) _isDirty = true;
            if (InputText("FontName##btn", ref e.FontName)) _isDirty = true;
            if (ImGui.InputFloat("FontSize##btn", ref e.FontSize)) _isDirty = true;
            if (ImGui.InputFloat("CornerRadius##btn", ref e.CornerRadius)) _isDirty = true;
            if (ImGui.InputFloat("BorderWidth##btn", ref e.BorderWidth)) _isDirty = true;
            if (ImGui.InputFloat("TransitionSpeed", ref e.TransitionSpeed)) _isDirty = true;
            ImGui.Separator();
            if (ImGui.ColorEdit4("NormalTop##btn", ref e.NormalTop)) _isDirty = true;
            if (ImGui.ColorEdit4("NormalBottom##btn", ref e.NormalBottom)) _isDirty = true;
            if (ImGui.ColorEdit4("HoverTop##btn", ref e.HoverTop)) _isDirty = true;
            if (ImGui.ColorEdit4("HoverBottom##btn", ref e.HoverBottom)) _isDirty = true;
            if (ImGui.ColorEdit4("PressedTop##btn", ref e.PressedTop)) _isDirty = true;
            if (ImGui.ColorEdit4("PressedBottom##btn", ref e.PressedBottom)) _isDirty = true;
            if (ImGui.ColorEdit4("BorderColor##btn", ref e.BorderColor)) _isDirty = true;
            if (ImGui.ColorEdit4("TextColor##btn", ref e.TextColor)) _isDirty = true;
        }

        private void DrawSlider(ElemData e)
        {
            if (!ImGui.CollapsingHeader("Slider##sld", ImGuiTreeNodeFlags.DefaultOpen)) return;
            if (ImGui.InputFloat("Min", ref e.Min)) _isDirty = true;
            if (ImGui.InputFloat("Max", ref e.Max)) _isDirty = true;
            if (ImGui.InputFloat("Value##sld", ref e.Value)) _isDirty = true;
            if (ImGui.InputFloat("ThumbRadius", ref e.ThumbRadius)) _isDirty = true;
            if (ImGui.Checkbox("ShowValue", ref e.ShowValue)) _isDirty = true;
            if (InputText("ValueFormat", ref e.ValueFormat)) _isDirty = true;
            if (ImGui.ColorEdit4("FillColorA", ref e.FillColorA)) _isDirty = true;
            if (ImGui.ColorEdit4("FillColorB", ref e.FillColorB)) _isDirty = true;
            if (ImGui.ColorEdit4("ThumbColor", ref e.ThumbColor)) _isDirty = true;
        }

        private void DrawInputText(ElemData e)
        {
            if (!ImGui.CollapsingHeader("InputText##it", ImGuiTreeNodeFlags.DefaultOpen)) return;
            if (InputText("Placeholder", ref e.Placeholder)) _isDirty = true;
            if (ImGui.InputInt("MaxLength", ref e.MaxLength)) _isDirty = true;
            if (ImGui.Checkbox("IsPassword", ref e.IsPassword)) _isDirty = true;
            if (ImGui.Checkbox("IsNumericOnly", ref e.IsNumericOnly)) _isDirty = true;
            if (InputText("FontName##it", ref e.FontName)) _isDirty = true;
            if (ImGui.InputFloat("FontSize##it", ref e.FontSize)) _isDirty = true;
            if (ImGui.InputFloat("CornerRadius##it", ref e.CornerRadius)) _isDirty = true;
            if (ImGui.InputFloat("BorderWidth##it", ref e.BorderWidth)) _isDirty = true;
            if (ImGui.ColorEdit4("TextColor##it", ref e.TextColor)) _isDirty = true;
            if (ImGui.ColorEdit4("NormalColor##it", ref e.NormalColor)) _isDirty = true;
            if (ImGui.ColorEdit4("HoverColor##it", ref e.HoverColor)) _isDirty = true;
            if (ImGui.ColorEdit4("FocusedColor##it", ref e.FocusedColor)) _isDirty = true;
            if (ImGui.ColorEdit4("BorderNormal##it", ref e.BorderColor)) _isDirty = true;
            if (ImGui.ColorEdit4("BorderFocused##it", ref e.BorderFocused)) _isDirty = true;
        }

        // ── Disk I/O ──────────────────────────────────────────────────────

        private void LoadFromDisk()
        {
            if (!System.IO.File.Exists(_assetPath)) { SetStatus("Archivo no encontrado"); return; }
            try
            {
                var root = JsonNode.Parse(System.IO.File.ReadAllText(_assetPath))!.AsObject();

                _canvasName = root["name"]?.GetValue<string>() ?? "canvas";
                _sortOrder = root["sortOrder"]?.GetValue<int>() ?? 0;
                _canvasVisible = root["visible"]?.GetValue<bool>() ?? true;
                string sm = root["scaleMode"]?.GetValue<string>() ?? "Fit";
                _scaleModeIdx = Math.Max(0, Array.IndexOf(ScaleModes, sm));
                _refWidth = root["referenceWidth"]?.GetValue<float>() ?? 1920f;
                _refHeight = root["referenceHeight"]?.GetValue<float>() ?? 1080f;

                _elements.Clear();
                var arr = root["elements"]?.AsArray();
                if (arr is not null)
                    foreach (var node in arr)
                        if (node is not null) _elements.Add(ParseElem(node.AsObject()));

                _selectedIndex = -1;
                _isDirty = false;
                SetStatus("Cargado OK");
            }
            catch (Exception ex) { SetStatus($"Error: {ex.Message}"); }
        }

        private void SaveToDisk()
        {
            if (string.IsNullOrEmpty(_assetPath)) { SetStatus("Sin ruta"); return; }
            try
            {
                var root = new JsonObject
                {
                    ["name"] = _canvasName,
                    ["sortOrder"] = _sortOrder,
                    ["visible"] = _canvasVisible,
                    ["scaleMode"] = ScaleModes[_scaleModeIdx],
                    ["referenceWidth"] = _refWidth,
                    ["referenceHeight"] = _refHeight,
                    ["elements"] = new JsonArray(_elements
                        .Select(e => (JsonNode?)SerializeElem(e)).ToArray())
                };

                string json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(_assetPath, json, new System.Text.UTF8Encoding(false));
                _isDirty = false;
                SetStatus("Guardado OK");
            }
            catch (Exception ex) { SetStatus($"Error: {ex.Message}"); }
        }

        // ── Element helpers ───────────────────────────────────────────────

        private void AddElement(string type)
        {
            var e = new ElemData { Type = type, Name = $"New{type}{_elements.Count}" };
            switch (type.ToLowerInvariant())
            {
                case "label": e.Text = "Label"; e.FontSize = 24f; e.Size = new NVec2(200, 40); e.AutoSize = true; break;
                case "button": e.Text = "Button"; e.Size = new NVec2(160, 50); break;
                case "slider": e.Size = new NVec2(300, 6); e.Max = 1f; e.Value = 0.5f; break;
                case "inputtext": e.Size = new NVec2(300, 40); e.Placeholder = "Type here..."; break;
                case "image": e.Size = new NVec2(100, 100); e.Color = NVec4.One; break;
            }
            _elements.Add(e);
            _selectedIndex = _elements.Count - 1;
            _isDirty = true;
        }

        private void SetStatus(string msg, float duration = 3f)
        {
            _statusMessage = msg;
            _statusTimer = duration;
        }

        // InputText helper — gestiona buffer UTF8 internamente
        private static bool InputText(string label, ref string value)
        {
            var buf = new byte[512];
            var src = Encoding.UTF8.GetBytes(value);
            Array.Copy(src, buf, Math.Min(src.Length, buf.Length - 1));
            bool changed = ImGui.InputText(label, buf, (uint)buf.Length);
            if (changed)
            {
                int nullPos = Array.IndexOf(buf, (byte)0);
                value = Encoding.UTF8.GetString(buf, 0, nullPos < 0 ? buf.Length : nullPos);
            }
            return changed;
        }

        // ── JSON parse / serialize ────────────────────────────────────────

        private static ElemData ParseElem(JsonObject o)
        {
            var e = new ElemData
            {
                Type = S(o, "type", "Label"),
                Name = S(o, "name", "element"),
                ZOrder = I(o, "zOrder", 0),
                Visible = B(o, "visible", true),
                Enabled = B(o, "enabled", true),
                Position = V2(o["position"]),
                Size = V2(o["size"]),
                Color = V4(o["color"], NVec4.One),
                // common text
                Text = S(o, "text", ""),
                FontName = S(o, "fontName", "Segoe UI"),
                FontSize = F(o, "fontSize", 18f),
                AutoSize = B(o, "autoSize", true),
                Rotation = F(o, "rotation", 0f),
                TextColor = V4(o["textColor"], NVec4.One),
                // image
                TexturePath = S(o, "texturePath", ""),
                // button
                CornerRadius = F(o, "cornerRadius", 4f),
                BorderWidth = F(o, "borderWidth", 1f),
                TransitionSpeed = F(o, "transitionSpeed", 10f),
                NormalTop = V4(o["normalTop"], new NVec4(0.22f, 0.22f, 0.22f, 1f)),
                NormalBottom = V4(o["normalBottom"], new NVec4(0.155f, 0.155f, 0.155f, 1f)),
                HoverTop = V4(o["hoverTop"], new NVec4(0.31f, 0.31f, 0.31f, 1f)),
                HoverBottom = V4(o["hoverBottom"], new NVec4(0.23f, 0.23f, 0.23f, 1f)),
                PressedTop = V4(o["pressedTop"], new NVec4(0.11f, 0.11f, 0.11f, 1f)),
                PressedBottom = V4(o["pressedBottom"], new NVec4(0.09f, 0.09f, 0.09f, 1f)),
                BorderColor = V4(o["borderColor"], new NVec4(0.52f, 0.52f, 0.52f, 1f)),
                // slider
                Min = F(o, "min", 0f),
                Max = F(o, "max", 1f),
                Value = F(o, "value", 0.5f),
                ThumbRadius = F(o, "thumbRadius", 9f),
                ShowValue = B(o, "showValue", true),
                ValueFormat = S(o, "valueFormat", "F0"),
                FillColorA = V4(o["fillColorA"], new NVec4(0.18f, 0.52f, 0.90f, 1f)),
                FillColorB = V4(o["fillColorB"], new NVec4(0.12f, 0.40f, 0.75f, 1f)),
                ThumbColor = V4(o["thumbColor"], new NVec4(0.92f, 0.92f, 0.92f, 1f)),
                // inputtext
                Placeholder = S(o, "placeholder", ""),
                MaxLength = I(o, "maxLength", 0),
                IsPassword = B(o, "isPassword", false),
                IsNumericOnly = B(o, "isNumericOnly", false),
                NormalColor = V4(o["normalColor"], new NVec4(0.14f, 0.14f, 0.14f, 1f)),
                HoverColor = V4(o["hoverColor"], new NVec4(0.20f, 0.20f, 0.20f, 1f)),
                FocusedColor = V4(o["focusedColor"], new NVec4(0.12f, 0.12f, 0.12f, 1f)),
                BorderFocused = V4(o["borderFocusedColor"], new NVec4(0.18f, 0.52f, 0.90f, 1f)),
            };
            return e;
        }

        private static JsonObject SerializeElem(ElemData e)
        {
            var o = new JsonObject
            {
                ["type"] = e.Type,
                ["name"] = e.Name,
                ["zOrder"] = e.ZOrder,
                ["visible"] = e.Visible,
                ["enabled"] = e.Enabled,
                ["position"] = JV2(e.Position),
                ["size"] = JV2(e.Size),
                ["color"] = JV4(e.Color),
            };

            switch (e.Type.ToLowerInvariant())
            {
                case "label":
                    o["text"] = e.Text; o["fontName"] = e.FontName;
                    o["fontSize"] = e.FontSize; o["autoSize"] = e.AutoSize;
                    o["rotation"] = e.Rotation; o["textColor"] = JV4(e.TextColor);
                    break;
                case "image":
                    o["texturePath"] = e.TexturePath; o["rotation"] = e.Rotation;
                    break;
                case "button":
                    o["text"] = e.Text; o["fontName"] = e.FontName; o["fontSize"] = e.FontSize;
                    o["cornerRadius"] = e.CornerRadius; o["borderWidth"] = e.BorderWidth;
                    o["transitionSpeed"] = e.TransitionSpeed;
                    o["normalTop"] = JV4(e.NormalTop); o["normalBottom"] = JV4(e.NormalBottom);
                    o["hoverTop"] = JV4(e.HoverTop); o["hoverBottom"] = JV4(e.HoverBottom);
                    o["pressedTop"] = JV4(e.PressedTop); o["pressedBottom"] = JV4(e.PressedBottom);
                    o["borderColor"] = JV4(e.BorderColor); o["textColor"] = JV4(e.TextColor);
                    break;
                case "slider":
                    o["min"] = e.Min; o["max"] = e.Max; o["value"] = e.Value;
                    o["thumbRadius"] = e.ThumbRadius; o["showValue"] = e.ShowValue;
                    o["valueFormat"] = e.ValueFormat;
                    o["fillColorA"] = JV4(e.FillColorA); o["fillColorB"] = JV4(e.FillColorB);
                    o["thumbColor"] = JV4(e.ThumbColor);
                    break;
                case "inputtext":
                    o["placeholder"] = e.Placeholder; o["maxLength"] = e.MaxLength;
                    o["isPassword"] = e.IsPassword; o["isNumericOnly"] = e.IsNumericOnly;
                    o["fontName"] = e.FontName; o["fontSize"] = e.FontSize;
                    o["cornerRadius"] = e.CornerRadius; o["borderWidth"] = e.BorderWidth;
                    o["textColor"] = JV4(e.TextColor);
                    o["normalColor"] = JV4(e.NormalColor);
                    o["hoverColor"] = JV4(e.HoverColor);
                    o["focusedColor"] = JV4(e.FocusedColor);
                    o["borderColor"] = JV4(e.BorderColor);
                    o["borderFocusedColor"] = JV4(e.BorderFocused);
                    break;
            }
            return o;
        }

        // ── JSON micro-helpers ────────────────────────────────────────────

        private static string S(JsonObject o, string k, string d) => o[k]?.GetValue<string>() ?? d;
        private static float F(JsonObject o, string k, float d) => o[k]?.GetValue<float>() ?? d;
        private static int I(JsonObject o, string k, int d) => o[k]?.GetValue<int>() ?? d;
        private static bool B(JsonObject o, string k, bool d) => o[k]?.GetValue<bool>() ?? d;

        private static NVec2 V2(JsonNode? n)
        {
            if (n is null) return NVec2.Zero;
            var a = n.AsArray();
            return new NVec2(a[0]?.GetValue<float>() ?? 0f, a[1]?.GetValue<float>() ?? 0f);
        }

        private static NVec4 V4(JsonNode? n, NVec4 def)
        {
            if (n is null) return def;
            var a = n.AsArray();
            float r = a[0]?.GetValue<float>() ?? 0f;
            float g = a[1]?.GetValue<float>() ?? 0f;
            float b = a[2]?.GetValue<float>() ?? 0f;
            float w = a.Count > 3 ? (a[3]?.GetValue<float>() ?? 1f) : 1f;
            if (r > 1f || g > 1f || b > 1f) { r /= 255f; g /= 255f; b /= 255f; w /= 255f; }
            return new NVec4(r, g, b, w);
        }

        private static JsonArray JV2(NVec2 v) => new(v.X, v.Y);
        private static JsonArray JV4(NVec4 v) => new(v.X, v.Y, v.Z, v.W);

        // ── ElemData ──────────────────────────────────────────────────────

        private sealed class ElemData
        {
            public string Type = "Label";
            public string Name = "element";
            public int ZOrder = 0;
            public bool Visible = true;
            public bool Enabled = true;
            public NVec2 Position;
            public NVec2 Size = new(100, 30);
            public NVec4 Color = NVec4.One;
            // Text / font
            public string Text = "";
            public string FontName = "Segoe UI";
            public float FontSize = 18f;
            public bool AutoSize = true;
            public float Rotation = 0f;
            public NVec4 TextColor = NVec4.One;
            // Image
            public string TexturePath = "";
            // Button
            public float CornerRadius = 4f;
            public float BorderWidth = 1f;
            public float TransitionSpeed = 10f;
            public NVec4 NormalTop = new(0.22f, 0.22f, 0.22f, 1f);
            public NVec4 NormalBottom = new(0.155f, 0.155f, 0.155f, 1f);
            public NVec4 HoverTop = new(0.31f, 0.31f, 0.31f, 1f);
            public NVec4 HoverBottom = new(0.23f, 0.23f, 0.23f, 1f);
            public NVec4 PressedTop = new(0.11f, 0.11f, 0.11f, 1f);
            public NVec4 PressedBottom = new(0.09f, 0.09f, 0.09f, 1f);
            public NVec4 BorderColor = new(0.52f, 0.52f, 0.52f, 1f);
            // Slider
            public float Min = 0f;
            public float Max = 1f;
            public float Value = 0.5f;
            public float ThumbRadius = 9f;
            public bool ShowValue = true;
            public string ValueFormat = "F0";
            public NVec4 FillColorA = new(0.18f, 0.52f, 0.90f, 1f);
            public NVec4 FillColorB = new(0.12f, 0.40f, 0.75f, 1f);
            public NVec4 ThumbColor = new(0.92f, 0.92f, 0.92f, 1f);
            // InputText
            public string Placeholder = "";
            public int MaxLength = 0;
            public bool IsPassword = false;
            public bool IsNumericOnly = false;
            public NVec4 NormalColor = new(0.14f, 0.14f, 0.14f, 1f);
            public NVec4 HoverColor = new(0.20f, 0.20f, 0.20f, 1f);
            public NVec4 FocusedColor = new(0.12f, 0.12f, 0.12f, 1f);
            public NVec4 BorderFocused = new(0.18f, 0.52f, 0.90f, 1f);
        }
    }
}