using ImGuiNET;
using KrayonCore;
using SysVec2 = System.Numerics.Vector2;
using SysVec3 = System.Numerics.Vector3;
using OpenTK.Mathematics;

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

    internal static class TransformGizmo
    {
        private static GizmoMode _currentMode = GizmoMode.Translate;
        private static GizmoSpace _currentSpace = GizmoSpace.World;

        private static bool _isDragging = false;
        private static bool _isHovering = false;
        private static SysVec2 _dragStartPos = SysVec2.Zero;
        private static SysVec3 _objectStartPos = SysVec3.Zero;
        private static Quaternion _objectStartRot = Quaternion.Identity;
        private static SysVec3 _objectStartScale = SysVec3.One;
        private static int _activeAxis = -1;
        private static int _hoveredAxis = -1;
        private static SysVec2 _lastMousePos = SysVec2.Zero;

        private static SysVec3 _accumulatedPos = SysVec3.Zero;
        private static Quaternion _accumulatedRot = Quaternion.Identity;
        private static SysVec3 _accumulatedScale = SysVec3.One;

        private static float _translateSnapValue = 0.5f;
        private static float _rotateSnapValue = 15.0f;
        private static float _scaleSnapValue = 0.1f;
        private static bool _snapEnabled = false;

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
            if (_currentMode != mode)
            {
                _currentMode = mode;
                _isDragging = false;
                _activeAxis = -1;
                _isHovering = false;
                _hoveredAxis = -1;
            }
        }

        public static void ToggleSpace()
        {
            _currentSpace = _currentSpace == GizmoSpace.World ? GizmoSpace.Local : GizmoSpace.World;
        }

        public static void Draw(
            GameObject? selectedObject,
            Camera? camera,
            SysVec2 viewportPos,
            SysVec2 viewportSize,
            bool isMouseOverViewport)
        {
            if (selectedObject == null || camera == null)
            {
                _isDragging = false;
                _isHovering = false;
                _hoveredAxis = -1;
                return;
            }

            SysVec3 objectPos = ToSysVec3(selectedObject.Transform.LocalPosition);

            if (!IsObjectInFrontOfCamera(objectPos, camera))
            {
                _isDragging = false;
                _isHovering = false;
                _hoveredAxis = -1;
                return;
            }

            ImGuiIOPtr io = ImGui.GetIO();
            SysVec2 mousePos = new SysVec2(io.MousePos.X - viewportPos.X, io.MousePos.Y - viewportPos.Y);
            bool mouseInViewport = isMouseOverViewport &&
                mousePos.X >= 0 && mousePos.X <= viewportSize.X &&
                mousePos.Y >= 0 && mousePos.Y <= viewportSize.Y;

            HandleInput(selectedObject, camera, mousePos, viewportSize, mouseInViewport);

            switch (_currentMode)
            {
                case GizmoMode.Translate:
                    DrawTranslateGizmo(selectedObject, objectPos, camera, viewportPos, viewportSize);
                    break;
                case GizmoMode.Rotate:
                    DrawRotateGizmo(selectedObject, objectPos, camera, viewportPos, viewportSize);
                    break;
                case GizmoMode.Scale:
                    DrawScaleGizmo(selectedObject, objectPos, camera, viewportPos, viewportSize);
                    break;
            }
        }

        private static bool IsObjectInFrontOfCamera(SysVec3 objectPos, Camera camera)
        {
            Matrix4 view = camera.GetViewMatrix();
            Vector4 worldPos4 = new Vector4(objectPos.X, objectPos.Y, objectPos.Z, 1.0f);
            Vector4 viewSpace = worldPos4 * view;
            return viewSpace.Z < 0;
        }

        private static void HandleInput(
            GameObject selectedObject,
            Camera camera,
            SysVec2 mousePos,
            SysVec2 viewportSize,
            bool mouseInViewport)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            bool mouseDown = io.MouseDown[0];

            if (!mouseInViewport && !_isDragging)
            {
                _isHovering = false;
                _hoveredAxis = -1;
                return;
            }

            SysVec3 objectPos = ToSysVec3(selectedObject.Transform.LocalPosition);

            if (!_isDragging && mouseInViewport)
            {
                _hoveredAxis = GetHoveredAxis(selectedObject, objectPos, camera, mousePos, viewportSize);
                _isHovering = _hoveredAxis >= 0;
            }

            if (mouseDown && !_isDragging && mouseInViewport)
            {
                _activeAxis = GetHoveredAxis(selectedObject, objectPos, camera, mousePos, viewportSize);

                if (_activeAxis >= 0)
                {
                    _isDragging = true;
                    _dragStartPos = mousePos;
                    _objectStartPos = ToSysVec3(selectedObject.Transform.LocalPosition);
                    _objectStartRot = selectedObject.Transform.Rotation;
                    _objectStartScale = ToSysVec3(selectedObject.Transform.LocalScale);
                    _lastMousePos = mousePos;

                    _accumulatedPos = _objectStartPos;
                    _accumulatedRot = _objectStartRot;
                    _accumulatedScale = _objectStartScale;
                }
            }
            else if (_isDragging)
            {
                if (mouseDown)
                {
                    SysVec2 currentDelta = mousePos - _lastMousePos;
                    ApplyTransform(selectedObject, currentDelta, mousePos, camera, viewportSize);
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
        }

        private static void ApplyTransform(GameObject obj, SysVec2 delta, SysVec2 mousePos, Camera camera, SysVec2 viewportSize)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            bool snapEnabled = _snapEnabled ^ io.KeyCtrl;

            switch (_currentMode)
            {
                case GizmoMode.Translate:
                    {
                        SysVec3 cameraPos = ToSysVec3(camera.Position);
                        float distance = SysVec3.Distance(_accumulatedPos, cameraPos);
                        float moveSpeed = distance * 0.002f;

                        if (_activeAxis == 6)
                        {
                            SysVec3 toCamera = SysVec3.Normalize(cameraPos - _accumulatedPos);

                            SysVec3 right = SysVec3.Normalize(SysVec3.Cross(toCamera, SysVec3.UnitY));
                            if (right.Length() < 0.1f)
                                right = SysVec3.Normalize(SysVec3.Cross(toCamera, SysVec3.UnitX));
                            SysVec3 up = SysVec3.Cross(right, toCamera);

                            SysVec3 movement = right * delta.X * moveSpeed + up * -delta.Y * moveSpeed;

                            _accumulatedPos += movement;

                            SysVec3 finalPos = _accumulatedPos;

                            if (snapEnabled)
                            {
                                finalPos.X = MathF.Round(_accumulatedPos.X / _translateSnapValue) * _translateSnapValue;
                                finalPos.Y = MathF.Round(_accumulatedPos.Y / _translateSnapValue) * _translateSnapValue;
                                finalPos.Z = MathF.Round(_accumulatedPos.Z / _translateSnapValue) * _translateSnapValue;
                            }

                            obj.Transform.SetPosition(finalPos.X, finalPos.Y, finalPos.Z);
                        }
                        else
                        {
                            SysVec3 axisDir = GetAxisDirection(_activeAxis, obj);

                            SysVec2 objScreen = WorldToScreen(_accumulatedPos, camera, viewportSize);
                            SysVec2 axisEndScreen = WorldToScreen(_accumulatedPos + axisDir, camera, viewportSize);
                            SysVec2 screenAxisDir = SysVec2.Normalize(axisEndScreen - objScreen);

                            float movementAlongAxis = SysVec2.Dot(delta, screenAxisDir);

                            SysVec3 movement = axisDir * movementAlongAxis * moveSpeed;

                            _accumulatedPos += movement;

                            SysVec3 finalPos = _accumulatedPos;

                            if (snapEnabled)
                            {
                                if (_activeAxis == 0)
                                    finalPos.X = MathF.Round(_accumulatedPos.X / _translateSnapValue) * _translateSnapValue;
                                else if (_activeAxis == 1)
                                    finalPos.Y = MathF.Round(_accumulatedPos.Y / _translateSnapValue) * _translateSnapValue;
                                else if (_activeAxis == 2)
                                    finalPos.Z = MathF.Round(_accumulatedPos.Z / _translateSnapValue) * _translateSnapValue;
                            }

                            obj.Transform.SetPosition(finalPos.X, finalPos.Y, finalPos.Z);
                        }
                    }
                    break;

                case GizmoMode.Rotate:
                    {
                        SysVec3 objectPos = ToSysVec3(obj.Transform.LocalPosition);
                        SysVec3 axisDir = GetAxisDirection(_activeAxis, obj);

                        SysVec2 objScreen = WorldToScreen(objectPos, camera, viewportSize);

                        SysVec3 cameraPos = ToSysVec3(camera.Position);
                        SysVec3 toCamera = SysVec3.Normalize(cameraPos - objectPos);

                        SysVec3 tangent = SysVec3.Normalize(SysVec3.Cross(axisDir, toCamera));
                        SysVec3 tangentEnd = objectPos + tangent * 0.5f;
                        SysVec2 tangentScreen = WorldToScreen(tangentEnd, camera, viewportSize);
                        SysVec2 screenTangent = SysVec2.Normalize(tangentScreen - objScreen);

                        float rotationAmount = SysVec2.Dot(delta, screenTangent);
                        float rotationSpeed = 0.5f;
                        float rotationDegrees = rotationAmount * rotationSpeed;

                        Vector3 rotAxis = new Vector3(
                            _activeAxis == 0 ? 1 : 0,
                            _activeAxis == 1 ? 1 : 0,
                            _activeAxis == 2 ? 1 : 0
                        );

                        if (_currentSpace == GizmoSpace.Local)
                        {
                            rotAxis = _accumulatedRot * rotAxis;
                        }

                        Quaternion deltaRot = Quaternion.FromAxisAngle(rotAxis, rotationDegrees * MathF.PI / 180.0f);
                        _accumulatedRot = Quaternion.Normalize(deltaRot * _accumulatedRot);

                        if (snapEnabled)
                        {
                            var euler = _accumulatedRot.ToEulerAngles();
                            SysVec3 eulerDeg = new SysVec3(
                                euler.X * 180f / MathF.PI,
                                euler.Y * 180f / MathF.PI,
                                euler.Z * 180f / MathF.PI
                            );

                            eulerDeg.X = MathF.Round(eulerDeg.X / _rotateSnapValue) * _rotateSnapValue;
                            eulerDeg.Y = MathF.Round(eulerDeg.Y / _rotateSnapValue) * _rotateSnapValue;
                            eulerDeg.Z = MathF.Round(eulerDeg.Z / _rotateSnapValue) * _rotateSnapValue;

                            Vector3 eulerRad = new Vector3(
                                eulerDeg.X * MathF.PI / 180f,
                                eulerDeg.Y * MathF.PI / 180f,
                                eulerDeg.Z * MathF.PI / 180f
                            );

                            obj.Transform.SetRotation(Quaternion.FromEulerAngles(eulerRad));
                        }
                        else
                        {
                            obj.Transform.SetRotation(_accumulatedRot);
                        }
                    }
                    break;

                case GizmoMode.Scale:
                    {
                        SysVec3 objectPos = ToSysVec3(obj.Transform.LocalPosition);

                        float scaleSpeed = 0.01f;
                        float scaleDelta = 0f;

                        if (_activeAxis == 6)
                        {
                            scaleDelta = (delta.X - delta.Y) * scaleSpeed;

                            _accumulatedScale += new SysVec3(scaleDelta, scaleDelta, scaleDelta);
                            _accumulatedScale = new SysVec3(
                                Math.Max(0.01f, _accumulatedScale.X),
                                Math.Max(0.01f, _accumulatedScale.Y),
                                Math.Max(0.01f, _accumulatedScale.Z)
                            );

                            SysVec3 finalScale = _accumulatedScale;

                            if (snapEnabled)
                            {
                                float snappedScale = MathF.Round(_accumulatedScale.X / _scaleSnapValue) * _scaleSnapValue;
                                snappedScale = Math.Max(0.01f, snappedScale);
                                finalScale = new SysVec3(snappedScale, snappedScale, snappedScale);
                            }

                            obj.Transform.SetScale(finalScale.X, finalScale.Y, finalScale.Z);
                        }
                        else
                        {
                            SysVec3 axisDir = GetAxisDirection(_activeAxis, obj);

                            SysVec2 objScreen = WorldToScreen(objectPos, camera, viewportSize);
                            SysVec2 axisEndScreen = WorldToScreen(objectPos + axisDir, camera, viewportSize);
                            SysVec2 screenAxisDir = SysVec2.Normalize(axisEndScreen - objScreen);

                            float movementAlongAxis = SysVec2.Dot(delta, screenAxisDir);
                            scaleDelta = movementAlongAxis * scaleSpeed;

                            if (_activeAxis == 0)
                                _accumulatedScale.X = Math.Max(0.01f, _accumulatedScale.X + scaleDelta);
                            else if (_activeAxis == 1)
                                _accumulatedScale.Y = Math.Max(0.01f, _accumulatedScale.Y + scaleDelta);
                            else if (_activeAxis == 2)
                                _accumulatedScale.Z = Math.Max(0.01f, _accumulatedScale.Z + scaleDelta);

                            SysVec3 finalScale = _accumulatedScale;

                            if (snapEnabled)
                            {
                                if (_activeAxis == 0)
                                    finalScale.X = Math.Max(0.01f, MathF.Round(_accumulatedScale.X / _scaleSnapValue) * _scaleSnapValue);
                                else if (_activeAxis == 1)
                                    finalScale.Y = Math.Max(0.01f, MathF.Round(_accumulatedScale.Y / _scaleSnapValue) * _scaleSnapValue);
                                else if (_activeAxis == 2)
                                    finalScale.Z = Math.Max(0.01f, MathF.Round(_accumulatedScale.Z / _scaleSnapValue) * _scaleSnapValue);
                            }

                            obj.Transform.SetScale(finalScale.X, finalScale.Y, finalScale.Z);
                        }
                    }
                    break;
            }
        }

        private static int GetHoveredAxis(GameObject obj, SysVec3 objectPos, Camera camera, SysVec2 mousePos, SysVec2 viewportSize)
        {
            float gizmoSize = GetGizmoSize(objectPos, camera);

            if (_currentMode == GizmoMode.Rotate)
            {
                float closestDistance = float.MaxValue;
                int closestAxis = -1;
                float clickThreshold = 50.0f;

                for (int axis = 0; axis < 3; axis++)
                {
                    SysVec3 axisDir = GetAxisDirection(axis, obj);

                    SysVec3 tangent1, tangent2;
                    if (Math.Abs(axisDir.Y) < 0.9f)
                    {
                        tangent1 = SysVec3.Normalize(SysVec3.Cross(axisDir, SysVec3.UnitY));
                    }
                    else
                    {
                        tangent1 = SysVec3.Normalize(SysVec3.Cross(axisDir, SysVec3.UnitX));
                    }
                    tangent2 = SysVec3.Cross(axisDir, tangent1);

                    int segments = 64;
                    float minDistForAxis = float.MaxValue;

                    for (int i = 0; i < segments; i++)
                    {
                        float angle1 = (float)(i * 2 * Math.PI / segments);
                        float angle2 = (float)((i + 1) * 2 * Math.PI / segments);

                        SysVec3 point1 = objectPos + (tangent1 * MathF.Cos(angle1) + tangent2 * MathF.Sin(angle1)) * gizmoSize;
                        SysVec3 point2 = objectPos + (tangent1 * MathF.Cos(angle2) + tangent2 * MathF.Sin(angle2)) * gizmoSize;

                        if (!IsPointInFrontOfCamera(point1, camera) || !IsPointInFrontOfCamera(point2, camera))
                            continue;

                        SysVec2 screen1 = WorldToScreen(point1, camera, viewportSize);
                        SysVec2 screen2 = WorldToScreen(point2, camera, viewportSize);

                        float dist = DistanceToLineSegment(mousePos, screen1, screen2);

                        if (dist < minDistForAxis)
                        {
                            minDistForAxis = dist;
                        }
                    }

                    if (minDistForAxis < closestDistance)
                    {
                        closestDistance = minDistForAxis;
                        closestAxis = axis;
                    }
                }

                if (closestDistance < clickThreshold)
                    return closestAxis;
            }
            else
            {
                float threshold = 20.0f;
                float closestDist = float.MaxValue;
                int closestAxis = -1;

                for (int axis = 0; axis < 3; axis++)
                {
                    SysVec3 axisDir = GetAxisDirection(axis, obj);
                    SysVec3 axisEnd = objectPos + axisDir * gizmoSize;

                    if (!IsPointInFrontOfCamera(axisEnd, camera))
                        continue;

                    SysVec2 startScreen = WorldToScreen(objectPos, camera, viewportSize);
                    SysVec2 endScreen = WorldToScreen(axisEnd, camera, viewportSize);

                    float dist = DistanceToLineSegment(mousePos, startScreen, endScreen);

                    if (dist < threshold && dist < closestDist)
                    {
                        closestDist = dist;
                        closestAxis = axis;
                    }
                }

                if (closestAxis >= 0)
                    return closestAxis;
            }

            if (_currentMode == GizmoMode.Scale || _currentMode == GizmoMode.Translate)
            {
                SysVec2 centerScreen = WorldToScreen(objectPos, camera, viewportSize);
                float centerRadius = _currentMode == GizmoMode.Scale ? 22.0f : 20.0f;
                if (SysVec2.Distance(mousePos, centerScreen) < centerRadius)
                    return 6;
            }

            return -1;
        }

        private static bool IsPointInFrontOfCamera(SysVec3 point, Camera camera)
        {
            Matrix4 view = camera.GetViewMatrix();
            Vector4 worldPos4 = new Vector4(point.X, point.Y, point.Z, 1.0f);
            Vector4 viewSpace = worldPos4 * view;
            return viewSpace.Z < 0;
        }

        private static void DrawTranslateGizmo(GameObject obj, SysVec3 position, Camera camera, SysVec2 viewportPos, SysVec2 viewportSize)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float gizmoSize = GetGizmoSize(position, camera);

            string[] axisLabels = { "X", "Y", "Z" };

            for (int i = 0; i < 3; i++)
            {
                SysVec3 axisDir = GetAxisDirection(i, obj);
                SysVec3 axisEnd = position + axisDir * gizmoSize;

                if (!IsPointInFrontOfCamera(axisEnd, camera))
                    continue;

                SysVec2 startScreen = WorldToScreen(position, camera, viewportSize);
                SysVec2 endScreen = WorldToScreen(axisEnd, camera, viewportSize);

                startScreen += viewportPos;
                endScreen += viewportPos;

                bool isActive = _isDragging && _activeAxis == i;
                bool isHovered = !_isDragging && _hoveredAxis == i;
                uint color = GetAxisColor(i, isActive, isHovered);
                uint shadowColor = 0x40000000;
                float thickness = (isActive || isHovered) ? 7.0f : 5.0f;

                drawList.AddLine(startScreen + new SysVec2(2, 2), endScreen + new SysVec2(2, 2), shadowColor, thickness);
                drawList.AddLine(startScreen, endScreen, color, thickness);

                if (camera.ProjectionMode != ProjectionMode.Orthographic)
                {
                    DrawArrowHead(drawList, startScreen, endScreen, color, thickness);
                }

                SysVec3 labelPos = position + axisDir * gizmoSize * 1.2f;
                SysVec2 labelScreen = WorldToScreen(labelPos, camera, viewportSize) + viewportPos;

                SysVec2 textSize = ImGui.CalcTextSize(axisLabels[i]);
                SysVec2 bgMin = labelScreen - new SysVec2(textSize.X * 0.5f + 3, textSize.Y * 0.5f + 2);
                SysVec2 bgMax = labelScreen + new SysVec2(textSize.X * 0.5f + 3, textSize.Y * 0.5f + 2);

                drawList.AddRectFilled(bgMin, bgMax, 0xBB000000, 3.0f);
                drawList.AddRect(bgMin, bgMax, color, 3.0f, 0, 1.5f);
                drawList.AddText(new SysVec2(labelScreen.X - textSize.X * 0.5f, labelScreen.Y - textSize.Y * 0.5f), color, axisLabels[i]);
            }

            SysVec2 centerScreen = WorldToScreen(position, camera, viewportSize) + viewportPos;
            bool isCenterActive = _isDragging && _activeAxis == 6;
            bool isCenterHovered = !_isDragging && _hoveredAxis == 6;
            uint centerColor = (isCenterActive || isCenterHovered) ? 0xFFFFDD00 : 0xFFFFFFFF;
            float centerRadius = (isCenterActive || isCenterHovered) ? 8.0f : 7.0f;

            drawList.AddCircleFilled(centerScreen, centerRadius + 2.0f, 0x60FFFFFF);
            drawList.AddCircleFilled(centerScreen, centerRadius, centerColor);
            drawList.AddCircle(centerScreen, centerRadius, 0xFF000000, 0, 2.0f);
        }

        private static void DrawRotateGizmo(GameObject obj, SysVec3 position, Camera camera, SysVec2 viewportPos, SysVec2 viewportSize)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float gizmoSize = GetGizmoSize(position, camera);
            SysVec2 centerScreen = WorldToScreen(position, camera, viewportSize) + viewportPos;

            string[] axisLabels = { "X", "Y", "Z" };

            for (int i = 0; i < 3; i++)
            {
                bool isActive = _isDragging && _activeAxis == i;
                bool isHovered = !_isDragging && _hoveredAxis == i;
                uint color = GetAxisColor(i, isActive, isHovered);
                uint shadowColor = 0x40000000;
                float thickness = (isActive || isHovered) ? 6.5f : 4.5f;

                DrawRotationCircle(drawList, position, i, gizmoSize, camera, viewportPos + new SysVec2(2, 2), viewportSize, shadowColor, thickness + 1, obj);
                DrawRotationCircle(drawList, position, i, gizmoSize, camera, viewportPos, viewportSize, color, thickness, obj);

                SysVec3 axisDir = GetAxisDirection(i, obj);
                SysVec3 labelPos = position + axisDir * gizmoSize * 1.2f;

                if (IsPointInFrontOfCamera(labelPos, camera))
                {
                    SysVec2 labelScreen = WorldToScreen(labelPos, camera, viewportSize) + viewportPos;

                    SysVec2 textSize = ImGui.CalcTextSize(axisLabels[i]);
                    SysVec2 bgMin = labelScreen - new SysVec2(textSize.X * 0.5f + 3, textSize.Y * 0.5f + 2);
                    SysVec2 bgMax = labelScreen + new SysVec2(textSize.X * 0.5f + 3, textSize.Y * 0.5f + 2);

                    drawList.AddRectFilled(bgMin, bgMax, 0xBB000000, 3.0f);
                    drawList.AddRect(bgMin, bgMax, color, 3.0f, 0, 1.5f);
                    drawList.AddText(new SysVec2(labelScreen.X - textSize.X * 0.5f, labelScreen.Y - textSize.Y * 0.5f), color, axisLabels[i]);
                }
            }

            drawList.AddCircleFilled(centerScreen, 9.0f, 0x60FFFFFF);
            drawList.AddCircleFilled(centerScreen, 7.0f, 0xFFFFFFFF);
            drawList.AddCircle(centerScreen, 7.0f, 0xFF000000, 0, 2.0f);
        }

        private static void DrawScaleGizmo(GameObject obj, SysVec3 position, Camera camera, SysVec2 viewportPos, SysVec2 viewportSize)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float gizmoSize = GetGizmoSize(position, camera);

            string[] axisLabels = { "X", "Y", "Z" };

            for (int i = 0; i < 3; i++)
            {
                SysVec3 axisDir = GetAxisDirection(i, obj);
                SysVec3 axisEnd = position + axisDir * gizmoSize;

                if (!IsPointInFrontOfCamera(axisEnd, camera))
                    continue;

                SysVec2 startScreen = WorldToScreen(position, camera, viewportSize);
                SysVec2 endScreen = WorldToScreen(axisEnd, camera, viewportSize);

                startScreen += viewportPos;
                endScreen += viewportPos;

                bool isActive = _isDragging && _activeAxis == i;
                bool isHovered = !_isDragging && _hoveredAxis == i;
                uint color = GetAxisColor(i, isActive, isHovered);
                uint shadowColor = 0x40000000;
                float thickness = (isActive || isHovered) ? 7.0f : 5.0f;

                drawList.AddLine(startScreen + new SysVec2(2, 2), endScreen + new SysVec2(2, 2), shadowColor, thickness);
                drawList.AddLine(startScreen, endScreen, color, thickness);

                float boxSize = (isActive || isHovered) ? 10.0f : 8.0f;

                drawList.AddRectFilled(
                    new SysVec2(endScreen.X - boxSize + 2, endScreen.Y - boxSize + 2),
                    new SysVec2(endScreen.X + boxSize + 2, endScreen.Y + boxSize + 2),
                    shadowColor,
                    2.0f
                );

                drawList.AddRectFilled(
                    new SysVec2(endScreen.X - boxSize, endScreen.Y - boxSize),
                    new SysVec2(endScreen.X + boxSize, endScreen.Y + boxSize),
                    color,
                    2.0f
                );

                drawList.AddRect(
                    new SysVec2(endScreen.X - boxSize, endScreen.Y - boxSize),
                    new SysVec2(endScreen.X + boxSize, endScreen.Y + boxSize),
                    0xFF000000,
                    2.0f,
                    0,
                    2.0f
                );

                SysVec3 labelPos = position + axisDir * gizmoSize * 1.25f;
                SysVec2 labelScreen = WorldToScreen(labelPos, camera, viewportSize) + viewportPos;

                SysVec2 textSize = ImGui.CalcTextSize(axisLabels[i]);
                SysVec2 bgMin = labelScreen - new SysVec2(textSize.X * 0.5f + 3, textSize.Y * 0.5f + 2);
                SysVec2 bgMax = labelScreen + new SysVec2(textSize.X * 0.5f + 3, textSize.Y * 0.5f + 2);

                drawList.AddRectFilled(bgMin, bgMax, 0xBB000000, 3.0f);
                drawList.AddRect(bgMin, bgMax, color, 3.0f, 0, 1.5f);
                drawList.AddText(new SysVec2(labelScreen.X - textSize.X * 0.5f, labelScreen.Y - textSize.Y * 0.5f), color, axisLabels[i]);
            }

            SysVec2 centerScreen = WorldToScreen(position, camera, viewportSize) + viewportPos;
            bool isCenterActive = _isDragging && _activeAxis == 6;
            bool isCenterHovered = !_isDragging && _hoveredAxis == 6;
            uint centerColor = (isCenterActive || isCenterHovered) ? 0xFFFFDD00 : 0xFFFFFFFF;
            float centerSize = (isCenterActive || isCenterHovered) ? 11.0f : 9.0f;

            drawList.AddRectFilled(
                new SysVec2(centerScreen.X - centerSize + 2, centerScreen.Y - centerSize + 2),
                new SysVec2(centerScreen.X + centerSize + 2, centerScreen.Y + centerSize + 2),
                0x60000000,
                2.0f
            );

            drawList.AddRectFilled(
                new SysVec2(centerScreen.X - centerSize, centerScreen.Y - centerSize),
                new SysVec2(centerScreen.X + centerSize, centerScreen.Y + centerSize),
                centerColor,
                2.0f
            );

            drawList.AddRect(
                new SysVec2(centerScreen.X - centerSize, centerScreen.Y - centerSize),
                new SysVec2(centerScreen.X + centerSize, centerScreen.Y + centerSize),
                0xFF000000,
                2.0f,
                0,
                2.5f
            );
        }

        private static void DrawRotationCircle(ImDrawListPtr drawList, SysVec3 center, int axis, float radius,
            Camera camera, SysVec2 viewportPos, SysVec2 viewportSize, uint color, float thickness, GameObject obj)
        {
            int segments = 96;
            SysVec3 axisDir = GetAxisDirection(axis, obj);

            SysVec3 tangent1, tangent2;
            if (Math.Abs(axisDir.Y) < 0.9f)
            {
                tangent1 = SysVec3.Normalize(SysVec3.Cross(axisDir, SysVec3.UnitY));
            }
            else
            {
                tangent1 = SysVec3.Normalize(SysVec3.Cross(axisDir, SysVec3.UnitX));
            }
            tangent2 = SysVec3.Cross(axisDir, tangent1);

            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)(i * 2 * Math.PI / segments);
                float angle2 = (float)((i + 1) * 2 * Math.PI / segments);

                SysVec3 point1 = center + (tangent1 * MathF.Cos(angle1) + tangent2 * MathF.Sin(angle1)) * radius;
                SysVec3 point2 = center + (tangent1 * MathF.Cos(angle2) + tangent2 * MathF.Sin(angle2)) * radius;

                if (!IsPointInFrontOfCamera(point1, camera) || !IsPointInFrontOfCamera(point2, camera))
                    continue;

                SysVec2 screen1 = WorldToScreen(point1, camera, viewportSize) + viewportPos;
                SysVec2 screen2 = WorldToScreen(point2, camera, viewportSize) + viewportPos;

                drawList.AddLine(screen1, screen2, color, thickness);
            }
        }

        private static void DrawArrowHead(ImDrawListPtr drawList, SysVec2 start, SysVec2 end, uint color, float lineThickness)
        {
            SysVec2 dir = SysVec2.Normalize(end - start);
            SysVec2 perp = new SysVec2(-dir.Y, dir.X);

            float arrowSize = 10.0f + lineThickness * 0.5f;
            float arrowWidth = 0.4f;

            SysVec2 p1 = end - dir * arrowSize + perp * arrowSize * arrowWidth;
            SysVec2 p2 = end - dir * arrowSize - perp * arrowSize * arrowWidth;

            drawList.AddTriangleFilled(
                end + new SysVec2(1.5f, 1.5f),
                p1 + new SysVec2(1.5f, 1.5f),
                p2 + new SysVec2(1.5f, 1.5f),
                0x60000000
            );

            drawList.AddTriangleFilled(end, p1, p2, color);
            drawList.AddTriangle(end, p1, p2, 0xFF000000, 1.5f);
        }

        private static uint GetAxisColor(int axis, bool isActive, bool isHovered = false)
        {
            if (isActive)
            {
                return axis switch
                {
                    0 => 0xFFFF6666,
                    1 => 0xFF66FF66,
                    2 => 0xFF6666FF,
                    _ => 0xFFFFFFFF
                };
            }
            else if (isHovered)
            {
                return axis switch
                {
                    0 => 0xFFFF8888,
                    1 => 0xFF88FF88,
                    2 => 0xFF8888FF,
                    _ => 0xFFFFFFFF
                };
            }
            else
            {
                return axis switch
                {
                    0 => 0xFFDD4444,
                    1 => 0xFF44DD44,
                    2 => 0xFF4444DD,
                    _ => 0xFFFFFFFF
                };
            }
        }

        private static SysVec3 GetAxisDirection(int axis, GameObject obj)
        {
            SysVec3 baseAxis = axis switch
            {
                0 => SysVec3.UnitX,
                1 => SysVec3.UnitY,
                2 => SysVec3.UnitZ,
                _ => SysVec3.Zero
            };

            if (_currentSpace == GizmoSpace.World)
            {
                return baseAxis;
            }

            Quaternion quat = obj.Transform.Rotation;
            Vector3 axis3D = new Vector3(baseAxis.X, baseAxis.Y, baseAxis.Z);
            Vector3 rotated = quat * axis3D;

            return new SysVec3(rotated.X, rotated.Y, rotated.Z);
        }

        private static float GetGizmoSize(SysVec3 position, Camera camera)
        {
            SysVec3 cameraPos = ToSysVec3(camera.Position);
            float distance = SysVec3.Distance(position, cameraPos);
            return Math.Max(0.5f, distance * 0.15f);
        }

        private static SysVec2 WorldToScreen(SysVec3 worldPos, Camera camera, SysVec2 viewportSize)
        {
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();

            Vector4 worldPos4 = new Vector4(worldPos.X, worldPos.Y, worldPos.Z, 1.0f);

            Vector4 viewSpace = worldPos4 * view;
            Vector4 clipSpace = viewSpace * projection;

            if (Math.Abs(clipSpace.W) < 0.0001f)
                clipSpace.W = 0.0001f;

            Vector3 ndc = new Vector3(
                clipSpace.X / clipSpace.W,
                clipSpace.Y / clipSpace.W,
                clipSpace.Z / clipSpace.W
            );

            SysVec2 screen = new SysVec2(
                (ndc.X + 1.0f) * 0.5f * viewportSize.X,
                (1.0f - ndc.Y) * 0.5f * viewportSize.Y
            );

            return screen;
        }

        private static float DistanceToLineSegment(SysVec2 point, SysVec2 lineStart, SysVec2 lineEnd)
        {
            SysVec2 line = lineEnd - lineStart;
            float lineLength = line.Length();

            if (lineLength < 0.001f)
                return SysVec2.Distance(point, lineStart);

            SysVec2 lineDir = line / lineLength;
            SysVec2 toPoint = point - lineStart;

            float projection = SysVec2.Dot(toPoint, lineDir);
            projection = Math.Clamp(projection, 0, lineLength);

            SysVec2 closestPoint = lineStart + lineDir * projection;
            return SysVec2.Distance(point, closestPoint);
        }

        private static SysVec3 ToSysVec3(Vector3 v)
        {
            return new SysVec3(v.X, v.Y, v.Z);
        }
    }
}