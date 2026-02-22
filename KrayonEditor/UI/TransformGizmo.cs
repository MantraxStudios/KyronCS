using ImGuiNET;
using OpenTK.Mathematics;
using SysVec2 = System.Numerics.Vector2;
using SysVec3 = System.Numerics.Vector3;

namespace KrayonEditor.UI
{
    public enum GizmoMode
    {
        Translate,
        Rotate,
        Scale
    }

    public enum GizmoSpace
    {
        World,
        Local
    }

    /// <summary>
    /// Represents the transform data passed to and returned from the gizmo.
    /// Engine-agnostic: you fill this from your own objects and apply the result yourself.
    /// </summary>
    public struct GizmoTransform
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public GizmoTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        /// <summary>Decomposes a TRS matrix into this struct.</summary>
        public static GizmoTransform FromMatrix(Matrix4 trs)
        {
            var position = trs.ExtractTranslation();
            var rotation = trs.ExtractRotation();
            var scale = trs.ExtractScale();
            return new GizmoTransform(position, rotation, scale);
        }

        /// <summary>Reconstructs a TRS matrix from this struct.</summary>
        public readonly Matrix4 ToMatrix()
        {
            return Matrix4.CreateScale(Scale)
                 * Matrix4.CreateFromQuaternion(Rotation)
                 * Matrix4.CreateTranslation(Position);
        }
    }

    /// <summary>
    /// Immediate-mode 3-D transform gizmo rendered via ImGui's DrawList.
    /// 
    /// Usage per frame:
    /// <code>
    ///   if (TransformGizmo.Draw(ref myTransform, viewMatrix, projMatrix, vpPos, vpSize, mouseInVp))
    ///   {
    ///       myObject.ApplyTransform(myTransform); // your engine call
    ///   }
    /// </code>
    ///
    /// No dependency on any engine type beyond OpenTK math + ImGuiNET.
    /// </summary>
    internal static class TransformGizmo
    {
        // ── State ────────────────────────────────────────────────────────────
        private static GizmoMode _currentMode = GizmoMode.Translate;
        private static GizmoSpace _currentSpace = GizmoSpace.World;

        private static bool _isDragging = false;
        private static bool _isHovering = false;
        private static int _activeAxis = -1;
        private static int _hoveredAxis = -1;
        private static SysVec2 _lastMousePos = SysVec2.Zero;

        // Accumulated values during a drag so snapping works correctly.
        private static SysVec3 _accumulatedPos = SysVec3.Zero;
        private static Quaternion _accumulatedRot = Quaternion.Identity;
        private static SysVec3 _accumulatedScale = SysVec3.One;

        // ── Snap settings ────────────────────────────────────────────────────
        private static float _translateSnapValue = 0.5f;
        private static float _rotateSnapValue = 15.0f;
        private static float _scaleSnapValue = 0.1f;
        private static bool _snapEnabled = false;

        // ── Public properties ────────────────────────────────────────────────
        public static GizmoMode CurrentMode => _currentMode;
        public static GizmoSpace CurrentSpace => _currentSpace;
        public static bool IsDragging => _isDragging;
        public static bool IsHovering => _isHovering;

        public static bool SnapEnabled
        {
            get => _snapEnabled;
            set => _snapEnabled = value;
        }

        public static float TranslateSnapValue
        {
            get => _translateSnapValue;
            set => _translateSnapValue = Math.Max(0.01f, value);
        }

        public static float RotateSnapValue
        {
            get => _rotateSnapValue;
            set => _rotateSnapValue = Math.Max(1.0f, value);
        }

        public static float ScaleSnapValue
        {
            get => _scaleSnapValue;
            set => _scaleSnapValue = Math.Max(0.01f, value);
        }

        public static void SetMode(GizmoMode mode)
        {
            if (_currentMode == mode) return;
            _currentMode = mode;
            _isDragging = false;
            _activeAxis = -1;
            _isHovering = false;
            _hoveredAxis = -1;
        }

        public static void ToggleSpace()
            => _currentSpace = _currentSpace == GizmoSpace.World ? GizmoSpace.Local : GizmoSpace.World;

        // ─────────────────────────────────────────────────────────────────────
        // Main entry point
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draws the gizmo and mutates <paramref name="transform"/> if the user drags it.
        /// </summary>
        /// <param name="transform">Current transform; updated in-place when the user interacts.</param>
        /// <param name="viewMatrix">Camera view matrix (row-major, OpenTK convention).</param>
        /// <param name="projectionMatrix">Camera projection matrix.</param>
        /// <param name="viewportPos">Top-left corner of the viewport in screen space.</param>
        /// <param name="viewportSize">Width and height of the viewport in pixels.</param>
        /// <param name="isMouseOverViewport">Whether the OS cursor is inside the viewport.</param>
        /// <returns><c>true</c> if <paramref name="transform"/> was modified this frame.</returns>
        public static bool Draw(
            ref GizmoTransform transform,
            Matrix4 viewMatrix,
            Matrix4 projectionMatrix,
            SysVec2 viewportPos,
            SysVec2 viewportSize,
            bool isMouseOverViewport)
        {
            SysVec3 objectPos = ToSysVec3(transform.Position);

            if (!IsInFrontOfCamera(objectPos, viewMatrix))
            {
                _isDragging = false;
                _isHovering = false;
                _hoveredAxis = -1;
                return false;
            }

            ImGuiIOPtr io = ImGui.GetIO();
            SysVec2 mousePos = new SysVec2(io.MousePos.X - viewportPos.X, io.MousePos.Y - viewportPos.Y);
            bool mouseInViewport = isMouseOverViewport
                && mousePos.X >= 0 && mousePos.X <= viewportSize.X
                && mousePos.Y >= 0 && mousePos.Y <= viewportSize.Y;

            bool modified = HandleInput(ref transform, viewMatrix, projectionMatrix, mousePos, viewportSize, mouseInViewport);

            switch (_currentMode)
            {
                case GizmoMode.Translate:
                    DrawTranslateGizmo(transform, viewMatrix, projectionMatrix, viewportPos, viewportSize);
                    break;
                case GizmoMode.Rotate:
                    DrawRotateGizmo(transform, viewMatrix, projectionMatrix, viewportPos, viewportSize);
                    break;
                case GizmoMode.Scale:
                    DrawScaleGizmo(transform, viewMatrix, projectionMatrix, viewportPos, viewportSize);
                    break;
            }

            return modified;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Input handling
        // ─────────────────────────────────────────────────────────────────────

        private static bool HandleInput(
            ref GizmoTransform transform,
            Matrix4 view,
            Matrix4 proj,
            SysVec2 mousePos,
            SysVec2 viewportSize,
            bool mouseInViewport)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            bool mouseDown = io.MouseDown[0];
            bool modified = false;

            if (!mouseInViewport && !_isDragging)
            {
                _isHovering = false;
                _hoveredAxis = -1;
                return false;
            }

            SysVec3 objectPos = ToSysVec3(transform.Position);

            if (!_isDragging && mouseInViewport)
            {
                _hoveredAxis = GetHoveredAxis(transform, objectPos, view, proj, mousePos, viewportSize);
                _isHovering = _hoveredAxis >= 0;
            }

            if (mouseDown && !_isDragging && mouseInViewport)
            {
                int hit = GetHoveredAxis(transform, objectPos, view, proj, mousePos, viewportSize);
                if (hit >= 0)
                {
                    _isDragging = true;
                    _activeAxis = hit;
                    _lastMousePos = mousePos;
                    _accumulatedPos = ToSysVec3(transform.Position);
                    _accumulatedRot = transform.Rotation;
                    _accumulatedScale = ToSysVec3(transform.Scale);
                }
            }
            else if (_isDragging)
            {
                if (mouseDown)
                {
                    SysVec2 delta = mousePos - _lastMousePos;
                    modified = ApplyTransform(ref transform, delta, view, proj, viewportSize);
                    _lastMousePos = mousePos;
                }
                else
                {
                    _isDragging = false;
                    _activeAxis = -1;
                    _isHovering = false;
                    _hoveredAxis = -1;
                }
            }

            return modified;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Transform application
        // ─────────────────────────────────────────────────────────────────────

        private static bool ApplyTransform(
            ref GizmoTransform transform,
            SysVec2 delta,
            Matrix4 view,
            Matrix4 proj,
            SysVec2 viewportSize)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            bool snapEnabled = _snapEnabled ^ io.KeyCtrl;

            // Derive a camera position from the inverse view matrix.
            Matrix4 invView = Matrix4.Invert(view);
            SysVec3 cameraPos = ToSysVec3(invView.ExtractTranslation());

            switch (_currentMode)
            {
                // ── Translate ────────────────────────────────────────────────
                case GizmoMode.Translate:
                    {
                        float distance = SysVec3.Distance(_accumulatedPos, cameraPos);
                        float moveSpeed = distance * 0.002f;

                        if (_activeAxis == 6) // Free XZ plane (camera-facing)
                        {
                            SysVec3 toCamera = SysVec3.Normalize(cameraPos - _accumulatedPos);
                            SysVec3 right = SysVec3.Normalize(SysVec3.Cross(toCamera, SysVec3.UnitY));
                            if (right.Length() < 0.1f)
                                right = SysVec3.Normalize(SysVec3.Cross(toCamera, SysVec3.UnitX));
                            SysVec3 up = SysVec3.Cross(right, toCamera);

                            _accumulatedPos += right * delta.X * moveSpeed + up * -delta.Y * moveSpeed;
                        }
                        else
                        {
                            SysVec3 axisDir = GetAxisDirection(_activeAxis, transform.Rotation);
                            SysVec2 objScreen = WorldToScreen(_accumulatedPos, view, proj, viewportSize);
                            SysVec2 axisEnd = WorldToScreen(_accumulatedPos + axisDir, view, proj, viewportSize);
                            SysVec2 screenAxis = SysVec2.Normalize(axisEnd - objScreen);
                            float along = SysVec2.Dot(delta, screenAxis);

                            _accumulatedPos += axisDir * along * moveSpeed;
                        }

                        SysVec3 finalPos = _accumulatedPos;
                        if (snapEnabled)
                        {
                            if (_activeAxis == 6 || _activeAxis == 0)
                                finalPos.X = MathF.Round(finalPos.X / _translateSnapValue) * _translateSnapValue;
                            if (_activeAxis == 6 || _activeAxis == 1)
                                finalPos.Y = MathF.Round(finalPos.Y / _translateSnapValue) * _translateSnapValue;
                            if (_activeAxis == 6 || _activeAxis == 2)
                                finalPos.Z = MathF.Round(finalPos.Z / _translateSnapValue) * _translateSnapValue;
                        }

                        transform.Position = ToOpenTKVec3(finalPos);
                        return true;
                    }

                // ── Rotate ───────────────────────────────────────────────────
                case GizmoMode.Rotate:
                    {
                        SysVec3 objectPos = ToSysVec3(transform.Position);
                        SysVec3 axisDir = GetAxisDirection(_activeAxis, transform.Rotation);
                        SysVec2 objScreen = WorldToScreen(objectPos, view, proj, viewportSize);

                        SysVec3 toCamera = SysVec3.Normalize(cameraPos - objectPos);
                        SysVec3 tangent = SysVec3.Normalize(SysVec3.Cross(axisDir, toCamera));
                        SysVec2 tanEnd = WorldToScreen(objectPos + tangent * 0.5f, view, proj, viewportSize);
                        SysVec2 screenTan = SysVec2.Normalize(tanEnd - objScreen);

                        float rotDeg = SysVec2.Dot(delta, screenTan) * 0.5f;

                        Vector3 rotAxis = _activeAxis switch
                        {
                            0 => Vector3.UnitX,
                            1 => Vector3.UnitY,
                            2 => Vector3.UnitZ,
                            _ => Vector3.Zero
                        };

                        if (_currentSpace == GizmoSpace.Local)
                            rotAxis = _accumulatedRot * rotAxis;

                        Quaternion deltaRot = Quaternion.FromAxisAngle(rotAxis, rotDeg * MathF.PI / 180f);
                        _accumulatedRot = Quaternion.Normalize(deltaRot * _accumulatedRot);

                        if (snapEnabled)
                        {
                            Vector3 euler = _accumulatedRot.ToEulerAngles();
                            euler = new Vector3(
                                MathF.Round(euler.X * 180f / MathF.PI / _rotateSnapValue) * _rotateSnapValue * MathF.PI / 180f,
                                MathF.Round(euler.Y * 180f / MathF.PI / _rotateSnapValue) * _rotateSnapValue * MathF.PI / 180f,
                                MathF.Round(euler.Z * 180f / MathF.PI / _rotateSnapValue) * _rotateSnapValue * MathF.PI / 180f
                            );
                            transform.Rotation = Quaternion.FromEulerAngles(euler);
                        }
                        else
                        {
                            transform.Rotation = _accumulatedRot;
                        }

                        return true;
                    }

                // ── Scale ────────────────────────────────────────────────────
                case GizmoMode.Scale:
                    {
                        float scaleSpeed = 0.01f;
                        SysVec3 objectPos = ToSysVec3(transform.Position);

                        if (_activeAxis == 6) // Uniform scale
                        {
                            float d = (delta.X - delta.Y) * scaleSpeed;
                            _accumulatedScale = SysVec3.Max(
                                _accumulatedScale + new SysVec3(d, d, d),
                                new SysVec3(0.01f, 0.01f, 0.01f));
                        }
                        else
                        {
                            SysVec3 axisDir = GetAxisDirection(_activeAxis, transform.Rotation);
                            SysVec2 objScreen = WorldToScreen(objectPos, view, proj, viewportSize);
                            SysVec2 axisEnd = WorldToScreen(objectPos + axisDir, view, proj, viewportSize);
                            SysVec2 screenAxis = SysVec2.Normalize(axisEnd - objScreen);
                            float along = SysVec2.Dot(delta, screenAxis) * scaleSpeed;

                            if (_activeAxis == 0) _accumulatedScale.X = Math.Max(0.01f, _accumulatedScale.X + along);
                            if (_activeAxis == 1) _accumulatedScale.Y = Math.Max(0.01f, _accumulatedScale.Y + along);
                            if (_activeAxis == 2) _accumulatedScale.Z = Math.Max(0.01f, _accumulatedScale.Z + along);
                        }

                        SysVec3 finalScale = _accumulatedScale;
                        if (snapEnabled)
                        {
                            if (_activeAxis == 6)
                            {
                                float s = Math.Max(0.01f, MathF.Round(_accumulatedScale.X / _scaleSnapValue) * _scaleSnapValue);
                                finalScale = new SysVec3(s, s, s);
                            }
                            else
                            {
                                if (_activeAxis == 0) finalScale.X = Math.Max(0.01f, MathF.Round(_accumulatedScale.X / _scaleSnapValue) * _scaleSnapValue);
                                if (_activeAxis == 1) finalScale.Y = Math.Max(0.01f, MathF.Round(_accumulatedScale.Y / _scaleSnapValue) * _scaleSnapValue);
                                if (_activeAxis == 2) finalScale.Z = Math.Max(0.01f, MathF.Round(_accumulatedScale.Z / _scaleSnapValue) * _scaleSnapValue);
                            }
                        }

                        transform.Scale = ToOpenTKVec3(finalScale);
                        return true;
                    }
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Hit-testing
        // ─────────────────────────────────────────────────────────────────────

        private static int GetHoveredAxis(
            GizmoTransform transform,
            SysVec3 objectPos,
            Matrix4 view,
            Matrix4 proj,
            SysVec2 mousePos,
            SysVec2 viewportSize)
        {
            float gizmoSize = GetGizmoSize(objectPos, view);

            if (_currentMode == GizmoMode.Rotate)
            {
                float closestDist = float.MaxValue;
                int closestAxis = -1;
                float threshold = 50.0f;

                for (int axis = 0; axis < 3; axis++)
                {
                    SysVec3 axisDir = GetAxisDirection(axis, transform.Rotation);
                    SysVec3 t1 = Math.Abs(axisDir.Y) < 0.9f
                        ? SysVec3.Normalize(SysVec3.Cross(axisDir, SysVec3.UnitY))
                        : SysVec3.Normalize(SysVec3.Cross(axisDir, SysVec3.UnitX));
                    SysVec3 t2 = SysVec3.Cross(axisDir, t1);

                    float minDist = float.MaxValue;
                    int segs = 64;

                    for (int i = 0; i < segs; i++)
                    {
                        float a1 = (float)(i * 2 * Math.PI / segs);
                        float a2 = (float)((i + 1) * 2 * Math.PI / segs);

                        SysVec3 p1 = objectPos + (t1 * MathF.Cos(a1) + t2 * MathF.Sin(a1)) * gizmoSize;
                        SysVec3 p2 = objectPos + (t1 * MathF.Cos(a2) + t2 * MathF.Sin(a2)) * gizmoSize;

                        if (!IsInFrontOfCamera(p1, view) || !IsInFrontOfCamera(p2, view))
                            continue;

                        float d = DistanceToSegment(mousePos,
                            WorldToScreen(p1, view, proj, viewportSize),
                            WorldToScreen(p2, view, proj, viewportSize));

                        if (d < minDist) minDist = d;
                    }

                    if (minDist < closestDist) { closestDist = minDist; closestAxis = axis; }
                }

                if (closestDist < threshold) return closestAxis;
            }
            else
            {
                float threshold = 20.0f;
                float closestDist = float.MaxValue;
                int closestAxis = -1;

                for (int axis = 0; axis < 3; axis++)
                {
                    SysVec3 axisDir = GetAxisDirection(axis, transform.Rotation);
                    SysVec3 axisEnd = objectPos + axisDir * gizmoSize;

                    if (!IsInFrontOfCamera(axisEnd, view)) continue;

                    float d = DistanceToSegment(mousePos,
                        WorldToScreen(objectPos, view, proj, viewportSize),
                        WorldToScreen(axisEnd, view, proj, viewportSize));

                    if (d < threshold && d < closestDist) { closestDist = d; closestAxis = axis; }
                }

                if (closestAxis >= 0) return closestAxis;
            }

            // Center handle (free move / uniform scale)
            if (_currentMode == GizmoMode.Scale || _currentMode == GizmoMode.Translate)
            {
                float radius = _currentMode == GizmoMode.Scale ? 22.0f : 20.0f;
                if (SysVec2.Distance(mousePos, WorldToScreen(objectPos, view, proj, viewportSize)) < radius)
                    return 6;
            }

            return -1;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Draw methods
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawTranslateGizmo(
            GizmoTransform transform,
            Matrix4 view, Matrix4 proj,
            SysVec2 viewportPos, SysVec2 viewportSize)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            SysVec3 objectPos = ToSysVec3(transform.Position);
            float gizmoSize = GetGizmoSize(objectPos, view);
            string[] labels = { "X", "Y", "Z" };

            for (int i = 0; i < 3; i++)
            {
                SysVec3 dir = GetAxisDirection(i, transform.Rotation);
                SysVec3 axisEnd = objectPos + dir * gizmoSize;
                if (!IsInFrontOfCamera(axisEnd, view)) continue;

                SysVec2 startSc = WorldToScreen(objectPos, view, proj, viewportSize) + viewportPos;
                SysVec2 endSc = WorldToScreen(axisEnd, view, proj, viewportSize) + viewportPos;

                bool isActive = _isDragging && _activeAxis == i;
                bool isHovered = !_isDragging && _hoveredAxis == i;
                uint color = GetAxisColor(i, isActive, isHovered);
                float thick = (isActive || isHovered) ? 7f : 5f;

                drawList.AddLine(startSc + new SysVec2(2, 2), endSc + new SysVec2(2, 2), 0x40000000, thick);
                drawList.AddLine(startSc, endSc, color, thick);
                DrawArrowHead(drawList, startSc, endSc, color, thick);

                DrawAxisLabel(drawList, objectPos + dir * gizmoSize * 1.2f, labels[i], color, view, proj, viewportPos, viewportSize);
            }

            DrawCenterCircle(drawList, objectPos, view, proj, viewportPos, viewportSize);
        }

        private static void DrawRotateGizmo(
            GizmoTransform transform,
            Matrix4 view, Matrix4 proj,
            SysVec2 viewportPos, SysVec2 viewportSize)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            SysVec3 objectPos = ToSysVec3(transform.Position);
            float gizmoSize = GetGizmoSize(objectPos, view);
            string[] labels = { "X", "Y", "Z" };

            for (int i = 0; i < 3; i++)
            {
                bool isActive = _isDragging && _activeAxis == i;
                bool isHovered = !_isDragging && _hoveredAxis == i;
                uint color = GetAxisColor(i, isActive, isHovered);
                float thick = (isActive || isHovered) ? 6.5f : 4.5f;

                DrawRotationCircle(drawList, objectPos, i, gizmoSize, view, proj, viewportPos + new SysVec2(2, 2), viewportSize, 0x40000000, thick + 1, transform.Rotation);
                DrawRotationCircle(drawList, objectPos, i, gizmoSize, view, proj, viewportPos, viewportSize, color, thick, transform.Rotation);

                SysVec3 axisDir = GetAxisDirection(i, transform.Rotation);
                SysVec3 labelPos = objectPos + axisDir * gizmoSize * 1.2f;
                if (IsInFrontOfCamera(labelPos, view))
                    DrawAxisLabel(drawList, labelPos, labels[i], color, view, proj, viewportPos, viewportSize);
            }

            SysVec2 centerSc = WorldToScreen(objectPos, view, proj, viewportSize) + viewportPos;
            drawList.AddCircleFilled(centerSc, 9f, 0x60FFFFFF);
            drawList.AddCircleFilled(centerSc, 7f, 0xFFFFFFFF);
            drawList.AddCircle(centerSc, 7f, 0xFF000000, 0, 2f);
        }

        private static void DrawScaleGizmo(
            GizmoTransform transform,
            Matrix4 view, Matrix4 proj,
            SysVec2 viewportPos, SysVec2 viewportSize)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            SysVec3 objectPos = ToSysVec3(transform.Position);
            float gizmoSize = GetGizmoSize(objectPos, view);
            string[] labels = { "X", "Y", "Z" };

            for (int i = 0; i < 3; i++)
            {
                SysVec3 dir = GetAxisDirection(i, transform.Rotation);
                SysVec3 axisEnd = objectPos + dir * gizmoSize;
                if (!IsInFrontOfCamera(axisEnd, view)) continue;

                SysVec2 startSc = WorldToScreen(objectPos, view, proj, viewportSize) + viewportPos;
                SysVec2 endSc = WorldToScreen(axisEnd, view, proj, viewportSize) + viewportPos;

                bool isActive = _isDragging && _activeAxis == i;
                bool isHovered = !_isDragging && _hoveredAxis == i;
                uint color = GetAxisColor(i, isActive, isHovered);
                float thick = (isActive || isHovered) ? 7f : 5f;
                float box = (isActive || isHovered) ? 10f : 8f;

                drawList.AddLine(startSc + new SysVec2(2, 2), endSc + new SysVec2(2, 2), 0x40000000, thick);
                drawList.AddLine(startSc, endSc, color, thick);

                drawList.AddRectFilled(endSc - new SysVec2(box - 2, box - 2), endSc + new SysVec2(box + 2, box + 2), 0x40000000, 2f);
                drawList.AddRectFilled(endSc - new SysVec2(box, box), endSc + new SysVec2(box, box), color, 2f);
                drawList.AddRect(endSc - new SysVec2(box, box), endSc + new SysVec2(box, box), 0xFF000000, 2f, 0, 2f);

                DrawAxisLabel(drawList, objectPos + dir * gizmoSize * 1.25f, labels[i], color, view, proj, viewportPos, viewportSize);
            }

            SysVec2 centerSc = WorldToScreen(objectPos, view, proj, viewportSize) + viewportPos;
            bool cActive = _isDragging && _activeAxis == 6;
            bool cHovered = !_isDragging && _hoveredAxis == 6;
            uint cColor = (cActive || cHovered) ? 0xFFFFDD00 : 0xFFFFFFFF;
            float cSize = (cActive || cHovered) ? 11f : 9f;

            drawList.AddRectFilled(centerSc - new SysVec2(cSize - 2, cSize - 2), centerSc + new SysVec2(cSize + 2, cSize + 2), 0x60000000, 2f);
            drawList.AddRectFilled(centerSc - new SysVec2(cSize, cSize), centerSc + new SysVec2(cSize, cSize), cColor, 2f);
            drawList.AddRect(centerSc - new SysVec2(cSize, cSize), centerSc + new SysVec2(cSize, cSize), 0xFF000000, 2f, 0, 2.5f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Shared draw helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawCenterCircle(
            ImDrawListPtr drawList, SysVec3 objectPos,
            Matrix4 view, Matrix4 proj,
            SysVec2 viewportPos, SysVec2 viewportSize)
        {
            SysVec2 centerSc = WorldToScreen(objectPos, view, proj, viewportSize) + viewportPos;
            bool isActive = _isDragging && _activeAxis == 6;
            bool isHovered = !_isDragging && _hoveredAxis == 6;
            uint color = (isActive || isHovered) ? 0xFFFFDD00 : 0xFFFFFFFF;
            float r = (isActive || isHovered) ? 8f : 7f;

            drawList.AddCircleFilled(centerSc, r + 2f, 0x60FFFFFF);
            drawList.AddCircleFilled(centerSc, r, color);
            drawList.AddCircle(centerSc, r, 0xFF000000, 0, 2f);
        }

        private static void DrawAxisLabel(
            ImDrawListPtr drawList, SysVec3 worldPos, string label, uint color,
            Matrix4 view, Matrix4 proj, SysVec2 viewportPos, SysVec2 viewportSize)
        {
            SysVec2 sc = WorldToScreen(worldPos, view, proj, viewportSize) + viewportPos;
            SysVec2 textSize = ImGui.CalcTextSize(label);
            SysVec2 bgMin = sc - new SysVec2(textSize.X * 0.5f + 3, textSize.Y * 0.5f + 2);
            SysVec2 bgMax = sc + new SysVec2(textSize.X * 0.5f + 3, textSize.Y * 0.5f + 2);

            drawList.AddRectFilled(bgMin, bgMax, 0xBB000000, 3f);
            drawList.AddRect(bgMin, bgMax, color, 3f, 0, 1.5f);
            drawList.AddText(new SysVec2(sc.X - textSize.X * 0.5f, sc.Y - textSize.Y * 0.5f), color, label);
        }

        private static void DrawRotationCircle(
            ImDrawListPtr drawList, SysVec3 center, int axis, float radius,
            Matrix4 view, Matrix4 proj, SysVec2 viewportPos, SysVec2 viewportSize,
            uint color, float thickness, Quaternion rotation)
        {
            SysVec3 axisDir = GetAxisDirection(axis, rotation);
            SysVec3 t1 = Math.Abs(axisDir.Y) < 0.9f
                ? SysVec3.Normalize(SysVec3.Cross(axisDir, SysVec3.UnitY))
                : SysVec3.Normalize(SysVec3.Cross(axisDir, SysVec3.UnitX));
            SysVec3 t2 = SysVec3.Cross(axisDir, t1);

            int segs = 96;
            for (int i = 0; i < segs; i++)
            {
                float a1 = (float)(i * 2 * Math.PI / segs);
                float a2 = (float)((i + 1) * 2 * Math.PI / segs);

                SysVec3 p1 = center + (t1 * MathF.Cos(a1) + t2 * MathF.Sin(a1)) * radius;
                SysVec3 p2 = center + (t1 * MathF.Cos(a2) + t2 * MathF.Sin(a2)) * radius;

                if (!IsInFrontOfCamera(p1, view) || !IsInFrontOfCamera(p2, view))
                    continue;

                drawList.AddLine(
                    WorldToScreen(p1, view, proj, viewportSize) + viewportPos,
                    WorldToScreen(p2, view, proj, viewportSize) + viewportPos,
                    color, thickness);
            }
        }

        private static void DrawArrowHead(ImDrawListPtr drawList, SysVec2 start, SysVec2 end, uint color, float lineThickness)
        {
            SysVec2 dir = SysVec2.Normalize(end - start);
            SysVec2 perp = new SysVec2(-dir.Y, dir.X);
            float size = 10f + lineThickness * 0.5f;

            SysVec2 p1 = end - dir * size + perp * size * 0.4f;
            SysVec2 p2 = end - dir * size - perp * size * 0.4f;

            drawList.AddTriangleFilled(end + new SysVec2(1.5f, 1.5f), p1 + new SysVec2(1.5f, 1.5f), p2 + new SysVec2(1.5f, 1.5f), 0x60000000);
            drawList.AddTriangleFilled(end, p1, p2, color);
            drawList.AddTriangle(end, p1, p2, 0xFF000000, 1.5f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pure math helpers  (no engine types)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns the world-space axis direction, respecting Local/World space.</summary>
        private static SysVec3 GetAxisDirection(int axis, Quaternion rotation)
        {
            SysVec3 baseAxis = axis switch
            {
                0 => SysVec3.UnitX,
                1 => SysVec3.UnitY,
                2 => SysVec3.UnitZ,
                _ => SysVec3.Zero
            };

            if (_currentSpace == GizmoSpace.World)
                return baseAxis;

            Vector3 rotated = rotation * new Vector3(baseAxis.X, baseAxis.Y, baseAxis.Z);
            return new SysVec3(rotated.X, rotated.Y, rotated.Z);
        }

        /// <summary>Gizmo world-space size that stays constant in screen space.</summary>
        private static float GetGizmoSize(SysVec3 position, Matrix4 view)
        {
            // Recover camera position from the inverse of the view matrix.
            Matrix4 invView = Matrix4.Invert(view);
            SysVec3 cameraPos = ToSysVec3(invView.ExtractTranslation());
            float distance = SysVec3.Distance(position, cameraPos);
            return Math.Max(0.5f, distance * 0.15f);
        }

        private static bool IsInFrontOfCamera(SysVec3 worldPos, Matrix4 view)
        {
            Vector4 viewSpace = new Vector4(worldPos.X, worldPos.Y, worldPos.Z, 1f) * view;
            return viewSpace.Z < 0f;
        }

        private static SysVec2 WorldToScreen(SysVec3 worldPos, Matrix4 view, Matrix4 proj, SysVec2 viewportSize)
        {
            Vector4 clip = new Vector4(worldPos.X, worldPos.Y, worldPos.Z, 1f) * view * proj;

            if (MathF.Abs(clip.W) < 0.0001f) clip.W = 0.0001f;

            Vector3 ndc = new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);

            return new SysVec2(
                (ndc.X + 1f) * 0.5f * viewportSize.X,
                (1f - ndc.Y) * 0.5f * viewportSize.Y);
        }

        private static float DistanceToSegment(SysVec2 point, SysVec2 a, SysVec2 b)
        {
            SysVec2 ab = b - a;
            float len = ab.Length();
            if (len < 0.001f) return SysVec2.Distance(point, a);

            float t = Math.Clamp(SysVec2.Dot(point - a, ab / len), 0f, len);
            return SysVec2.Distance(point, a + ab / len * t);
        }

        private static uint GetAxisColor(int axis, bool isActive, bool isHovered = false)
        {
            if (isActive)
                return axis switch { 0 => 0xFFFF6666u, 1 => 0xFF66FF66u, 2 => 0xFF6666FFu, _ => 0xFFFFFFFFu };
            if (isHovered)
                return axis switch { 0 => 0xFFFF8888u, 1 => 0xFF88FF88u, 2 => 0xFF8888FFu, _ => 0xFFFFFFFFu };
            return axis switch { 0 => 0xFFDD4444u, 1 => 0xFF44DD44u, 2 => 0xFF4444DDu, _ => 0xFFFFFFFFu };
        }

        private static SysVec3 ToSysVec3(Vector3 v) => new SysVec3(v.X, v.Y, v.Z);
        private static Vector3 ToOpenTKVec3(SysVec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}