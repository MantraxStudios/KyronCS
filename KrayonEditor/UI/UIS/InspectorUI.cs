using ImGuiNET;
using KrayonCore;
using KrayonCore.Physics;
using System;
using System.Numerics;
using System.Reflection;
using KrayonEditor;
using KrayonCore.GraphicsData;
using KrayonCore.Core.Attributes;
using System.IO;
using KrayonEditor.Main;

namespace KrayonEditor.UI
{
    public class InspectorUI : UIBehaviour
    {
        // ── Paleta de colores estilo Unity Dark ──────────────────────────────
        private static readonly Vector4 ColHeader = new(0.17f, 0.17f, 0.17f, 1f);
        private static readonly Vector4 ColHeaderHover = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Vector4 ColHeaderActive = new(0.14f, 0.14f, 0.14f, 1f);
        private static readonly Vector4 ColField = new(0.13f, 0.13f, 0.13f, 1f);
        private static readonly Vector4 ColFieldHover = new(0.19f, 0.19f, 0.19f, 1f);
        private static readonly Vector4 ColAccent = new(0.22f, 0.47f, 0.82f, 1f);
        private static readonly Vector4 ColAccentHover = new(0.27f, 0.55f, 0.93f, 1f);
        private static readonly Vector4 ColDanger = new(0.65f, 0.18f, 0.18f, 1f);
        private static readonly Vector4 ColDangerHover = new(0.78f, 0.22f, 0.22f, 1f);
        private static readonly Vector4 ColTextMain = new(0.88f, 0.88f, 0.88f, 1f);
        private static readonly Vector4 ColTextDim = new(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Vector4 ColTextDisabled = new(0.40f, 0.40f, 0.40f, 1f);

        // Ancho fijo de la columna de labels (igual en todos los campos)
        private const float LabelColumnWidth = 148f;

        // ════════════════════════════════════════════════════════════════════
        //  APPLY THEME
        // ════════════════════════════════════════════════════════════════════

        private static void PushUnityTheme()
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.20f, 0.20f, 0.20f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.17f, 0.17f, 0.17f, 1f));
            ImGui.PushStyleColor(ImGuiCol.FrameBg, ColField);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, ColFieldHover);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.24f, 0.24f, 0.24f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Header, ColHeader);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ColHeaderHover);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, ColHeaderActive);
            ImGui.PushStyleColor(ImGuiCol.Button, ColField);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColFieldHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.24f, 0.24f, 0.24f, 1f));
            ImGui.PushStyleColor(ImGuiCol.CheckMark, ColAccent);
            ImGui.PushStyleColor(ImGuiCol.SliderGrab, ColAccent);
            ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, ColAccentHover);
            ImGui.PushStyleColor(ImGuiCol.Text, ColTextMain);
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.28f, 0.28f, 0.28f, 1f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6f, 3f));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6f, 4f));
            ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, 14f);
        }

        private static void PopUnityTheme()
        {
            ImGui.PopStyleVar(4);
            ImGui.PopStyleColor(16);
        }

        // ════════════════════════════════════════════════════════════════════
        //  LABEL + FIELD ROW  (columna fija a la izquierda como Unity)
        // ════════════════════════════════════════════════════════════════════

        private static void BeginFieldRow(string label)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ColTextDim);
            ImGui.Text(label);
            ImGui.PopStyleColor();
            ImGui.SameLine(LabelColumnWidth);
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        }

        private static void BeginFieldRowFixed(string label, float fieldWidth)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ColTextDim);
            ImGui.Text(label);
            ImGui.PopStyleColor();
            ImGui.SameLine(LabelColumnWidth);
            ImGui.SetNextItemWidth(fieldWidth);
        }

        // ════════════════════════════════════════════════════════════════════
        //  ASSET STRING FIELD  (estilo Unity: rect arrastrable + boton X)
        // ════════════════════════════════════════════════════════════════════

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

        private static string DrawAssetStringField(string label, string rawValue)
        {
            bool isGuid = Guid.TryParse(rawValue, out _);
            string resolved = ResolveGuidLabel(rawValue);
            bool hasAsset = isGuid && resolved != rawValue;

            float clearW = 20f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float fieldW = ImGui.GetContentRegionAvail().X - LabelColumnWidth - clearW - spacing;
            if (fieldW < 40f) fieldW = 40f;

            ImGui.PushStyleColor(ImGuiCol.Text, ColTextDim);
            ImGui.Text(label);
            ImGui.PopStyleColor();
            ImGui.SameLine(LabelColumnWidth);

            if (hasAsset)
            {
                var dl = ImGui.GetWindowDrawList();
                var pos = ImGui.GetCursorScreenPos();
                float h = ImGui.GetFrameHeight();
                float radius = 3f;

                uint bgCol = ImGui.ColorConvertFloat4ToU32(ColField);
                uint brCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.35f, 0.35f, 0.35f, 1f));
                uint dotCol = ImGui.ColorConvertFloat4ToU32(ColAccent);
                uint txtCol = ImGui.ColorConvertFloat4ToU32(ColTextMain);

                dl.AddRectFilled(pos, new Vector2(pos.X + fieldW, pos.Y + h), bgCol, radius);
                dl.AddRect(pos, new Vector2(pos.X + fieldW, pos.Y + h), brCol, radius);

                // Punto de color acento como icono
                float dotR = h * 0.20f;
                dl.AddCircleFilled(new Vector2(pos.X + h * 0.5f, pos.Y + h * 0.5f), dotR, dotCol);

                // Nombre truncado
                string txt = resolved;
                float maxTW = fieldW - h * 0.9f - 4f;
                while (txt.Length > 4 && ImGui.CalcTextSize(txt).X > maxTW)
                    txt = txt[..^4] + "...";
                dl.AddText(new Vector2(pos.X + h * 0.9f, pos.Y + (h - ImGui.GetTextLineHeight()) * 0.5f), txtCol, txt);

                ImGui.InvisibleButton($"##asset_{label}", new Vector2(fieldW, h));

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.SetTooltip($"{resolved}\n{rawValue}");
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
                ImGui.PushStyleColor(ImGuiCol.Button, ColField);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColDanger);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, ColDangerHover);
                ImGui.PushStyleColor(ImGuiCol.Text, ColTextDim);
                if (ImGui.Button($"x##{label}_clr", new Vector2(clearW, h)))
                    rawValue = "";
                ImGui.PopStyleColor(4);
            }
            else
            {
                ImGui.SetNextItemWidth(fieldW + clearW + spacing);
                ImGui.InputText($"##{label}_in", ref rawValue, 512);

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
            }

            return rawValue;
        }

        // ════════════════════════════════════════════════════════════════════
        //  COMPONENT HEADER  (barra oscura con nombre, como Unity)
        // ════════════════════════════════════════════════════════════════════

        private static bool DrawComponentHeader(string name, bool isEnabled)
        {
            uint bgCol = ImGui.ColorConvertFloat4ToU32(
                isEnabled ? new Vector4(0.23f, 0.23f, 0.23f, 1f)
                          : new Vector4(0.18f, 0.18f, 0.18f, 1f));

            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.23f, 0.23f, 0.23f, 1f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.27f, 0.27f, 0.27f, 1f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.20f, 0.20f, 0.20f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text,
                isEnabled ? ColTextMain : ColTextDisabled);

            bool open = ImGui.CollapsingHeader(name, ImGuiTreeNodeFlags.DefaultOpen);

            ImGui.PopStyleColor(4);
            return open;
        }

        // ════════════════════════════════════════════════════════════════════
        //  ON DRAW UI
        // ════════════════════════════════════════════════════════════════════

        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            PushUnityTheme();
            ImGui.Begin("Inspector", ref _isVisible);

            if (EditorActions.SelectedObject != null)
            {
                DrawObjectHeader();
                ImGui.Spacing();
                DrawTransformComponent();
                DrawComponents();
                ImGui.Spacing();
                DrawAddComponentButton();
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ColTextDisabled);
                ImGui.TextUnformatted("No object selected");
                ImGui.PopStyleColor();
            }

            ImGui.End();
            PopUnityTheme();

            if (EditorActions.SelectedObject != null &&
                GraphicsEngine.Instance.GetKeyboardState()
                    .IsKeyPressed(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Delete) && EditorActions.IsHoveringScene)
            {
                SceneManager.ActiveScene.DestroyGameObject(EditorActions.SelectedObject);
                EditorActions.SelectedObject = null;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  OBJECT HEADER  (Name + Tag en una fila)
        // ════════════════════════════════════════════════════════════════════

        private void DrawObjectHeader()
        {
            float avail = ImGui.GetContentRegionAvail().X;
            float tagW = 90f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float nameW = avail - tagW - spacing;

            ImGui.SetNextItemWidth(nameW);
            string name = EditorActions.SelectedObject!.Name;
            if (ImGui.InputText("##obj_name", ref name, 256))
                EditorActions.SelectedObject.Name = name;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Name");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(tagW);
            string tag = EditorActions.SelectedObject.Tag;
            if (ImGui.InputText("##obj_tag", ref tag, 128))
                EditorActions.SelectedObject.Tag = tag;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Tag");

            ImGui.Spacing();
            ImGui.Separator();
        }

        // ════════════════════════════════════════════════════════════════════
        //  TRANSFORM
        // ════════════════════════════════════════════════════════════════════

        private void DrawTransformComponent()
        {
            ImGui.PushID("Transform");

            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.23f, 0.23f, 0.23f, 1f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.27f, 0.27f, 0.27f, 1f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.20f, 0.20f, 0.20f, 1f));
            bool open = ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen);
            ImGui.PopStyleColor(3);

            if (open)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ColTextDim);
                var t = EditorActions.SelectedObject!.Transform;

                Vector3 pos = new(t.X, t.Y, t.Z);
                BeginFieldRow("Position");
                if (ImGui.DragFloat3("##pos", ref pos, 0.1f))
                    t.SetPosition(pos.X, pos.Y, pos.Z);

                Vector3 rot = new(t.RotationX, t.RotationY, t.RotationZ);
                BeginFieldRow("Rotation");
                if (ImGui.DragFloat3("##rot", ref rot, 0.5f))
                    t.SetRotation(rot.X, rot.Y, rot.Z);

                Vector3 scl = new(t.ScaleX, t.ScaleY, t.ScaleZ);
                BeginFieldRow("Scale");
                if (ImGui.DragFloat3("##scl", ref scl, 0.01f))
                    t.SetScale(scl.X, scl.Y, scl.Z);

                ImGui.PopStyleColor();
                ImGui.Spacing();
            }

            ImGui.PopID();
        }

        // ════════════════════════════════════════════════════════════════════
        //  COMPONENTS LOOP
        // ════════════════════════════════════════════════════════════════════

        private void DrawComponents()
        {
            var components = EditorActions.SelectedObject!.GetAllComponents().ToList();
            int idx = 0;
            foreach (var component in components)
            {
                if (component.GetType().Name == "Transform") { idx++; continue; }
                ImGui.PushID($"Comp_{idx}");
                DrawComponentWithReflection(component);
                ImGui.PopID();
                idx++;
            }
        }

        private void DrawComponentWithReflection(object component)
        {
            Type ct = component.GetType();
            string name = ct.Name;
            var enabledProp = ct.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
            bool hasEnabled = enabledProp != null && enabledProp.PropertyType == typeof(bool);
            bool isEnabled = hasEnabled ? (bool)enabledProp!.GetValue(component)! : true;

            bool open = DrawComponentHeader(name, isEnabled);

            // Context menu (clic derecho en el header)
            if (ImGui.BeginPopupContextItem($"ctx_{name}"))
            {
                if (hasEnabled)
                {
                    if (ImGui.MenuItem(isEnabled ? "Disable" : "Enable"))
                        enabledProp!.SetValue(component, !isEnabled);
                    ImGui.Separator();
                }
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.35f, 0.35f, 1f));
                if (ImGui.MenuItem("Remove Component"))
                {
                    ImGui.PopStyleColor();
                    ImGui.EndPopup();
                    EditorActions.SelectedObject!.RemoveComponent((Component)component);
                    EngineEditor.LogMessage($"Removed {name}");
                    return;
                }
                ImGui.PopStyleColor();
                ImGui.EndPopup();
            }

            if (!open) return;

            ImGui.PushStyleColor(ImGuiCol.Text, isEnabled ? ColTextMain : ColTextDisabled);
            ImGui.Indent(4f);

            if (component is KrayonCore.Rigidbody rb)
            {
                DrawRigidbodyInspector(rb);
            }
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
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }

        // ════════════════════════════════════════════════════════════════════
        //  CALL EVENT BUTTONS
        // ════════════════════════════════════════════════════════════════════

        private void DrawCallEventMethods(object component, Type ct)
        {
            var methods = ct.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            bool any = false;
            foreach (var m in methods)
                if (m.GetCustomAttribute<CallEventAttribute>() != null) { any = true; break; }
            if (!any) return;

            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, ColTextDim);
            ImGui.TextUnformatted("Events");
            ImGui.PopStyleColor();
            ImGui.Separator();

            int mi = 0;
            foreach (var m in methods)
            {
                var attr = m.GetCustomAttribute<CallEventAttribute>();
                if (attr == null) continue;
                string label = string.IsNullOrEmpty(attr.DisplayName) ? m.Name : attr.DisplayName;

                ImGui.PushID($"ev_{mi}");
                ImGui.PushStyleColor(ImGuiCol.Button, ColAccent);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColAccentHover);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.18f, 0.40f, 0.72f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
                if (ImGui.Button(label, new Vector2(-1, 22f)))
                {
                    try
                    {
                        if (m.GetParameters().Length == 0) m.Invoke(component, null);
                        else EngineEditor.LogMessage($"Error: {m.Name} requires parameters");
                    }
                    catch (Exception ex) { EngineEditor.LogMessage($"Error: {ex.Message}"); }
                }
                ImGui.PopStyleColor(4);
                ImGui.PopID();
                mi++;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  RIGIDBODY  — reemplazar el método completo DrawRigidbodyInspector
        // ════════════════════════════════════════════════════════════════════

        private static OpenTK.Mathematics.Vector3 ClampVec3(Vector3 v) =>
            new(Math.Max(0.01f, v.X), Math.Max(0.01f, v.Y), Math.Max(0.01f, v.Z));

        private void DrawRigidbodyInspector(KrayonCore.Rigidbody rb)
        {
            // ── Motion Type ───────────────────────────────────────────────
            string[] motionNames = Enum.GetNames(typeof(BodyMotionType));
            int motionIdx = (int)rb.MotionType;
            BeginFieldRow("Motion Type");
            if (ImGui.Combo("##motiontype", ref motionIdx, motionNames, motionNames.Length))
                rb.MotionType = (BodyMotionType)motionIdx;

            // ── Flags (Kinematic / Trigger / Gravity) ─────────────────────
            ImGui.Spacing();
            bool kin = rb.IsKinematic;
            if (ImGui.Checkbox("Kinematic", ref kin)) rb.IsKinematic = kin;
            ImGui.SameLine();
            bool trig = rb.IsTrigger;
            if (ImGui.Checkbox("Trigger", ref trig)) rb.IsTrigger = trig;
            ImGui.SameLine();
            bool grav = rb.UseGravity;
            if (ImGui.Checkbox("Gravity", ref grav)) rb.UseGravity = grav;
            ImGui.Spacing();

            // ── Mass / Sleep ──────────────────────────────────────────────
            float mass = rb.Mass;
            BeginFieldRow("Mass");
            if (ImGui.DragFloat("##mass", ref mass, 0.1f, 0.01f, 1000f))
                rb.Mass = Math.Max(0.01f, mass);

            float sleep = rb.SleepThreshold;
            BeginFieldRow("Sleep Threshold");
            if (ImGui.DragFloat("##sleep", ref sleep, 0.001f, 0f, 1f, "%.4f"))
                rb.SleepThreshold = Math.Max(0f, sleep);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // ── Shape ─────────────────────────────────────────────────────
            string[] shapeNames = Enum.GetNames(typeof(ShapeType));
            int shapeIdx = (int)rb.ShapeType;
            BeginFieldRow("Shape");
            if (ImGui.Combo("##shapetype", ref shapeIdx, shapeNames, shapeNames.Length))
                rb.ShapeType = (ShapeType)shapeIdx;

            // Shape Size — label dinámico según la forma
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
                // Box: X Y Z completo
                case ShapeType.Box:
                    {
                        BeginFieldRow(shapeSizeLabel);
                        if (ImGui.DragFloat3("##shapesize", ref ssv, 0.05f, 0.01f, 100f))
                            rb.ShapeSize = ClampVec3(ssv);
                        break;
                    }

                // Sphere: solo radio (X)
                case ShapeType.Sphere:
                    {
                        float r = ssv.X;
                        BeginFieldRow(shapeSizeLabel);
                        if (ImGui.DragFloat("##shapesize_r", ref r, 0.05f, 0.01f, 100f))
                            rb.ShapeSize = new OpenTK.Mathematics.Vector3(
                                Math.Max(0.01f, r), ss.Y, ss.Z);
                        break;
                    }

                // Capsule: X = radio, Y = half-length del cilindro
                case ShapeType.Capsule:
                    {
                        float r = ssv.X;
                        float h = ssv.Y;
                        float fieldW = (ImGui.GetContentRegionAvail().X - LabelColumnWidth) * 0.5f - 3f;

                        ImGui.PushStyleColor(ImGuiCol.Text, ColTextDim);
                        ImGui.Text(shapeSizeLabel);
                        ImGui.PopStyleColor();
                        ImGui.SameLine(LabelColumnWidth);

                        ImGui.SetNextItemWidth(fieldW);
                        if (ImGui.DragFloat("##caps_r", ref r, 0.05f, 0.01f, 100f, "R:%.2f"))
                            rb.ShapeSize = new OpenTK.Mathematics.Vector3(
                                Math.Max(0.01f, r), ss.Y, ss.Z);

                        ImGui.SameLine(0, 6f);
                        ImGui.SetNextItemWidth(fieldW);
                        if (ImGui.DragFloat("##caps_h", ref h, 0.05f, 0.01f, 100f, "H:%.2f"))
                            rb.ShapeSize = new OpenTK.Mathematics.Vector3(
                                ss.X, Math.Max(0.01f, h), ss.Z);

                        // Indicador visual de altura total = cilindro + 2 hemisferios
                        float totalH = h * 2f + r * 2f;
                        ImGui.SameLine(0, 6f);
                        ImGui.PushStyleColor(ImGuiCol.Text, ColTextDim);
                        ImGui.TextUnformatted($"({totalH:F2} total)");
                        ImGui.PopStyleColor();
                        break;
                    }
            }

            // ── Collider Offset  (igual que Unity: Center X Y Z) ──────────
            ImGui.Spacing();

            var co = rb.ColliderOffset;
            var cov = new Vector3(co.X, co.Y, co.Z);
            BeginFieldRow("Center");
            if (ImGui.DragFloat3("##coloffset", ref cov, 0.01f))
                rb.ColliderOffset = new OpenTK.Mathematics.Vector3(cov.X, cov.Y, cov.Z);

            // Botón Reset offset (pequeño, a la derecha)
            if (cov != Vector3.Zero)
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.28f, 0.28f, 0.28f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.38f, 0.38f, 0.38f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Text, ColTextDim);
                if (ImGui.Button("⌀##reset_offset", new Vector2(22f, 0f)))
                    rb.ColliderOffset = OpenTK.Mathematics.Vector3.Zero;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset Center to (0, 0, 0)");
                ImGui.PopStyleColor(3);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // ── Layer ─────────────────────────────────────────────────────
            DrawPhysicsLayerSelector("Layer", rb);

            // ── Subpaneles plegables ──────────────────────────────────────
            ImGui.Spacing();
            if (ImGui.TreeNodeEx("Constraints", ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                DrawRigidbodyConstraints(rb);
                ImGui.TreePop();
            }
            if (ImGui.TreeNodeEx("Physics Material", ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                DrawRigidbodyPhysicsProperties(rb);
                ImGui.TreePop();
            }
        }


        private void DrawPhysicsLayerSelector(string label, KrayonCore.Rigidbody rb)
        {
            PhysicsLayer cur = rb.Layer;
            string preview = GetLayerPreviewText(cur);

            BeginFieldRowFixed(label, ImGui.GetContentRegionAvail().X);
            if (ImGui.Button($"{preview}##lyr_btn", new Vector2(ImGui.CalcItemWidth(), 0)))
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
                    {
                        rb.Layer = set ? cur | values[i] : cur & ~values[i];
                        cur = rb.Layer;
                    }
                }
                ImGui.EndPopup();
            }
        }

        private string GetLayerPreviewText(PhysicsLayer layer)
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

        private void DrawRigidbodyConstraints(KrayonCore.Rigidbody rb)
        {
            ImGui.TextUnformatted("Freeze Position");
            bool fpx = rb.FreezePositionX; bool fpy = rb.FreezePositionY; bool fpz = rb.FreezePositionZ;
            ImGui.SameLine(LabelColumnWidth);
            if (ImGui.Checkbox("X##fpx", ref fpx)) rb.FreezePositionX = fpx;
            ImGui.SameLine();
            if (ImGui.Checkbox("Y##fpy", ref fpy)) rb.FreezePositionY = fpy;
            ImGui.SameLine();
            if (ImGui.Checkbox("Z##fpz", ref fpz)) rb.FreezePositionZ = fpz;

            ImGui.TextUnformatted("Freeze Rotation");
            bool frx = rb.FreezeRotationX; bool fry = rb.FreezeRotationY; bool frz = rb.FreezeRotationZ;
            ImGui.SameLine(LabelColumnWidth);
            if (ImGui.Checkbox("X##frx", ref frx)) rb.FreezeRotationX = frx;
            ImGui.SameLine();
            if (ImGui.Checkbox("Y##fry", ref fry)) rb.FreezeRotationY = fry;
            ImGui.SameLine();
            if (ImGui.Checkbox("Z##frz", ref frz)) rb.FreezeRotationZ = frz;
        }

        private void DrawRigidbodyPhysicsProperties(KrayonCore.Rigidbody rb)
        {
            float ld = rb.LinearDamping;
            BeginFieldRow("Linear Damping");
            if (ImGui.DragFloat("##ld", ref ld, 0.01f, 0f, 1f)) rb.LinearDamping = Math.Max(0f, ld);

            float ad = rb.AngularDamping;
            BeginFieldRow("Angular Damping");
            if (ImGui.DragFloat("##ad", ref ad, 0.01f, 0f, 1f)) rb.AngularDamping = Math.Max(0f, ad);

            float fr = rb.Friction;
            BeginFieldRow("Friction");
            if (ImGui.SliderFloat("##fr", ref fr, 0f, 1f)) rb.Friction = fr;

            float re = rb.Restitution;
            BeginFieldRow("Restitution");
            if (ImGui.SliderFloat("##re", ref re, 0f, 1f)) rb.Restitution = re;
        }

        // ════════════════════════════════════════════════════════════════════
        //  DRAW PROPERTY  (por reflection)
        // ════════════════════════════════════════════════════════════════════

        private void DrawProperty(object comp, PropertyInfo prop)
        {
            Type t = prop.PropertyType;
            object? val = prop.GetValue(comp);

            if (t.IsArray) { DrawArrayProperty(comp, prop, val); return; }
            if (t.IsEnum) { DrawEnumProperty(comp, prop, val); return; }
            if (val == null) { DrawLabelValue(prop.Name, "null"); return; }

            var range = prop.GetCustomAttribute<KrayonCore.RangeAttribute>();

            if (t == typeof(bool))
            {
                bool v = (bool)val;
                BeginFieldRow(prop.Name);
                if (ImGui.Checkbox($"##{prop.Name}", ref v)) prop.SetValue(comp, v);
            }
            else if (t == typeof(float))
            {
                float v = (float)val;
                BeginFieldRow(prop.Name);
                bool changed = range != null
                    ? ImGui.DragFloat($"##{prop.Name}", ref v, 0.01f, range.Min, range.Max)
                    : ImGui.DragFloat($"##{prop.Name}", ref v, 0.01f);
                if (changed) prop.SetValue(comp, v);
            }
            else if (t == typeof(int))
            {
                int v = (int)val;
                BeginFieldRow(prop.Name);
                bool changed = range != null
                    ? ImGui.DragInt($"##{prop.Name}", ref v, 1f, (int)range.Min, (int)range.Max)
                    : ImGui.DragInt($"##{prop.Name}", ref v);
                if (changed) prop.SetValue(comp, v);
            }
            else if (t == typeof(string))
            {
                string nv = DrawAssetStringField(prop.Name, (string)val);
                if (nv != (string)val) prop.SetValue(comp, nv);
            }
            else if (t == typeof(Vector2))
            {
                Vector2 v = (Vector2)val;
                BeginFieldRow(prop.Name);
                if (ImGui.DragFloat2($"##{prop.Name}", ref v, 0.01f)) prop.SetValue(comp, v);
            }
            else if (t == typeof(Vector3))
            {
                Vector3 v = (Vector3)val;
                BeginFieldRow(prop.Name);
                if (ImGui.DragFloat3($"##{prop.Name}", ref v, 0.01f)) prop.SetValue(comp, v);
            }
            else if (t == typeof(OpenTK.Mathematics.Vector3))
            {
                var otk = (OpenTK.Mathematics.Vector3)val;
                Vector3 v = new(otk.X, otk.Y, otk.Z);
                BeginFieldRow(prop.Name);
                if (ImGui.DragFloat3($"##{prop.Name}", ref v, 0.01f))
                    prop.SetValue(comp, new OpenTK.Mathematics.Vector3(v.X, v.Y, v.Z));
            }
            else if (t == typeof(Vector4))
            {
                Vector4 v = (Vector4)val;
                BeginFieldRow(prop.Name);
                if (ImGui.InputFloat4($"##{prop.Name}", ref v)) prop.SetValue(comp, v);
            }
            else if (t == typeof(Quaternion))
            {
                var q = (Quaternion)val;
                Vector4 v = new(q.X, q.Y, q.Z, q.W);
                BeginFieldRow(prop.Name);
                if (ImGui.InputFloat4($"##{prop.Name}", ref v))
                    prop.SetValue(comp, new Quaternion(v.X, v.Y, v.Z, v.W));
            }
            else if (t == typeof(OpenTK.Mathematics.Quaternion))
            {
                var q = (OpenTK.Mathematics.Quaternion)val;
                Vector4 v = new(q.X, q.Y, q.Z, q.W);
                BeginFieldRow(prop.Name);
                if (ImGui.InputFloat4($"##{prop.Name}", ref v))
                    prop.SetValue(comp, new OpenTK.Mathematics.Quaternion(v.X, v.Y, v.Z, v.W));
            }
            else if (t == typeof(GameObject))
            {
                DrawObjectRefField(prop.Name, (GameObject)val,
                    go => prop.SetValue(comp, go));
            }
            else if (t == typeof(KrayonCore.Material))
            {
                DrawMaterialRefField(prop.Name, (KrayonCore.Material)val,
                    mat => prop.SetValue(comp, mat));
            }
            else
            {
                DrawLabelValue(prop.Name, val.ToString());
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DRAW FIELD  (por reflection)
        // ════════════════════════════════════════════════════════════════════

        private void DrawField(object comp, FieldInfo field)
        {
            Type t = field.FieldType;
            object? val = field.GetValue(comp);

            if (t.IsArray) { DrawArrayField(comp, field, val); return; }
            if (t.IsEnum) { DrawEnumField(comp, field, val); return; }
            if (val == null) { DrawLabelValue(field.Name, "null"); return; }

            var range = field.GetCustomAttribute<KrayonCore.RangeAttribute>();

            if (t == typeof(bool))
            {
                bool v = (bool)val;
                BeginFieldRow(field.Name);
                if (ImGui.Checkbox($"##{field.Name}", ref v)) field.SetValue(comp, v);
            }
            else if (t == typeof(float))
            {
                float v = (float)val;
                BeginFieldRow(field.Name);
                bool changed = range != null
                    ? ImGui.DragFloat($"##{field.Name}", ref v, 0.01f, range.Min, range.Max)
                    : ImGui.DragFloat($"##{field.Name}", ref v, 0.01f);
                if (changed) field.SetValue(comp, v);
            }
            else if (t == typeof(int))
            {
                int v = (int)val;
                BeginFieldRow(field.Name);
                bool changed = range != null
                    ? ImGui.DragInt($"##{field.Name}", ref v, 1f, (int)range.Min, (int)range.Max)
                    : ImGui.DragInt($"##{field.Name}", ref v);
                if (changed) field.SetValue(comp, v);
            }
            else if (t == typeof(string))
            {
                string nv = DrawAssetStringField(field.Name, (string)val);
                if (nv != (string)val) field.SetValue(comp, nv);
            }
            else if (t == typeof(Vector2))
            {
                Vector2 v = (Vector2)val;
                BeginFieldRow(field.Name);
                if (ImGui.DragFloat2($"##{field.Name}", ref v, 0.01f)) field.SetValue(comp, v);
            }
            else if (t == typeof(Vector3))
            {
                Vector3 v = (Vector3)val;
                BeginFieldRow(field.Name);
                if (ImGui.DragFloat3($"##{field.Name}", ref v, 0.01f)) field.SetValue(comp, v);
            }
            else if (t == typeof(OpenTK.Mathematics.Vector3))
            {
                var otk = (OpenTK.Mathematics.Vector3)val;
                Vector3 v = new(otk.X, otk.Y, otk.Z);
                BeginFieldRow(field.Name);
                if (ImGui.DragFloat3($"##{field.Name}", ref v, 0.01f))
                    field.SetValue(comp, new OpenTK.Mathematics.Vector3(v.X, v.Y, v.Z));
            }
            else if (t == typeof(Vector4))
            {
                Vector4 v = (Vector4)val;
                BeginFieldRow(field.Name);
                if (ImGui.InputFloat4($"##{field.Name}", ref v)) field.SetValue(comp, v);
            }
            else if (t == typeof(Quaternion))
            {
                var q = (Quaternion)val;
                Vector4 v = new(q.X, q.Y, q.Z, q.W);
                BeginFieldRow(field.Name);
                if (ImGui.InputFloat4($"##{field.Name}", ref v))
                    field.SetValue(comp, new Quaternion(v.X, v.Y, v.Z, v.W));
            }
            else if (t == typeof(OpenTK.Mathematics.Quaternion))
            {
                var q = (OpenTK.Mathematics.Quaternion)val;
                Vector4 v = new(q.X, q.Y, q.Z, q.W);
                BeginFieldRow(field.Name);
                if (ImGui.InputFloat4($"##{field.Name}", ref v))
                    field.SetValue(comp, new OpenTK.Mathematics.Quaternion(v.X, v.Y, v.Z, v.W));
            }
            else if (t == typeof(GameObject))
            {
                DrawObjectRefField(field.Name, (GameObject)val,
                    go => field.SetValue(comp, go));
            }
            else if (t == typeof(KrayonCore.Material))
            {
                DrawMaterialRefField(field.Name, (KrayonCore.Material)val,
                    mat => field.SetValue(comp, mat));
            }
            else
            {
                DrawLabelValue(field.Name, val.ToString());
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ENUM
        // ════════════════════════════════════════════════════════════════════

        private void DrawEnumProperty(object comp, PropertyInfo prop, object? val)
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

        private void DrawEnumField(object comp, FieldInfo field, object? val)
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

        private void DrawFlagsField(string label, Type enumType, uint cur, Action<uint> setter)
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
                    if (ImGui.Checkbox(names[i], ref set))
                    {
                        cur = set ? cur | fv : cur & ~fv;
                        setter(cur);
                    }
                }
                ImGui.EndPopup();
            }
        }

        private string GetFlagsPreviewText(Type enumType, uint value)
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

        // ════════════════════════════════════════════════════════════════════
        //  ARRAYS
        // ════════════════════════════════════════════════════════════════════

        private void DrawArrayProperty(object comp, PropertyInfo prop, object? val)
        {
            Type elemT = prop.PropertyType.GetElementType()!;
            Array? arr = val as Array;
            if (!ImGui.TreeNodeEx($"{prop.Name}##arrp", ImGuiTreeNodeFlags.SpanAvailWidth)) return;
            DrawArrayControls(ref arr, elemT, v => prop.SetValue(comp, v));
            ImGui.TreePop();
        }

        private void DrawArrayField(object comp, FieldInfo field, object? val)
        {
            Type elemT = field.FieldType.GetElementType()!;
            Array? arr = val as Array;
            if (!ImGui.TreeNodeEx($"{field.Name}##arrf", ImGuiTreeNodeFlags.SpanAvailWidth)) return;
            DrawArrayControls(ref arr, elemT, v => field.SetValue(comp, v));
            ImGui.TreePop();
        }

        private void DrawArrayControls(ref Array? arr, Type elemT, Action<Array> setter)
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
            for (int i = 0; i < arr.Length; i++)
            {
                ImGui.PushID(i);
                DrawArrayElement(arr, i, elemT);
                ImGui.PopID();
            }
        }

        private void DrawArrayElement(Array arr, int i, Type elemT)
        {
            object? val = arr.GetValue(i);

            if (elemT == typeof(string))
            {
                string sv = (string?)val ?? "";
                string nv = DrawAssetStringField($"[{i}]", sv);
                if (nv != sv) arr.SetValue(nv, i);
            }
            else if (elemT == typeof(int))
            {
                int v = val != null ? (int)val : 0;
                BeginFieldRow($"[{i}]");
                if (ImGui.InputInt($"##el{i}", ref v)) arr.SetValue(v, i);
            }
            else if (elemT == typeof(float))
            {
                float v = val != null ? (float)val : 0f;
                BeginFieldRow($"[{i}]");
                if (ImGui.DragFloat($"##el{i}", ref v, 0.01f)) arr.SetValue(v, i);
            }
            else if (elemT == typeof(bool))
            {
                bool v = val != null && (bool)val;
                BeginFieldRow($"[{i}]");
                if (ImGui.Checkbox($"##el{i}", ref v)) arr.SetValue(v, i);
            }
            else if (elemT == typeof(Vector2))
            {
                Vector2 v = val != null ? (Vector2)val : Vector2.Zero;
                BeginFieldRow($"[{i}]");
                if (ImGui.DragFloat2($"##el{i}", ref v, 0.01f)) arr.SetValue(v, i);
            }
            else if (elemT == typeof(Vector3))
            {
                Vector3 v = val != null ? (Vector3)val : Vector3.Zero;
                BeginFieldRow($"[{i}]");
                if (ImGui.DragFloat3($"##el{i}", ref v, 0.01f)) arr.SetValue(v, i);
            }
            else if (elemT == typeof(OpenTK.Mathematics.Vector3))
            {
                var otk = val != null ? (OpenTK.Mathematics.Vector3)val : OpenTK.Mathematics.Vector3.Zero;
                Vector3 v = new(otk.X, otk.Y, otk.Z);
                BeginFieldRow($"[{i}]");
                if (ImGui.DragFloat3($"##el{i}", ref v, 0.01f))
                    arr.SetValue(new OpenTK.Mathematics.Vector3(v.X, v.Y, v.Z), i);
            }
            else if (elemT == typeof(GameObject))
            {
                DrawObjectRefField($"[{i}]", val as GameObject, go => arr.SetValue(go, i));
            }
            else if (elemT == typeof(KrayonCore.Material))
            {
                DrawMaterialRefField($"[{i}]", val as KrayonCore.Material, mat => arr.SetValue(mat, i));
            }
            else
            {
                DrawLabelValue($"[{i}]", val?.ToString() ?? "null");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  REFERENCE FIELDS  (GameObject, Material)
        // ════════════════════════════════════════════════════════════════════

        private void DrawObjectRefField(string label, GameObject? current, Action<GameObject?> setter)
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
                foreach (var obj in SceneManager.ActiveScene?.GetAllGameObjects()
                         ?? new System.Collections.Generic.List<GameObject>())
                {
                    ImGui.PushID(idx++);
                    if (ImGui.MenuItem(obj.Name)) setter(obj);
                    ImGui.PopID();
                }
                ImGui.EndPopup();
            }
        }

        private void DrawMaterialRefField(string label, KrayonCore.Material? current, Action<KrayonCore.Material?> setter)
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
                {
                    ImGui.PushID(idx++);
                    if (ImGui.MenuItem(mat.Name)) setter(mat);
                    ImGui.PopID();
                }
                ImGui.EndPopup();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static void DrawLabelValue(string label, string value)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ColTextDim);
            ImGui.Text(label);
            ImGui.PopStyleColor();
            ImGui.SameLine(LabelColumnWidth);
            ImGui.PushStyleColor(ImGuiCol.Text, ColTextDisabled);
            ImGui.TextUnformatted(value);
            ImGui.PopStyleColor();
        }

        // ════════════════════════════════════════════════════════════════════
        //  ADD COMPONENT BUTTON
        // ════════════════════════════════════════════════════════════════════

        private void DrawAddComponentButton()
        {
            ImGui.PushID("AddComp");
            ImGui.PushStyleColor(ImGuiCol.Button, ColField);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColFieldHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.24f, 0.24f, 0.24f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, ColTextDim);

            if (ImGui.Button("Add Component", new Vector2(-1f, 26f)))
                ImGui.OpenPopup("AddCompPopup");

            ImGui.PopStyleColor(4);

            if (ImGui.BeginPopup("AddCompPopup"))
            {
                int idx = 0;
                foreach (var ct in ComponentRegistry.Components)
                {
                    ImGui.PushID(idx++);
                    if (ImGui.MenuItem(ct.Name))
                    {
                        EditorActions.SelectedObject!.AddComponent(ct).Start();
                        EngineEditor.LogMessage($"Added {ct.Name}");
                    }
                    ImGui.PopID();
                }
                ImGui.EndPopup();
            }
            ImGui.PopID();
        }
    }
}