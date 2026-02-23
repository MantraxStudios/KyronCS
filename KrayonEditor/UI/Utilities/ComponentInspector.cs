using ImGuiNET;
using KrayonCore;
using KrayonCore.Physics;
using KrayonCore.GraphicsData;
using KrayonCore.Core.Attributes;
using KrayonCore.Core.Components;
using KrayonEditor.Main;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace KrayonEditor.UI
{
    public static class ComponentInspector
    {
        private const float LabelColumnWidth = 148f;

        private static string _pickerSearch = "";
        private static Action<string>? _pickerCallback = null;
        private static string? _pickerFilterType = null;
        private static string _pickerPopupId = "##AssetPickerModal";
        private static string? _pickerPendingLabel = null;
        private static string? _pickerPendingResult = null;
        private static bool _pickerWantsOpen = false;

        private static string _matPickerSearch = "";
        private static Action<string>? _matPickerCallback = null;
        private static string _matPickerPopupId = "##MatPickerModal";
        private static string? _matPickerPendingLabel = null;
        private static string? _matPickerPendingResult = null;
        private static bool _matPickerWantsOpen = false;

        public static void BeginFieldRow(string label)
        {
            ImGui.Text(label);
            ImGui.SameLine(LabelColumnWidth);
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        }

        public static void BeginFieldRowFixed(string label, float fieldWidth)
        {
            ImGui.Text(label);
            ImGui.SameLine(LabelColumnWidth);
            ImGui.SetNextItemWidth(fieldWidth);
        }

        public static void DrawLabelValue(string label, string value)
        {
            ImGui.Text(label);
            ImGui.SameLine(LabelColumnWidth);
            ImGui.TextUnformatted(value);
        }

        public static bool DrawComponentHeader(string name)
        {
            return ImGui.CollapsingHeader(name, ImGuiTreeNodeFlags.DefaultOpen);
        }

        private static string ResolveGuidLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (Guid.TryParse(value, out var guid))
            {
                var record = AssetManager.Get(guid);
                if (record != null) return Path.GetFileName(record.Path);
            }
            return value;
        }

        private static void OpenAssetPicker(Action<string> onSelected, string? filterType = null)
        {
            _pickerSearch = "";
            _pickerCallback = onSelected;
            _pickerFilterType = filterType;
            _pickerWantsOpen = true;
        }

        private static void OpenMaterialPicker(string label, Action<string> onSelected)
        {
            _matPickerSearch = "";
            _matPickerCallback = onSelected;
            _matPickerWantsOpen = true;
        }

        public static string DrawMaterialStringField(string label, string currentName)
        {
            if (_matPickerPendingLabel == label && _matPickerPendingResult != null)
            {
                currentName = _matPickerPendingResult;
                _matPickerPendingLabel = null;
                _matPickerPendingResult = null;
            }

            bool hasMat = !string.IsNullOrEmpty(currentName);

            float clearW = 20f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float fieldW = ImGui.GetContentRegionAvail().X - LabelColumnWidth - clearW - spacing;
            if (fieldW < 40f) fieldW = 40f;

            ImGui.Text(label);
            ImGui.SameLine(LabelColumnWidth);

            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            float h = ImGui.GetFrameHeight();

            uint bgCol = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg]);
            uint bgHov = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBgHovered]);
            uint brCol = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Border]);
            uint dotCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.8f, 0.5f, 0.1f, 1f));
            uint txtCol = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
            uint dimCol = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);

            bool hovered = ImGui.IsMouseHoveringRect(pos, new Vector2(pos.X + fieldW, pos.Y + h));

            dl.AddRectFilled(pos, new Vector2(pos.X + fieldW, pos.Y + h), hovered ? bgHov : bgCol, 3f);
            dl.AddRect(pos, new Vector2(pos.X + fieldW, pos.Y + h), brCol, 3f);

            if (hasMat)
            {
                dl.AddCircleFilled(new Vector2(pos.X + h * 0.5f, pos.Y + h * 0.5f), h * 0.20f, dotCol);
                string txt = currentName;
                float maxTW = fieldW - h * 0.9f - 4f;
                while (txt.Length > 4 && ImGui.CalcTextSize(txt).X > maxTW)
                    txt = txt[..^4] + "...";
                dl.AddText(new Vector2(pos.X + h * 0.9f, pos.Y + (h - ImGui.GetTextLineHeight()) * 0.5f), txtCol, txt);
            }
            else
            {
                string ptxt = "None (click to select)";
                float maxTW = fieldW - 8f;
                while (ptxt.Length > 4 && ImGui.CalcTextSize(ptxt).X > maxTW)
                    ptxt = ptxt[..^4] + "...";
                dl.AddText(new Vector2(pos.X + 5f, pos.Y + (h - ImGui.GetTextLineHeight()) * 0.5f), dimCol, ptxt);
            }

            ImGui.InvisibleButton($"##matfield_{label}", new Vector2(fieldW, h));

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip(hasMat ? currentName : "Click to select material");
            }

            if (ImGui.IsItemClicked())
            {
                string capturedLabel = label;
                OpenMaterialPicker(label, name =>
                {
                    _matPickerPendingLabel = capturedLabel;
                    _matPickerPendingResult = name;
                });
            }

            ImGui.SameLine();
            if (ImGui.Button($"x##{label}_matclr", new Vector2(clearW, h)))
                currentName = "";

            return currentName;
        }

        public static void DrawAssetPickerModal()
        {
            if (_pickerWantsOpen)
            {
                ImGui.OpenPopup(_pickerPopupId);
                _pickerWantsOpen = false;
            }

            if (_matPickerWantsOpen)
            {
                ImGui.OpenPopup(_matPickerPopupId);
                _matPickerWantsOpen = false;
            }

            ImGui.SetNextWindowSize(new Vector2(440, 520), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(
                new Vector2(ImGui.GetIO().DisplaySize.X * 0.5f, ImGui.GetIO().DisplaySize.Y * 0.5f),
                ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (ImGui.BeginPopupModal(_pickerPopupId, ref open,
                ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.TextUnformatted("Select Asset");
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 22f);
                if (ImGui.Button("x##closepicker", new Vector2(22f, 0f)))
                    ImGui.CloseCurrentPopup();

                ImGui.Separator();
                ImGui.Spacing();

                ImGui.SetNextItemWidth(-1f);
                if (ImGui.IsWindowAppearing())
                    ImGui.SetKeyboardFocusHere();
                ImGui.InputTextWithHint("##pickersearch", "Search by name...", ref _pickerSearch, 256);
                ImGui.Spacing();

                var assets = AssetManager.All()
                    .Where(a =>
                        (_pickerFilterType == null || a.Type == _pickerFilterType) &&
                        (string.IsNullOrEmpty(_pickerSearch) ||
                         Path.GetFileName(a.Path).Contains(_pickerSearch, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(a => a.Type)
                    .ThenBy(a => Path.GetFileName(a.Path))
                    .ToList();

                if (ImGui.BeginChild("##pickerlist", new Vector2(-1f, -36f)))
                {
                    string? lastType = null;
                    foreach (var asset in assets)
                    {
                        if (asset.Type != lastType)
                        {
                            if (lastType != null) ImGui.Spacing();
                            ImGui.TextDisabled(asset.Type);
                            ImGui.Separator();
                            lastType = asset.Type;
                        }

                        string fileName = Path.GetFileName(asset.Path);
                        string dir = Path.GetDirectoryName(asset.Path)?.Replace("\\", "/") ?? "";

                        bool sel = false;
                        if (ImGui.Selectable($"{fileName}##{asset.Guid}", ref sel))
                        {
                            _pickerCallback?.Invoke(asset.Guid.ToString());
                            ImGui.CloseCurrentPopup();
                        }

                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip(string.IsNullOrEmpty(dir) ? fileName : $"{dir}/{fileName}");
                    }

                    if (assets.Count == 0)
                    {
                        ImGui.Spacing();
                        ImGui.TextDisabled(string.IsNullOrEmpty(_pickerSearch)
                            ? "No assets found."
                            : $"No results for \"{_pickerSearch}\"");
                    }

                    ImGui.EndChild();
                }

                ImGui.Spacing();
                if (ImGui.Button("Cancel", new Vector2(-1f, 0f)))
                    ImGui.CloseCurrentPopup();

                ImGui.EndPopup();
            }

            ImGui.SetNextWindowSize(new Vector2(440, 520), ImGuiCond.Appearing);
            ImGui.SetNextWindowPos(
                new Vector2(ImGui.GetIO().DisplaySize.X * 0.5f, ImGui.GetIO().DisplaySize.Y * 0.5f),
                ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool matOpen = true;
            if (ImGui.BeginPopupModal(_matPickerPopupId, ref matOpen,
                ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.TextUnformatted("Select Material");
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 22f);
                if (ImGui.Button("x##closematpicker", new Vector2(22f, 0f)))
                    ImGui.CloseCurrentPopup();

                ImGui.Separator();
                ImGui.Spacing();

                ImGui.SetNextItemWidth(-1f);
                if (ImGui.IsWindowAppearing())
                    ImGui.SetKeyboardFocusHere();
                ImGui.InputTextWithHint("##matpickersearch", "Search by name...", ref _matPickerSearch, 256);
                ImGui.Spacing();

                var materials = GraphicsEngine.Instance.Materials.GetAll()
                    .Where(m => string.IsNullOrEmpty(_matPickerSearch) ||
                                m.Name.Contains(_matPickerSearch, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(m => m.Name)
                    .ToList();

                if (ImGui.BeginChild("##matpickerlist", new Vector2(-1f, -36f)))
                {
                    ImGui.TextDisabled("Materials");
                    ImGui.Separator();
                    ImGui.Spacing();

                    bool noneSel = false;
                    if (ImGui.Selectable("None##matnone", ref noneSel))
                    {
                        _matPickerCallback?.Invoke("");
                        ImGui.CloseCurrentPopup();
                    }

                    foreach (var mat in materials)
                    {
                        bool sel = false;
                        if (ImGui.Selectable($"{mat.Name}##mat_{mat.Name}", ref sel))
                        {
                            _matPickerCallback?.Invoke(mat.Name);
                            ImGui.CloseCurrentPopup();
                        }
                    }

                    if (materials.Count == 0)
                    {
                        ImGui.Spacing();
                        ImGui.TextDisabled(string.IsNullOrEmpty(_matPickerSearch)
                            ? "No materials found."
                            : $"No results for \"{_matPickerSearch}\"");
                    }

                    ImGui.EndChild();
                }

                ImGui.Spacing();
                if (ImGui.Button("Cancel", new Vector2(-1f, 0f)))
                    ImGui.CloseCurrentPopup();

                ImGui.EndPopup();
            }
        }

        public static string DrawAssetStringField(string label, string rawValue, string? filterType = null)
        {
            if (_pickerPendingLabel == label && _pickerPendingResult != null)
            {
                rawValue = _pickerPendingResult;
                _pickerPendingLabel = null;
                _pickerPendingResult = null;
            }

            bool isGuid = Guid.TryParse(rawValue, out _);
            string resolved = ResolveGuidLabel(rawValue);
            bool hasAsset = isGuid && resolved != rawValue;

            float clearW = 20f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float fieldW = ImGui.GetContentRegionAvail().X - LabelColumnWidth - clearW - spacing;
            if (fieldW < 40f) fieldW = 40f;

            ImGui.Text(label);
            ImGui.SameLine(LabelColumnWidth);

            var dl = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            float h = ImGui.GetFrameHeight();

            uint bgCol = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg]);
            uint bgHov = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBgHovered]);
            uint brCol = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Border]);
            uint dotCol = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.CheckMark]);
            uint txtCol = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
            uint dimCol = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);

            bool hovered = ImGui.IsMouseHoveringRect(pos, new Vector2(pos.X + fieldW, pos.Y + h));

            dl.AddRectFilled(pos, new Vector2(pos.X + fieldW, pos.Y + h), hovered ? bgHov : bgCol, 3f);
            dl.AddRect(pos, new Vector2(pos.X + fieldW, pos.Y + h), brCol, 3f);

            if (hasAsset)
            {
                dl.AddCircleFilled(new Vector2(pos.X + h * 0.5f, pos.Y + h * 0.5f), h * 0.20f, dotCol);
                string txt = resolved;
                float maxTW = fieldW - h * 0.9f - 4f;
                while (txt.Length > 4 && ImGui.CalcTextSize(txt).X > maxTW)
                    txt = txt[..^4] + "...";
                dl.AddText(new Vector2(pos.X + h * 0.9f, pos.Y + (h - ImGui.GetTextLineHeight()) * 0.5f), txtCol, txt);
            }
            else
            {
                string placeholder = string.IsNullOrEmpty(rawValue) ? "None (click to select)" : rawValue;
                uint col = string.IsNullOrEmpty(rawValue) ? dimCol : txtCol;
                string ptxt = placeholder;
                float maxTW = fieldW - 8f;
                while (ptxt.Length > 4 && ImGui.CalcTextSize(ptxt).X > maxTW)
                    ptxt = ptxt[..^4] + "...";
                dl.AddText(new Vector2(pos.X + 5f, pos.Y + (h - ImGui.GetTextLineHeight()) * 0.5f), col, ptxt);
            }

            ImGui.InvisibleButton($"##assetfield_{label}", new Vector2(fieldW, h));

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (hasAsset)
                    ImGui.SetTooltip($"{resolved}\n{rawValue}");
                else
                    ImGui.SetTooltip("Click to select asset");
            }

            if (ImGui.IsItemClicked())
            {
                string capturedLabel = label;
                OpenAssetPicker(guid =>
                {
                    _pickerPendingLabel = capturedLabel;
                    _pickerPendingResult = guid;
                }, filterType);
            }

            if (ImGui.BeginDragDropTarget())
            {
                unsafe
                {
                    var payload = ImGui.AcceptDragDropPayload("ASSET_PATH");
                    if (payload.NativePtr != null)
                    {
                        byte[] d = new byte[payload.DataSize];
                        System.Runtime.InteropServices.Marshal.Copy(payload.Data, d, 0, payload.DataSize);
                        rawValue = System.Text.Encoding.UTF8.GetString(d);
                    }
                }
                ImGui.EndDragDropTarget();
            }

            ImGui.SameLine();
            if (ImGui.Button($"x##{label}_clr", new Vector2(clearW, h)))
                rawValue = "";

            return rawValue;
        }

        public static void DrawTransform(KrayonCore.Components.Transform t)
        {
            ImGui.PushID("Transform");
            if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                Vector3 pos = new(t.X, t.Y, t.Z);
                BeginFieldRow("Position");
                if (ImGui.DragFloat3("##pos", ref pos, 0.1f)) t.SetPosition(pos.X, pos.Y, pos.Z);

                Vector3 rot = new(t.RotationX, t.RotationY, t.RotationZ);
                BeginFieldRow("Rotation");
                if (ImGui.DragFloat3("##rot", ref rot, 0.5f)) t.SetRotation(rot.X, rot.Y, rot.Z);

                Vector3 scl = new(t.ScaleX, t.ScaleY, t.ScaleZ);
                BeginFieldRow("Scale");
                if (ImGui.DragFloat3("##scl", ref scl, 0.01f)) t.SetScale(scl.X, scl.Y, scl.Z);

                ImGui.Spacing();
            }
            ImGui.PopID();
        }

        public static void DrawComponentWithReflection(Component component, GameObject owner)
        {
            Type ct = component.GetType();
            string name = ct.Name;
            var enabledProp = ct.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
            bool hasEnabled = enabledProp != null && enabledProp.PropertyType == typeof(bool);
            bool isEnabled = hasEnabled ? (bool)enabledProp!.GetValue(component)! : true;

            bool open = DrawComponentHeader(name);

            if (ImGui.BeginPopupContextItem($"ctx_{name}_{component.GetHashCode()}"))
            {
                if (hasEnabled)
                {
                    if (ImGui.MenuItem(isEnabled ? "Disable" : "Enable"))
                        enabledProp!.SetValue(component, !isEnabled);
                    ImGui.Separator();
                }
                if (ImGui.MenuItem("Remove Component"))
                {
                    ImGui.EndPopup();
                    owner.RemoveComponent(component);
                    EngineEditor.LogMessage($"Removed {name}");
                    return;
                }
                ImGui.EndPopup();
            }

            if (!open) return;

            ImGui.Indent(4f);

            if (component is KrayonCore.Rigidbody rb)
                DrawRigidbodyInspector(rb);
            else if (component is CSharpLogic csl)
                DrawCSharpLogicInspector(csl);
            else
            {
                foreach (var prop in ct.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanRead || !prop.CanWrite) continue;
                    if (prop.Name == "Enabled" && prop.PropertyType == typeof(bool)) continue;
                    if (prop.GetCustomAttribute<NoSerializeToInspectorAttribute>() != null) continue;
                    DrawProperty(component, prop);
                }
                foreach (var field in ct.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (field.GetCustomAttribute<NoSerializeToInspectorAttribute>() != null) continue;
                    DrawField(component, field);
                }
                DrawCallEventMethods(component, ct);
            }

            ImGui.Unindent(4f);
            ImGui.Spacing();
        }

        public static void DrawAddComponentButton(GameObject target)
        {
            ImGui.PushID("AddComp");
            if (ImGui.Button("Add Component", new Vector2(-1f, 26f)))
                ImGui.OpenPopup("AddCompPopup");

            if (ImGui.BeginPopup("AddCompPopup"))
            {
                int idx = 0;
                foreach (var ct in ComponentRegistry.Components)
                {
                    ImGui.PushID(idx++);
                    if (ImGui.MenuItem(ct.Name))
                    {
                        try
                        {
                            target.AddComponent(ct).Start();
                            EngineEditor.LogMessage($"Added {ct.Name}");
                        }
                        catch (Exception ex)
                        {
                            EngineEditor.LogMessage($"Error adding {ct.Name}: {ex.InnerException?.Message ?? ex.Message}");
                        }
                    }
                    ImGui.PopID();
                }
                ImGui.EndPopup();
            }
            ImGui.PopID();
        }

        private static void DrawCSharpLogicInspector(CSharpLogic csl)
        {
            string scriptVal = DrawAssetStringField("Script", csl.Script, "GameScript");
            if (scriptVal != csl.Script) csl.Script = scriptVal;

            if (csl.GetScriptVariables().Count == 0 && !string.IsNullOrEmpty(csl.Script))
                csl.LoadScript();

            var vars = csl.GetScriptVariables();
            if (vars.Count == 0) return;

            ImGui.Spacing();
            ImGui.TextUnformatted("Script Variables");
            ImGui.Separator();
            ImGui.Spacing();

            int vi = 0;
            foreach (var (varName, varType, varValue) in vars)
            {
                ImGui.PushID($"sv_{vi}");
                DrawScriptVariable(csl, varName, varType, varValue);
                ImGui.PopID();
                vi++;
            }
        }

        private static void DrawScriptVariable(CSharpLogic csl, string varName, Type varType, object varValue)
        {
            if (varType == typeof(bool))
            {
                bool v = varValue != null && (bool)varValue;
                BeginFieldRow(varName);
                if (ImGui.Checkbox($"##{varName}", ref v)) csl.SetScriptVariable(varName, v);
            }
            else if (varType == typeof(float))
            {
                float v = varValue != null ? (float)varValue : 0f;
                BeginFieldRow(varName);
                if (ImGui.DragFloat($"##{varName}", ref v, 0.01f)) csl.SetScriptVariable(varName, v);
            }
            else if (varType == typeof(int))
            {
                int v = varValue != null ? (int)varValue : 0;
                BeginFieldRow(varName);
                if (ImGui.DragInt($"##{varName}", ref v)) csl.SetScriptVariable(varName, v);
            }
            else if (varType == typeof(string))
            {
                string sv = (string)varValue ?? "";
                string nv = DrawAssetStringField(varName, sv);
                if (nv != sv) csl.SetScriptVariable(varName, nv);
            }
            else if (varType == typeof(Vector2))
            {
                Vector2 v = varValue != null ? (Vector2)varValue : Vector2.Zero;
                BeginFieldRow(varName);
                if (ImGui.DragFloat2($"##{varName}", ref v, 0.01f)) csl.SetScriptVariable(varName, v);
            }
            else if (varType == typeof(Vector3))
            {
                Vector3 v = varValue != null ? (Vector3)varValue : Vector3.Zero;
                BeginFieldRow(varName);
                if (ImGui.DragFloat3($"##{varName}", ref v, 0.01f)) csl.SetScriptVariable(varName, v);
            }
            else if (varType == typeof(OpenTK.Mathematics.Vector3))
            {
                var otk = varValue != null ? (OpenTK.Mathematics.Vector3)varValue : OpenTK.Mathematics.Vector3.Zero;
                Vector3 v = new(otk.X, otk.Y, otk.Z);
                BeginFieldRow(varName);
                if (ImGui.DragFloat3($"##{varName}", ref v, 0.01f))
                    csl.SetScriptVariable(varName, new OpenTK.Mathematics.Vector3(v.X, v.Y, v.Z));
            }
            else if (varType == typeof(Vector4))
            {
                Vector4 v = varValue != null ? (Vector4)varValue : Vector4.Zero;
                BeginFieldRow(varName);
                if (ImGui.InputFloat4($"##{varName}", ref v)) csl.SetScriptVariable(varName, v);
            }
            else if (varType.IsEnum)
            {
                string[] names = Enum.GetNames(varType);
                int idx = varValue != null ? Array.IndexOf(names, varValue.ToString()) : 0;
                if (idx < 0) idx = 0;
                BeginFieldRow(varName);
                if (ImGui.Combo($"##{varName}", ref idx, names, names.Length))
                    csl.SetScriptVariable(varName, Enum.Parse(varType, names[idx]));
            }
            else if (varType == typeof(GameObject))
            {
                DrawObjectRefField(varName, varValue as GameObject, go => csl.SetScriptVariable(varName, go));
            }
            else
            {
                DrawLabelValue(varName, varValue?.ToString() ?? "null");
            }
        }

        private static void DrawCallEventMethods(object component, Type ct)
        {
            var methods = ct.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            bool any = false;
            foreach (var m in methods)
                if (m.GetCustomAttribute<CallEventAttribute>() != null) { any = true; break; }
            if (!any) return;

            ImGui.Spacing();
            ImGui.TextUnformatted("Events");
            ImGui.Separator();

            int mi = 0;
            foreach (var m in methods)
            {
                var attr = m.GetCustomAttribute<CallEventAttribute>();
                if (attr == null) continue;
                string label = string.IsNullOrEmpty(attr.DisplayName) ? m.Name : attr.DisplayName;
                ImGui.PushID($"ev_{mi}");
                if (ImGui.Button(label, new Vector2(-1, 22f)))
                {
                    try
                    {
                        if (m.GetParameters().Length == 0) m.Invoke(component, null);
                        else EngineEditor.LogMessage($"Error: {m.Name} requires parameters");
                    }
                    catch (Exception ex) { EngineEditor.LogMessage($"Error: {ex.Message}"); }
                }
                ImGui.PopID();
                mi++;
            }
        }

        private static OpenTK.Mathematics.Vector3 ClampVec3(Vector3 v) =>
            new(Math.Max(0.01f, v.X), Math.Max(0.01f, v.Y), Math.Max(0.01f, v.Z));

        private static void DrawRigidbodyInspector(KrayonCore.Rigidbody rb)
        {
            string[] motionNames = Enum.GetNames(typeof(BodyMotionType));
            int motionIdx = (int)rb.MotionType;
            BeginFieldRow("Motion Type");
            if (ImGui.Combo("##motiontype", ref motionIdx, motionNames, motionNames.Length))
                rb.MotionType = (BodyMotionType)motionIdx;

            ImGui.Spacing();
            bool kin = rb.IsKinematic; if (ImGui.Checkbox("Kinematic", ref kin)) rb.IsKinematic = kin; ImGui.SameLine();
            bool trig = rb.IsTrigger; if (ImGui.Checkbox("Trigger", ref trig)) rb.IsTrigger = trig; ImGui.SameLine();
            bool grav = rb.UseGravity; if (ImGui.Checkbox("Gravity", ref grav)) rb.UseGravity = grav;
            ImGui.Spacing();

            float mass = rb.Mass;
            BeginFieldRow("Mass");
            if (ImGui.DragFloat("##mass", ref mass, 0.1f, 0.01f, 1000f)) rb.Mass = Math.Max(0.01f, mass);

            float sleep = rb.SleepThreshold;
            BeginFieldRow("Sleep Threshold");
            if (ImGui.DragFloat("##sleep", ref sleep, 0.001f, 0f, 1f, "%.4f")) rb.SleepThreshold = Math.Max(0f, sleep);

            ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();

            string[] shapeNames = Enum.GetNames(typeof(ShapeType));
            int shapeIdx = (int)rb.ShapeType;
            BeginFieldRow("Shape");
            if (ImGui.Combo("##shapetype", ref shapeIdx, shapeNames, shapeNames.Length))
                rb.ShapeType = (ShapeType)shapeIdx;

            string shapeSizeLabel = rb.ShapeType switch
            {
                ShapeType.Sphere => "Radius",
                ShapeType.Capsule => "Radius / Height",
                _ => "Size"
            };

            var ss = rb.ShapeSize;
            var ssv = new Vector3(ss.X, ss.Y, ss.Z);

            switch (rb.ShapeType)
            {
                case ShapeType.Box:
                    BeginFieldRow(shapeSizeLabel);
                    if (ImGui.DragFloat3("##shapesize", ref ssv, 0.05f, 0.01f, 100f))
                        rb.ShapeSize = ClampVec3(ssv);
                    break;

                case ShapeType.Sphere:
                    float r = ssv.X;
                    BeginFieldRow(shapeSizeLabel);
                    if (ImGui.DragFloat("##shapesize_r", ref r, 0.05f, 0.01f, 100f))
                        rb.ShapeSize = new OpenTK.Mathematics.Vector3(Math.Max(0.01f, r), ss.Y, ss.Z);
                    break;

                case ShapeType.Capsule:
                    float cr = ssv.X, ch = ssv.Y;
                    float fw = (ImGui.GetContentRegionAvail().X - LabelColumnWidth) * 0.5f - 3f;
                    ImGui.Text(shapeSizeLabel); ImGui.SameLine(LabelColumnWidth);
                    ImGui.SetNextItemWidth(fw);
                    if (ImGui.DragFloat("##caps_r", ref cr, 0.05f, 0.01f, 100f, "R:%.2f"))
                        rb.ShapeSize = new OpenTK.Mathematics.Vector3(Math.Max(0.01f, cr), ss.Y, ss.Z);
                    ImGui.SameLine(0, 6f);
                    ImGui.SetNextItemWidth(fw);
                    if (ImGui.DragFloat("##caps_h", ref ch, 0.05f, 0.01f, 100f, "H:%.2f"))
                        rb.ShapeSize = new OpenTK.Mathematics.Vector3(ss.X, Math.Max(0.01f, ch), ss.Z);
                    ImGui.SameLine(0, 6f);
                    ImGui.TextUnformatted($"({ch * 2f + cr * 2f:F2} total)");
                    break;
            }

            ImGui.Spacing();

            var co = rb.ColliderOffset;
            var cov = new Vector3(co.X, co.Y, co.Z);
            BeginFieldRow("Center");
            if (ImGui.DragFloat3("##coloffset", ref cov, 0.01f))
                rb.ColliderOffset = new OpenTK.Mathematics.Vector3(cov.X, cov.Y, cov.Z);
            if (cov != Vector3.Zero)
            {
                ImGui.SameLine();
                if (ImGui.Button("⌀##reset_offset", new Vector2(22f, 0f)))
                    rb.ColliderOffset = OpenTK.Mathematics.Vector3.Zero;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset Center to (0, 0, 0)");
            }

            ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
            DrawPhysicsLayerSelector("Layer", rb);

            ImGui.Spacing();
            if (ImGui.TreeNodeEx("Constraints", ImGuiTreeNodeFlags.SpanAvailWidth))
            { DrawRigidbodyConstraints(rb); ImGui.TreePop(); }
            if (ImGui.TreeNodeEx("Physics Material", ImGuiTreeNodeFlags.SpanAvailWidth))
            { DrawRigidbodyPhysicsProperties(rb); ImGui.TreePop(); }
        }

        private static void DrawPhysicsLayerSelector(string label, KrayonCore.Rigidbody rb)
        {
            PhysicsLayer cur = rb.Layer;
            BeginFieldRowFixed(label, ImGui.GetContentRegionAvail().X);
            if (ImGui.Button($"{GetLayerPreviewText(cur)}##lyr_btn", new Vector2(ImGui.CalcItemWidth(), 0)))
                ImGui.OpenPopup("LayerPopup");

            if (ImGui.BeginPopup("LayerPopup"))
            {
                if (ImGui.Button("All", new Vector2(55, 0))) rb.Layer = PhysicsLayer.All;
                ImGui.SameLine();
                if (ImGui.Button("None", new Vector2(55, 0))) rb.Layer = PhysicsLayer.None;
                ImGui.Separator();
                var names = Enum.GetNames(typeof(PhysicsLayer));
                var values = (PhysicsLayer[])Enum.GetValues(typeof(PhysicsLayer));
                for (int i = 0; i < names.Length; i++)
                {
                    if (values[i] == PhysicsLayer.None || values[i] == PhysicsLayer.All) continue;
                    bool set = (cur & values[i]) != 0;
                    if (ImGui.Checkbox(names[i], ref set))
                    { rb.Layer = set ? cur | values[i] : cur & ~values[i]; cur = rb.Layer; }
                }
                ImGui.EndPopup();
            }
        }

        private static string GetLayerPreviewText(PhysicsLayer layer)
        {
            if (layer == PhysicsLayer.None) return "None";
            if (layer == PhysicsLayer.All) return "All";
            var list = new System.Collections.Generic.List<string>();
            var names = Enum.GetNames(typeof(PhysicsLayer));
            var values = (PhysicsLayer[])Enum.GetValues(typeof(PhysicsLayer));
            for (int i = 0; i < names.Length; i++)
            {
                if (values[i] == PhysicsLayer.None || values[i] == PhysicsLayer.All) continue;
                if ((layer & values[i]) != 0) list.Add(names[i]);
            }
            if (list.Count == 0) return "None";
            if (list.Count <= 2) return string.Join(", ", list);
            return $"{list[0]}, {list[1]} +{list.Count - 2}";
        }

        private static void DrawRigidbodyConstraints(KrayonCore.Rigidbody rb)
        {
            ImGui.TextUnformatted("Freeze Position");
            bool fpx = rb.FreezePositionX, fpy = rb.FreezePositionY, fpz = rb.FreezePositionZ;
            ImGui.SameLine(LabelColumnWidth);
            if (ImGui.Checkbox("X##fpx", ref fpx)) rb.FreezePositionX = fpx; ImGui.SameLine();
            if (ImGui.Checkbox("Y##fpy", ref fpy)) rb.FreezePositionY = fpy; ImGui.SameLine();
            if (ImGui.Checkbox("Z##fpz", ref fpz)) rb.FreezePositionZ = fpz;

            ImGui.TextUnformatted("Freeze Rotation");
            bool frx = rb.FreezeRotationX, fry = rb.FreezeRotationY, frz = rb.FreezeRotationZ;
            ImGui.SameLine(LabelColumnWidth);
            if (ImGui.Checkbox("X##frx", ref frx)) rb.FreezeRotationX = frx; ImGui.SameLine();
            if (ImGui.Checkbox("Y##fry", ref fry)) rb.FreezeRotationY = fry; ImGui.SameLine();
            if (ImGui.Checkbox("Z##frz", ref frz)) rb.FreezeRotationZ = frz;
        }

        private static void DrawRigidbodyPhysicsProperties(KrayonCore.Rigidbody rb)
        {
            float ld = rb.LinearDamping; BeginFieldRow("Linear Damping");
            if (ImGui.DragFloat("##ld", ref ld, 0.01f, 0f, 1f)) rb.LinearDamping = Math.Max(0f, ld);

            float ad = rb.AngularDamping; BeginFieldRow("Angular Damping");
            if (ImGui.DragFloat("##ad", ref ad, 0.01f, 0f, 1f)) rb.AngularDamping = Math.Max(0f, ad);

            float fr = rb.Friction; BeginFieldRow("Friction");
            if (ImGui.SliderFloat("##fr", ref fr, 0f, 1f)) rb.Friction = fr;

            float re = rb.Restitution; BeginFieldRow("Restitution");
            if (ImGui.SliderFloat("##re", ref re, 0f, 1f)) rb.Restitution = re;
        }

        public static void DrawProperty(object comp, PropertyInfo prop)
        {
            Type t = prop.PropertyType;
            object? val = prop.GetValue(comp);

            if (t.IsArray) { DrawArrayProperty(comp, prop, val); return; }
            if (t.IsEnum) { DrawEnumProperty(comp, prop, val); return; }
            if (val == null) { DrawLabelValue(prop.Name, "null"); return; }

            var range = prop.GetCustomAttribute<KrayonCore.RangeAttribute>();

            if (t == typeof(bool))
            {
                bool v = (bool)val; BeginFieldRow(prop.Name);
                if (ImGui.Checkbox($"##{prop.Name}", ref v)) prop.SetValue(comp, v);
            }
            else if (t == typeof(float))
            {
                float v = (float)val; BeginFieldRow(prop.Name);
                bool changed = range != null
                    ? ImGui.DragFloat($"##{prop.Name}", ref v, 0.01f, range.Min, range.Max)
                    : ImGui.DragFloat($"##{prop.Name}", ref v, 0.01f);
                if (changed) prop.SetValue(comp, v);
            }
            else if (t == typeof(int))
            {
                int v = (int)val; BeginFieldRow(prop.Name);
                bool changed = range != null
                    ? ImGui.DragInt($"##{prop.Name}", ref v, 1f, (int)range.Min, (int)range.Max)
                    : ImGui.DragInt($"##{prop.Name}", ref v);
                if (changed) prop.SetValue(comp, v);
            }
            else if (t == typeof(string))
            {
                if (prop.GetCustomAttribute<MaterialRefAttribute>() != null)
                {
                    string nv = DrawMaterialStringField(prop.Name, (string)val);
                    if (nv != (string)val) prop.SetValue(comp, nv);
                }
                else
                {
                    string nv = DrawAssetStringField(prop.Name, (string)val);
                    if (nv != (string)val) prop.SetValue(comp, nv);
                }
            }
            else if (t == typeof(Vector2))
            {
                Vector2 v = (Vector2)val; BeginFieldRow(prop.Name);
                if (ImGui.DragFloat2($"##{prop.Name}", ref v, 0.01f)) prop.SetValue(comp, v);
            }
            else if (t == typeof(Vector3))
            {
                Vector3 v = (Vector3)val; BeginFieldRow(prop.Name);
                if (ImGui.DragFloat3($"##{prop.Name}", ref v, 0.01f)) prop.SetValue(comp, v);
            }
            else if (t == typeof(OpenTK.Mathematics.Vector3))
            {
                var otk = (OpenTK.Mathematics.Vector3)val;
                Vector3 v = new(otk.X, otk.Y, otk.Z); BeginFieldRow(prop.Name);
                if (ImGui.DragFloat3($"##{prop.Name}", ref v, 0.01f))
                    prop.SetValue(comp, new OpenTK.Mathematics.Vector3(v.X, v.Y, v.Z));
            }
            else if (t == typeof(Vector4))
            {
                Vector4 v = (Vector4)val; BeginFieldRow(prop.Name);
                if (ImGui.InputFloat4($"##{prop.Name}", ref v)) prop.SetValue(comp, v);
            }
            else if (t == typeof(Quaternion))
            {
                var q = (Quaternion)val; Vector4 v = new(q.X, q.Y, q.Z, q.W); BeginFieldRow(prop.Name);
                if (ImGui.InputFloat4($"##{prop.Name}", ref v))
                    prop.SetValue(comp, new Quaternion(v.X, v.Y, v.Z, v.W));
            }
            else if (t == typeof(OpenTK.Mathematics.Quaternion))
            {
                var q = (OpenTK.Mathematics.Quaternion)val; Vector4 v = new(q.X, q.Y, q.Z, q.W); BeginFieldRow(prop.Name);
                if (ImGui.InputFloat4($"##{prop.Name}", ref v))
                    prop.SetValue(comp, new OpenTK.Mathematics.Quaternion(v.X, v.Y, v.Z, v.W));
            }
            else if (t == typeof(GameObject))
                DrawObjectRefField(prop.Name, (GameObject)val, go => prop.SetValue(comp, go));
            else if (t == typeof(KrayonCore.Material))
                DrawMaterialRefField(prop.Name, (KrayonCore.Material)val, mat => prop.SetValue(comp, mat));
            else
                DrawLabelValue(prop.Name, val.ToString());
        }

        public static void DrawField(object comp, FieldInfo field)
        {
            Type t = field.FieldType;
            object? val = field.GetValue(comp);

            if (t.IsArray) { DrawArrayField(comp, field, val); return; }
            if (t.IsEnum) { DrawEnumField(comp, field, val); return; }
            if (val == null) { DrawLabelValue(field.Name, "null"); return; }

            var range = field.GetCustomAttribute<KrayonCore.RangeAttribute>();

            if (t == typeof(bool))
            {
                bool v = (bool)val; BeginFieldRow(field.Name);
                if (ImGui.Checkbox($"##{field.Name}", ref v)) field.SetValue(comp, v);
            }
            else if (t == typeof(float))
            {
                float v = (float)val; BeginFieldRow(field.Name);
                bool changed = range != null
                    ? ImGui.DragFloat($"##{field.Name}", ref v, 0.01f, range.Min, range.Max)
                    : ImGui.DragFloat($"##{field.Name}", ref v, 0.01f);
                if (changed) field.SetValue(comp, v);
            }
            else if (t == typeof(int))
            {
                int v = (int)val; BeginFieldRow(field.Name);
                bool changed = range != null
                    ? ImGui.DragInt($"##{field.Name}", ref v, 1f, (int)range.Min, (int)range.Max)
                    : ImGui.DragInt($"##{field.Name}", ref v);
                if (changed) field.SetValue(comp, v);
            }
            else if (t == typeof(string))
            {
                if (field.GetCustomAttribute<MaterialRefAttribute>() != null)
                {
                    string nv = DrawMaterialStringField(field.Name, (string)val);
                    if (nv != (string)val) field.SetValue(comp, nv);
                }
                else
                {
                    string nv = DrawAssetStringField(field.Name, (string)val);
                    if (nv != (string)val) field.SetValue(comp, nv);
                }
            }
            else if (t == typeof(Vector2))
            {
                Vector2 v = (Vector2)val; BeginFieldRow(field.Name);
                if (ImGui.DragFloat2($"##{field.Name}", ref v, 0.01f)) field.SetValue(comp, v);
            }
            else if (t == typeof(Vector3))
            {
                Vector3 v = (Vector3)val; BeginFieldRow(field.Name);
                if (ImGui.DragFloat3($"##{field.Name}", ref v, 0.01f)) field.SetValue(comp, v);
            }
            else if (t == typeof(OpenTK.Mathematics.Vector3))
            {
                var otk = (OpenTK.Mathematics.Vector3)val;
                Vector3 v = new(otk.X, otk.Y, otk.Z); BeginFieldRow(field.Name);
                if (ImGui.DragFloat3($"##{field.Name}", ref v, 0.01f))
                    field.SetValue(comp, new OpenTK.Mathematics.Vector3(v.X, v.Y, v.Z));
            }
            else if (t == typeof(Vector4))
            {
                Vector4 v = (Vector4)val; BeginFieldRow(field.Name);
                if (ImGui.InputFloat4($"##{field.Name}", ref v)) field.SetValue(comp, v);
            }
            else if (t == typeof(Quaternion))
            {
                var q = (Quaternion)val; Vector4 v = new(q.X, q.Y, q.Z, q.W); BeginFieldRow(field.Name);
                if (ImGui.InputFloat4($"##{field.Name}", ref v))
                    field.SetValue(comp, new Quaternion(v.X, v.Y, v.Z, v.W));
            }
            else if (t == typeof(OpenTK.Mathematics.Quaternion))
            {
                var q = (OpenTK.Mathematics.Quaternion)val; Vector4 v = new(q.X, q.Y, q.Z, q.W); BeginFieldRow(field.Name);
                if (ImGui.InputFloat4($"##{field.Name}", ref v))
                    field.SetValue(comp, new OpenTK.Mathematics.Quaternion(v.X, v.Y, v.Z, v.W));
            }
            else if (t == typeof(GameObject))
                DrawObjectRefField(field.Name, (GameObject)val, go => field.SetValue(comp, go));
            else if (t == typeof(KrayonCore.Material))
                DrawMaterialRefField(field.Name, (KrayonCore.Material)val, mat => field.SetValue(comp, mat));
            else
                DrawLabelValue(field.Name, val.ToString());
        }

        private static void DrawEnumProperty(object comp, PropertyInfo prop, object? val)
        {
            if (val == null) { DrawLabelValue(prop.Name, "null"); return; }
            if (prop.PropertyType.GetCustomAttribute<FlagsAttribute>() != null)
            {
                DrawFlagsField(prop.Name, prop.PropertyType, Convert.ToUInt32(val),
                    v => prop.SetValue(comp, Enum.ToObject(prop.PropertyType, v)));
                return;
            }
            string[] names = Enum.GetNames(prop.PropertyType);
            int idx = Array.IndexOf(names, val.ToString());
            BeginFieldRow(prop.Name);
            if (ImGui.Combo($"##{prop.Name}", ref idx, names, names.Length))
                prop.SetValue(comp, Enum.Parse(prop.PropertyType, names[idx]));
        }

        private static void DrawEnumField(object comp, FieldInfo field, object? val)
        {
            if (val == null) { DrawLabelValue(field.Name, "null"); return; }
            if (field.FieldType.GetCustomAttribute<FlagsAttribute>() != null)
            {
                DrawFlagsField(field.Name, field.FieldType, Convert.ToUInt32(val),
                    v => field.SetValue(comp, Enum.ToObject(field.FieldType, v)));
                return;
            }
            string[] names = Enum.GetNames(field.FieldType);
            int idx = Array.IndexOf(names, val.ToString());
            BeginFieldRow(field.Name);
            if (ImGui.Combo($"##{field.Name}", ref idx, names, names.Length))
                field.SetValue(comp, Enum.Parse(field.FieldType, names[idx]));
        }

        private static void DrawFlagsField(string label, Type enumType, uint cur, Action<uint> setter)
        {
            string preview = GetFlagsPreviewText(enumType, cur);
            BeginFieldRow(label);
            float w = ImGui.CalcItemWidth();
            if (ImGui.Button($"{preview}##flags_{label}", new Vector2(w, 0)))
                ImGui.OpenPopup($"flags_{label}");

            if (ImGui.BeginPopup($"flags_{label}"))
            {
                uint all = Convert.ToUInt32(Enum.ToObject(enumType, ~0u));
                if (ImGui.Button("All", new Vector2(55, 0))) { cur = all; setter(cur); }
                ImGui.SameLine();
                if (ImGui.Button("None", new Vector2(55, 0))) { cur = 0; setter(cur); }
                ImGui.Separator();
                string[] names = Enum.GetNames(enumType);
                Array values = Enum.GetValues(enumType);
                for (int i = 0; i < names.Length; i++)
                {
                    uint fv = Convert.ToUInt32(values.GetValue(i));
                    if (fv == 0 || fv == all) continue;
                    bool set = (cur & fv) != 0;
                    if (ImGui.Checkbox(names[i], ref set)) { cur = set ? cur | fv : cur & ~fv; setter(cur); }
                }
                ImGui.EndPopup();
            }
        }

        private static string GetFlagsPreviewText(Type enumType, uint value)
        {
            if (value == 0) return "None";
            uint all = Convert.ToUInt32(Enum.ToObject(enumType, ~0u));
            if (value == all) return "All";
            var list = new System.Collections.Generic.List<string>();
            string[] names = Enum.GetNames(enumType);
            Array values = Enum.GetValues(enumType);
            for (int i = 0; i < names.Length; i++)
            {
                uint fv = Convert.ToUInt32(values.GetValue(i));
                if (fv == 0 || fv == all) continue;
                if ((value & fv) != 0) list.Add(names[i]);
            }
            if (list.Count == 0) return "None";
            if (list.Count <= 2) return string.Join(", ", list);
            return $"{list[0]}, {list[1]} +{list.Count - 2}";
        }

        private static void DrawArrayProperty(object comp, PropertyInfo prop, object? val)
        {
            Type elemT = prop.PropertyType.GetElementType()!;
            Array? arr = val as Array;
            bool isMaterialRef = prop.GetCustomAttribute<MaterialRefAttribute>() != null;
            if (!ImGui.TreeNodeEx($"{prop.Name}##arrp", ImGuiTreeNodeFlags.SpanAvailWidth)) return;
            DrawArrayControls(ref arr, elemT, v => prop.SetValue(comp, v), isMaterialRef);
            ImGui.TreePop();
        }

        private static void DrawArrayField(object comp, FieldInfo field, object? val)
        {
            Type elemT = field.FieldType.GetElementType()!;
            Array? arr = val as Array;
            bool isMaterialRef = field.GetCustomAttribute<MaterialRefAttribute>() != null;
            if (!ImGui.TreeNodeEx($"{field.Name}##arrf", ImGuiTreeNodeFlags.SpanAvailWidth)) return;
            DrawArrayControls(ref arr, elemT, v => field.SetValue(comp, v), isMaterialRef);
            ImGui.TreePop();
        }

        private static void DrawArrayControls(ref Array? arr, Type elemT, Action<Array> setter, bool isMaterialRef = false)
        {
            int size = arr?.Length ?? 0;
            ImGui.SetNextItemWidth(80f);
            if (ImGui.InputInt("Size##arrsz", ref size))
            {
                size = Math.Max(0, size);
                Array na = Array.CreateInstance(elemT, size);
                if (arr != null) Array.Copy(arr, na, Math.Min(arr.Length, size));
                setter(na);
                arr = na;
            }
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++) { ImGui.PushID(i); DrawArrayElement(arr, i, elemT, isMaterialRef); ImGui.PopID(); }
        }

        private static void DrawArrayElement(Array arr, int i, Type elemT, bool isMaterialRef = false)
        {
            object? val = arr.GetValue(i);
            if (elemT == typeof(string))
            {
                string sv = (string?)val ?? "";
                string nv = isMaterialRef
                    ? DrawMaterialStringField($"[{i}]", sv)
                    : DrawAssetStringField($"[{i}]", sv);
                if (nv != sv) arr.SetValue(nv, i);
            }
            else if (elemT == typeof(int)) { int v = val != null ? (int)val : 0; BeginFieldRow($"[{i}]"); if (ImGui.InputInt($"##el{i}", ref v, 0)) arr.SetValue(v, i); }
            else if (elemT == typeof(float)) { float v = val != null ? (float)val : 0f; BeginFieldRow($"[{i}]"); if (ImGui.DragFloat($"##el{i}", ref v, 0.01f)) arr.SetValue(v, i); }
            else if (elemT == typeof(bool)) { bool v = val != null && (bool)val; BeginFieldRow($"[{i}]"); if (ImGui.Checkbox($"##el{i}", ref v)) arr.SetValue(v, i); }
            else if (elemT == typeof(Vector2)) { Vector2 v = val != null ? (Vector2)val : Vector2.Zero; BeginFieldRow($"[{i}]"); if (ImGui.DragFloat2($"##el{i}", ref v, 0.01f)) arr.SetValue(v, i); }
            else if (elemT == typeof(Vector3)) { Vector3 v = val != null ? (Vector3)val : Vector3.Zero; BeginFieldRow($"[{i}]"); if (ImGui.DragFloat3($"##el{i}", ref v, 0.01f)) arr.SetValue(v, i); }
            else if (elemT == typeof(OpenTK.Mathematics.Vector3))
            {
                var otk = val != null ? (OpenTK.Mathematics.Vector3)val : OpenTK.Mathematics.Vector3.Zero;
                Vector3 v = new(otk.X, otk.Y, otk.Z); BeginFieldRow($"[{i}]");
                if (ImGui.DragFloat3($"##el{i}", ref v, 0.01f))
                    arr.SetValue(new OpenTK.Mathematics.Vector3(v.X, v.Y, v.Z), i);
            }
            else if (elemT == typeof(GameObject))
                DrawObjectRefField($"[{i}]", val as GameObject, go => arr.SetValue(go, i));
            else if (elemT == typeof(KrayonCore.Material))
                DrawMaterialRefField($"[{i}]", val as KrayonCore.Material, mat => arr.SetValue(mat, i));
            else
                DrawLabelValue($"[{i}]", val?.ToString() ?? "null");
        }

        public static void DrawObjectRefField(string label, GameObject? current, Action<GameObject?> setter)
        {
            string display = current != null ? current.Name : "None";
            BeginFieldRow(label);
            float w = ImGui.CalcItemWidth();
            if (ImGui.Button($"{display}##go_{label}", new Vector2(w, 0)))
                ImGui.OpenPopup($"gopop_{label}");
            if (ImGui.BeginPopup($"gopop_{label}"))
            {
                if (ImGui.MenuItem("None")) setter(null);
                ImGui.Separator();
                int idx = 0;
                foreach (var obj in SceneManager.PrimaryScene?.GetAllGameObjects() ?? new System.Collections.Generic.List<GameObject>())
                { ImGui.PushID(idx++); if (ImGui.MenuItem(obj.Name)) setter(obj); ImGui.PopID(); }
                ImGui.EndPopup();
            }
        }

        public static void DrawMaterialRefField(string label, KrayonCore.Material? current, Action<KrayonCore.Material?> setter)
        {
            string display = current != null ? current.Name : "None";
            BeginFieldRow(label);
            float w = ImGui.CalcItemWidth();
            if (ImGui.Button($"{display}##mat_{label}", new Vector2(w, 0)))
                ImGui.OpenPopup($"matpop_{label}");
            if (ImGui.BeginPopup($"matpop_{label}"))
            {
                if (ImGui.MenuItem("None")) setter(null);
                ImGui.Separator();
                int idx = 0;
                foreach (var mat in GraphicsEngine.Instance.Materials.GetAll())
                { ImGui.PushID(idx++); if (ImGui.MenuItem(mat.Name)) setter(mat); ImGui.PopID(); }
                ImGui.EndPopup();
            }
        }
    }
}