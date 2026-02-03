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
        private static SysVec2 _dragStartPos = SysVec2.Zero;
        private static SysVec3 _objectStartPos = SysVec3.Zero;
        private static SysVec3 _objectStartRot = SysVec3.Zero;
        private static SysVec3 _objectStartScale = SysVec3.One;
        private static int _activeAxis = -1;
        private static SysVec2 _lastMousePos = SysVec2.Zero;

        private static SysVec3 _accumulatedPos = SysVec3.Zero;
        private static SysVec3 _accumulatedRot = SysVec3.Zero;
        private static SysVec3 _accumulatedScale = SysVec3.One;

        private static float _translateSnapValue = 0.5f;  
        private static float _rotateSnapValue = 15.0f;    
        private static float _scaleSnapValue = 0.1f;      
        private static bool _snapEnabled = false;         

        public static GizmoMode CurrentMode => _currentMode;
        public static GizmoSpace CurrentSpace => _currentSpace;

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
            }
        }

        public static void ToggleSpace()
        {
            _currentSpace = _currentSpace == GizmoSpace.World ? GizmoSpace.Local : GizmoSpace.World;
        }

        public static void Draw(
            GameObject? selectedObject,
            CameraComponent? camera,
            SysVec2 viewportPos,
            SysVec2 viewportSize,
            bool isMouseOverViewport)
        {
            if (selectedObject == null || camera == null)
            {
                _isDragging = false;
                return;
            }

            SysVec3 objectPos = ToSysVec3(selectedObject.Transform.LocalPosition);
            SysVec3 objectRot = ToSysVec3(selectedObject.Transform.LocalRotation);
            SysVec3 objectScale = ToSysVec3(selectedObject.Transform.LocalScale);

            ImGuiIOPtr io = ImGui.GetIO();
            SysVec2 mousePos = new SysVec2(io.MousePos.X - viewportPos.X, io.MousePos.Y - viewportPos.Y);
            bool mouseInViewport = isMouseOverViewport &&
                mousePos.X >= 0 && mousePos.X <= viewportSize.X &&
                mousePos.Y >= 0 && mousePos.Y <= viewportSize.Y;

            HandleInput(selectedObject, camera, mousePos, viewportSize, mouseInViewport);

            switch (_currentMode)
            {
                case GizmoMode.Translate:
                    DrawTranslateGizmo(objectPos, objectRot, camera, viewportPos, viewportSize);
                    break;
                case GizmoMode.Rotate:
                    DrawRotateGizmo(objectPos, objectRot, camera, viewportPos, viewportSize);
                    break;
                case GizmoMode.Scale:
                    DrawScaleGizmo(objectPos, objectRot, camera, viewportPos, viewportSize);
                    break;
            }
        }

        private static void HandleInput(
            GameObject selectedObject,
            CameraComponent camera,
            SysVec2 mousePos,
            SysVec2 viewportSize,
            bool mouseInViewport)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            bool mouseDown = io.MouseDown[0];

            if (!mouseInViewport && !_isDragging)
            {
                return;
            }

            SysVec3 objectPos = ToSysVec3(selectedObject.Transform.LocalPosition);
            SysVec3 objectRot = ToSysVec3(selectedObject.Transform.LocalRotation);

            if (mouseDown && !_isDragging && mouseInViewport)
            {
                _activeAxis = GetHoveredAxis(objectPos, objectRot, camera, mousePos, viewportSize);

                if (_activeAxis >= 0)
                {
                    _isDragging = true;
                    _dragStartPos = mousePos;
                    _objectStartPos = ToSysVec3(selectedObject.Transform.LocalPosition);
                    _objectStartRot = ToSysVec3(selectedObject.Transform.LocalRotation);
                    _objectStartScale = ToSysVec3(selectedObject.Transform.LocalScale);
                    _lastMousePos = mousePos;

                    // Inicializar valores acumulados
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
                }
            }
        }

        private static void ApplyTransform(GameObject obj, SysVec2 delta, SysVec2 mousePos, CameraComponent camera, SysVec2 viewportSize)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            bool snapEnabled = _snapEnabled ^ io.KeyCtrl;

            switch (_currentMode)
            {
                case GizmoMode.Translate:
                    {
                        SysVec3 currentRot = ToSysVec3(obj.Transform.LocalRotation);
                        SysVec3 axisDir = GetAxisDirection(_activeAxis, currentRot);

                        SysVec2 objScreen = WorldToScreen(_accumulatedPos, camera, viewportSize);
                        SysVec2 axisEndScreen = WorldToScreen(_accumulatedPos + axisDir, camera, viewportSize);
                        SysVec2 screenAxisDir = SysVec2.Normalize(axisEndScreen - objScreen);

                        float movementAlongAxis = SysVec2.Dot(delta, screenAxisDir);

                        float moveSpeed = 0.01f;
                        SysVec3 movement = axisDir * movementAlongAxis * moveSpeed;

                        _accumulatedPos += movement;

                        SysVec3 finalPos = _accumulatedPos;
                        
                        if (snapEnabled)
                        {
                            if (_activeAxis == 0) // X
                                finalPos.X = MathF.Round(_accumulatedPos.X / _translateSnapValue) * _translateSnapValue;
                            else if (_activeAxis == 1) // Y
                                finalPos.Y = MathF.Round(_accumulatedPos.Y / _translateSnapValue) * _translateSnapValue;
                            else if (_activeAxis == 2) // Z
                                finalPos.Z = MathF.Round(_accumulatedPos.Z / _translateSnapValue) * _translateSnapValue;
                        }

                        obj.Transform.SetPosition(finalPos.X, finalPos.Y, finalPos.Z);
                    }
                    break;

                case GizmoMode.Rotate:
                    {
                        SysVec3 currentRot = ToSysVec3(obj.Transform.LocalRotation);
                        SysVec3 objectPos = ToSysVec3(obj.Transform.LocalPosition);

                        SysVec3 axisDir = GetAxisDirection(_activeAxis, currentRot);
                        SysVec2 objScreen = WorldToScreen(objectPos, camera, viewportSize);

                        SysVec3 cameraPos = ToSysVec3(camera.Position);
                        SysVec3 toCamera = SysVec3.Normalize(cameraPos - objectPos);

                        SysVec3 tangent = SysVec3.Normalize(SysVec3.Cross(axisDir, toCamera));
                        SysVec3 tangentEnd = objectPos + tangent * 0.5f;
                        SysVec2 tangentScreen = WorldToScreen(tangentEnd, camera, viewportSize);
                        SysVec2 screenTangent = SysVec2.Normalize(tangentScreen - objScreen);

                        float rotationAmount = SysVec2.Dot(delta, screenTangent);
                        float rotationSpeed = 0.5f;
                        float rotation = rotationAmount * rotationSpeed;

                        if (_activeAxis == 0)
                            _accumulatedRot.X += rotation;
                        else if (_activeAxis == 1)
                            _accumulatedRot.Y += rotation;
                        else if (_activeAxis == 2)
                            _accumulatedRot.Z += rotation;

                        SysVec3 finalRot = _accumulatedRot;

                        if (snapEnabled)
                        {
                            if (_activeAxis == 0)
                                finalRot.X = MathF.Round(_accumulatedRot.X / _rotateSnapValue) * _rotateSnapValue;
                            else if (_activeAxis == 1)
                                finalRot.Y = MathF.Round(_accumulatedRot.Y / _rotateSnapValue) * _rotateSnapValue;
                            else if (_activeAxis == 2)
                                finalRot.Z = MathF.Round(_accumulatedRot.Z / _rotateSnapValue) * _rotateSnapValue;
                        }

                        obj.Transform.SetRotation(finalRot.X, finalRot.Y, finalRot.Z);
                    }
                    break;

                case GizmoMode.Scale:
                    {
                        SysVec3 objectPos = ToSysVec3(obj.Transform.LocalPosition);
                        SysVec3 currentRot = ToSysVec3(obj.Transform.LocalRotation);

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
                            SysVec3 axisDir = GetAxisDirection(_activeAxis, currentRot);

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

        private static int GetHoveredAxis(SysVec3 objectPos, SysVec3 objectRot, CameraComponent camera, SysVec2 mousePos, SysVec2 viewportSize)
        {
            float gizmoSize = GetGizmoSize(objectPos, camera);

            if (_currentMode == GizmoMode.Rotate)
            {
                float closestDistance = float.MaxValue;
                int closestAxis = -1;
                float clickThreshold = 50.0f; 

                for (int axis = 0; axis < 3; axis++)
                {
                    SysVec3 axisDir = GetAxisDirection(axis, objectRot);

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
                float threshold = 18.0f; 
                float closestDist = float.MaxValue;
                int closestAxis = -1;

                for (int axis = 0; axis < 3; axis++)
                {
                    SysVec3 axisDir = GetAxisDirection(axis, objectRot);
                    SysVec3 axisEnd = objectPos + axisDir * gizmoSize;

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

            if (_currentMode == GizmoMode.Scale)
            {
                SysVec2 centerScreen = WorldToScreen(objectPos, camera, viewportSize);
                if (SysVec2.Distance(mousePos, centerScreen) < 20.0f)
                    return 6;
            }

            return -1;
        }

        private static void DrawTranslateGizmo(SysVec3 position, SysVec3 rotation, CameraComponent camera, SysVec2 viewportPos, SysVec2 viewportSize)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float gizmoSize = GetGizmoSize(position, camera);

            string[] axisLabels = { "X", "Y", "Z" };

            for (int i = 0; i < 3; i++)
            {
                SysVec3 axisDir = GetAxisDirection(i, rotation);
                SysVec3 axisEnd = position + axisDir * gizmoSize;

                SysVec2 startScreen = WorldToScreen(position, camera, viewportSize);
                SysVec2 endScreen = WorldToScreen(axisEnd, camera, viewportSize);

                startScreen += viewportPos;
                endScreen += viewportPos;

                uint color = GetAxisColor(i, _isDragging && _activeAxis == i);
                float thickness = _isDragging && _activeAxis == i ? 6.0f : 4.5f;

                drawList.AddLine(startScreen, endScreen, color, thickness);
                DrawArrowHead(drawList, startScreen, endScreen, color);

                SysVec3 labelPos = position + axisDir * gizmoSize * 1.15f;
                SysVec2 labelScreen = WorldToScreen(labelPos, camera, viewportSize) + viewportPos;
                drawList.AddText(labelScreen, color, axisLabels[i]);
            }

            SysVec2 centerScreen = WorldToScreen(position, camera, viewportSize) + viewportPos;
            drawList.AddCircleFilled(centerScreen, 6.0f, 0xFFFFFFFF);
        }

        private static void DrawRotateGizmo(SysVec3 position, SysVec3 rotation, CameraComponent camera, SysVec2 viewportPos, SysVec2 viewportSize)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float gizmoSize = GetGizmoSize(position, camera);
            SysVec2 centerScreen = WorldToScreen(position, camera, viewportSize) + viewportPos;

            string[] axisLabels = { "X", "Y", "Z" };

            for (int i = 0; i < 3; i++)
            {
                uint color = GetAxisColor(i, _isDragging && _activeAxis == i);
                float thickness = _isDragging && _activeAxis == i ? 5.5f : 4.0f;

                DrawRotationCircle(drawList, position, i, gizmoSize, camera, viewportPos, viewportSize, color, thickness, rotation);

                SysVec3 axisDir = GetAxisDirection(i, rotation);
                SysVec3 labelPos = position + axisDir * gizmoSize * 1.15f;
                SysVec2 labelScreen = WorldToScreen(labelPos, camera, viewportSize) + viewportPos;
                drawList.AddText(labelScreen, color, axisLabels[i]);
            }

            drawList.AddCircleFilled(centerScreen, 6.0f, 0xFFFFFFFF);
        }

        private static void DrawScaleGizmo(SysVec3 position, SysVec3 rotation, CameraComponent camera, SysVec2 viewportPos, SysVec2 viewportSize)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            float gizmoSize = GetGizmoSize(position, camera);

            string[] axisLabels = { "X", "Y", "Z" };

            for (int i = 0; i < 3; i++)
            {
                SysVec3 axisDir = GetAxisDirection(i, rotation);
                SysVec3 axisEnd = position + axisDir * gizmoSize;

                SysVec2 startScreen = WorldToScreen(position, camera, viewportSize);
                SysVec2 endScreen = WorldToScreen(axisEnd, camera, viewportSize);

                startScreen += viewportPos;
                endScreen += viewportPos;

                uint color = GetAxisColor(i, _isDragging && _activeAxis == i);
                float thickness = _isDragging && _activeAxis == i ? 6.0f : 4.5f;

                drawList.AddLine(startScreen, endScreen, color, thickness);

                float boxSize = 8.0f;
                drawList.AddRectFilled(
                    new SysVec2(endScreen.X - boxSize, endScreen.Y - boxSize),
                    new SysVec2(endScreen.X + boxSize, endScreen.Y + boxSize),
                    color
                );

                SysVec3 labelPos = position + axisDir * gizmoSize * 1.15f;
                SysVec2 labelScreen = WorldToScreen(labelPos, camera, viewportSize) + viewportPos;
                drawList.AddText(labelScreen, color, axisLabels[i]);
            }

            SysVec2 centerScreen = WorldToScreen(position, camera, viewportSize) + viewportPos;
            uint centerColor = _isDragging && _activeAxis == 6 ? 0xFFFFFF00 : 0xFFFFFFFF;
            drawList.AddRectFilled(
                new SysVec2(centerScreen.X - 8, centerScreen.Y - 8),
                new SysVec2(centerScreen.X + 8, centerScreen.Y + 8),
                centerColor
            );
        }

        private static void DrawRotationCircle(ImDrawListPtr drawList, SysVec3 center, int axis, float radius,
            CameraComponent camera, SysVec2 viewportPos, SysVec2 viewportSize, uint color, float thickness, SysVec3 rotation)
        {
            int segments = 64;
            SysVec3 axisDir = GetAxisDirection(axis, rotation);

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

                SysVec2 screen1 = WorldToScreen(point1, camera, viewportSize) + viewportPos;
                SysVec2 screen2 = WorldToScreen(point2, camera, viewportSize) + viewportPos;

                drawList.AddLine(screen1, screen2, color, thickness);
            }
        }

        private static void DrawArrowHead(ImDrawListPtr drawList, SysVec2 start, SysVec2 end, uint color)
        {
            SysVec2 dir = SysVec2.Normalize(end - start);
            SysVec2 perp = new SysVec2(-dir.Y, dir.X);

            float arrowSize = 18.0f;  
            float arrowWidth = 0.6f;  
            SysVec2 p1 = end - dir * arrowSize + perp * arrowSize * arrowWidth;
            SysVec2 p2 = end - dir * arrowSize - perp * arrowSize * arrowWidth;

            drawList.AddTriangleFilled(end, p1, p2, color);
        }

        private static uint GetAxisColor(int axis, bool isActive)
        {
            if (isActive)
            {
                return axis switch
                {
                    0 => 0xFFFF5555,
                    1 => 0xFF55FF55,
                    2 => 0xFF5555FF,
                    _ => 0xFFFFFFFF
                };
            }
            else
            {
                return axis switch
                {
                    0 => 0xFFCC3333,
                    1 => 0xFF33CC33,
                    2 => 0xFF3333CC,
                    _ => 0xFFFFFFFF
                };
            }
        }

        private static SysVec3 GetAxisDirection(int axis, SysVec3 rotation)
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

            float rotX = rotation.X * MathF.PI / 180.0f;
            float rotY = rotation.Y * MathF.PI / 180.0f;
            float rotZ = rotation.Z * MathF.PI / 180.0f;

            float cosY = MathF.Cos(rotY);
            float sinY = MathF.Sin(rotY);

            float cosX = MathF.Cos(rotX);
            float sinX = MathF.Sin(rotX);

            float cosZ = MathF.Cos(rotZ);
            float sinZ = MathF.Sin(rotZ);

            SysVec3 rotated = baseAxis;

            float tempX = rotated.X * cosY + rotated.Z * sinY;
            float tempZ = -rotated.X * sinY + rotated.Z * cosY;
            rotated.X = tempX;
            rotated.Z = tempZ;

            float tempY = rotated.Y * cosX - rotated.Z * sinX;
            tempZ = rotated.Y * sinX + rotated.Z * cosX;
            rotated.Y = tempY;
            rotated.Z = tempZ;

            tempX = rotated.X * cosZ - rotated.Y * sinZ;
            tempY = rotated.X * sinZ + rotated.Y * cosZ;
            rotated.X = tempX;
            rotated.Y = tempY;

            return SysVec3.Normalize(rotated);
        }

        private static float GetGizmoSize(SysVec3 position, CameraComponent camera)
        {
            SysVec3 cameraPos = ToSysVec3(camera.Position);
            float distance = SysVec3.Distance(position, cameraPos);
            return Math.Max(0.5f, distance * 0.15f);
        }

        private static SysVec2 WorldToScreen(SysVec3 worldPos, CameraComponent camera, SysVec2 viewportSize)
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