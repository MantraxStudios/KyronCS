using ImGuiNET;
using KrayonCore;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace KrayonEditor.UI
{
    public class SpriteAnimationUI : UIBehaviour
    {
        private SpriteRenderer _selectedSprite;
        private SpriteClip _selectedClip;
        private int _selectedFrameIndex = -1;

        private string _newClipName = "";
        private float _previewTimer = 0.0f;
        private int _previewFrameIndex = 0;
        private bool _isPreviewPlaying = false;

        private bool _showGridPicker = false;
        private int _hoveredTileX = -1;
        private int _hoveredTileY = -1;

        public override void OnDrawUI()
        {
            ImGui.SetNextWindowSize(new Vector2(1200, 700), ImGuiCond.FirstUseEver);
            ImGui.Begin("Animation Editor", ImGuiWindowFlags.MenuBar);

            DrawMenuBar();

            var windowSize = ImGui.GetContentRegionAvail();

            float leftWidth = 220;
            ImGui.BeginChild("SpriteSelector", new Vector2(leftWidth, windowSize.Y));
            DrawSpriteSelector();
            ImGui.EndChild();

            ImGui.SameLine();

            if (_selectedSprite != null)
            {
                float middleWidth = 300;
                ImGui.BeginChild("AnimationPanel", new Vector2(middleWidth, windowSize.Y));
                DrawAnimationPanel();
                ImGui.EndChild();

                ImGui.SameLine();

                float rightWidth = windowSize.X - leftWidth - middleWidth - 30;
                ImGui.BeginChild("TimelinePanel", new Vector2(rightWidth, windowSize.Y));
                DrawTimelinePanel();
                ImGui.EndChild();
            }
            else
            {
                ImGui.BeginChild("EmptyState", new Vector2(0, 0));
                DrawEmptyState();
                ImGui.EndChild();
            }

            ImGui.End();

            if (_showGridPicker && _selectedSprite != null && _selectedClip != null)
            {
                DrawGridPicker();
            }
        }

        private void DrawMenuBar()
        {
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Save All Animations")) { }
                    ImGui.Separator();
                    if (ImGui.MenuItem("Close")) { }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Help"))
                {
                    if (ImGui.MenuItem("Quick Start Guide")) { }
                    ImGui.EndMenu();
                }

                ImGui.EndMenuBar();
            }
        }

        private void DrawSpriteSelector()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.9f, 1.0f, 1.0f));
            ImGui.Text("SPRITES");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();

            var scene = SceneManager.ActiveScene;
            if (scene == null)
            {
                ImGui.TextDisabled("No active scene loaded");
                return;
            }

            var gameObjects = scene.GetAllGameObjects();
            bool foundSprites = false;

            foreach (var go in gameObjects)
            {
                var sprite = go.GetComponent<SpriteRenderer>();
                if (sprite != null)
                {
                    foundSprites = true;
                    bool isSelected = _selectedSprite == sprite;

                    if (isSelected)
                        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.3f, 0.5f, 0.8f, 0.8f));

                    if (ImGui.Selectable($"{go.Name}##{go.GetHashCode()}", isSelected, ImGuiSelectableFlags.None, new Vector2(0, 30)))
                    {
                        SelectSprite(sprite);
                    }

                    if (isSelected)
                        ImGui.PopStyleColor();

                    if (isSelected)
                    {
                        ImGui.Indent();
                        ImGui.TextDisabled($"{sprite.TilesPerRow}x{sprite.TilesPerColumn} grid");
                        ImGui.TextDisabled($"{sprite.Animations.Count} animations");
                        ImGui.Unindent();
                    }

                    ImGui.Spacing();
                }
            }

            if (!foundSprites)
            {
                ImGui.TextDisabled("No sprites in scene");
                ImGui.Spacing();
                ImGui.TextWrapped("Add a SpriteRenderer component to a GameObject to get started.");
            }
        }

        private void DrawAnimationPanel()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.9f, 1.0f, 1.0f));
            ImGui.Text("ANIMATIONS");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();

            DrawQuickActions();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Animation Clips:");
            ImGui.BeginChild("AnimationList", new Vector2(0, 200));

            for (int i = 0; i < _selectedSprite.Animations.Count; i++)
            {
                var clip = _selectedSprite.Animations[i];
                bool isSelected = _selectedClip == clip;

                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.3f, 0.5f, 0.8f, 0.8f));

                string icon = clip.Loop ? "[LOOP]" : "[ONCE]";
                if (ImGui.Selectable($"{icon} {clip.Name}##{i}", isSelected, ImGuiSelectableFlags.None, new Vector2(0, 25)))
                {
                    SelectClip(clip);
                }

                if (isSelected)
                    ImGui.PopStyleColor();

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text($"{clip.Frames.Count} frames @ {clip.FrameRate:F1} fps");
                    ImGui.Text(clip.Loop ? "Looping" : "One-shot");
                    ImGui.EndTooltip();
                }
            }

            ImGui.EndChild();

            ImGui.Spacing();
            ImGui.InputTextWithHint("##NewAnimName", "New animation name...", ref _newClipName, 128);

            bool canCreate = !string.IsNullOrWhiteSpace(_newClipName);
            if (!canCreate) ImGui.BeginDisabled();

            if (ImGui.Button("+ Create Animation", new Vector2(-1, 30)))
            {
                CreateNewAnimation();
            }

            if (!canCreate) ImGui.EndDisabled();

            if (_selectedClip != null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                DrawAnimationSettings();
            }
        }

        private void DrawQuickActions()
        {
            ImGui.Text("Playback:");

            bool isPlaying = _selectedSprite.IsPlaying;
            if (ImGui.Checkbox("Playing", ref isPlaying))
            {
                _selectedSprite.IsPlaying = isPlaying;
            }

            float speed = _selectedSprite.AnimationSpeed;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##Speed", ref speed, 0.1f, 3.0f, "Speed: %.1fx"))
            {
                _selectedSprite.AnimationSpeed = speed;
            }
        }

        private void DrawAnimationSettings()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.9f, 0.7f, 1.0f));
            ImGui.Text($"Settings: {_selectedClip.Name}");
            ImGui.PopStyleColor();

            ImGui.Spacing();

            string name = _selectedClip.Name;
            ImGui.Text("Name:");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##ClipName", ref name, 256))
            {
                _selectedClip.Name = name;
            }

            ImGui.Spacing();
            float frameRate = _selectedClip.FrameRate;
            ImGui.Text("Frame Rate:");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##FrameRate", ref frameRate, 1.0f, 60.0f, "%.1f fps"))
            {
                _selectedClip.FrameRate = frameRate;
            }

            ImGui.Spacing();
            bool loop = _selectedClip.Loop;
            if (ImGui.Checkbox("Loop Animation", ref loop))
            {
                _selectedClip.Loop = loop;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Play This Animation", new Vector2(-1, 25)))
            {
                _selectedSprite.Play(_selectedClip.Name);
            }

            if (ImGui.Button("Delete Animation", new Vector2(-1, 25)))
            {
                if (_selectedSprite.Animations.Count > 1)
                {
                    _selectedSprite.RemoveAnimation(_selectedClip.Name);
                    SelectClip(null);
                }
            }

            if (_selectedSprite.Animations.Count <= 1)
            {
                ImGui.TextDisabled("(Keep at least 1 animation)");
            }
        }

        private void DrawTimelinePanel()
        {
            if (_selectedClip == null)
            {
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 200);
                var textSize = ImGui.CalcTextSize("Select an animation to edit");
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - textSize.X) * 0.5f);
                ImGui.TextDisabled("Select an animation to edit");
                return;
            }

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.9f, 1.0f, 1.0f));
            ImGui.Text("PREVIEW");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();

            DrawPreviewControls();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawVisualPreview();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.9f, 1.0f, 1.0f));
            ImGui.Text("TIMELINE");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();

            DrawTimeline();
        }

        private void DrawPreviewControls()
        {
            if (_selectedClip.Frames.Count == 0)
            {
                ImGui.TextDisabled("No frames yet. Add frames below!");
                return;
            }

            string playButtonText = _isPreviewPlaying ? "Pause" : "Play";
            if (ImGui.Button(playButtonText, new Vector2(100, 30)))
            {
                _isPreviewPlaying = !_isPreviewPlaying;
                if (_isPreviewPlaying)
                {
                    _previewTimer = 0.0f;
                    _previewFrameIndex = Math.Max(0, Math.Min(_previewFrameIndex, _selectedClip.Frames.Count - 1));
                }
            }

            ImGui.SameLine();

            if (ImGui.Button("Stop", new Vector2(100, 30)))
            {
                _isPreviewPlaying = false;
                _previewFrameIndex = 0;
                _previewTimer = 0.0f;

                if (_selectedClip.Frames.Count > 0)
                {
                    var frame = _selectedClip.Frames[0];
                    _selectedSprite.SetTile(frame.TileIndexX, frame.TileIndexY);
                }
            }

            ImGui.SameLine();

            ImGui.Text($"  Frame: {_previewFrameIndex + 1} / {_selectedClip.Frames.Count}");

            float duration = _selectedClip.Frames.Count / _selectedClip.FrameRate;
            ImGui.Text($"Duration: {duration:F2}s @ {_selectedClip.FrameRate:F1} fps");

            ImGui.Spacing();
            float progress = _selectedClip.Frames.Count > 1
                ? (float)_previewFrameIndex / (_selectedClip.Frames.Count - 1)
                : 0f;
            ImGui.ProgressBar(progress, new Vector2(-1, 20));

            int scrubFrame = _previewFrameIndex;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderInt("##Scrubber", ref scrubFrame, 0, _selectedClip.Frames.Count - 1, "Frame %d"))
            {
                _previewFrameIndex = scrubFrame;
                _isPreviewPlaying = false;

                if (_previewFrameIndex >= 0 && _previewFrameIndex < _selectedClip.Frames.Count)
                {
                    var frame = _selectedClip.Frames[_previewFrameIndex];
                    _selectedSprite.SetTile(frame.TileIndexX, frame.TileIndexY);
                }
            }

            if (_isPreviewPlaying)
            {
                UpdatePreviewAnimation();
            }
        }

        private void DrawVisualPreview()
        {
            if (_selectedClip.Frames.Count == 0)
            {
                return;
            }

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.9f, 1.0f, 1.0f));
            ImGui.Text("VISUAL PREVIEW");
            ImGui.PopStyleColor();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.BeginChild("VisualPreviewArea", new Vector2(0, 180));

            if (_previewFrameIndex >= 0 && _previewFrameIndex < _selectedClip.Frames.Count)
            {
                var currentFrame = _selectedClip.Frames[_previewFrameIndex];

                var regionAvail = ImGui.GetContentRegionAvail();
                float previewSize = 100f;
                float startX = (regionAvail.X - previewSize) * 0.5f;
                float startY = (180 - previewSize - 30) * 0.5f;

                ImGui.SetCursorPosX(startX);
                ImGui.SetCursorPosY(startY);

                var drawList = ImGui.GetWindowDrawList();
                var cursorPos = ImGui.GetCursorScreenPos();

                // Intentar obtener la textura
                IntPtr textureId = IntPtr.Zero;
                bool hasTexture = false;
                float texWidth = 1f;
                float texHeight = 1f;
                float tileWidth = _selectedSprite.TileWidth;
                float tileHeight = _selectedSprite.TileHeight;

                try
                {
                    if (_selectedSprite.Material.AlbedoTexture != null)
                    {
                        textureId = _selectedSprite.Material.AlbedoTexture.TextureId;
                        if (textureId != IntPtr.Zero)
                        {
                            hasTexture = true;
                            texWidth = _selectedSprite.TextureWidth;
                            texHeight = _selectedSprite.TextureHeight;
                        }
                    }
                }
                catch
                {
                    hasTexture = false;
                }

                if (hasTexture)
                {
                    // Calcular UVs con inversión vertical (intercambiar v0 y v1)
                    float u0 = (currentFrame.TileIndexX * tileWidth) / texWidth;
                    float v0 = ((currentFrame.TileIndexY + 1) * tileHeight) / texHeight;
                    float u1 = ((currentFrame.TileIndexX + 1) * tileWidth) / texWidth;
                    float v1 = (currentFrame.TileIndexY * tileHeight) / texHeight;

                    // Dibujar la imagen del tile (v0 y v1 intercambiados invierte verticalmente)
                    drawList.AddImage(
                        textureId,
                        cursorPos,
                        new Vector2(cursorPos.X + previewSize, cursorPos.Y + previewSize),
                        new Vector2(u0, v0),
                        new Vector2(u1, v1)
                    );

                    // Borde alrededor de la imagen
                    drawList.AddRect(
                        cursorPos,
                        new Vector2(cursorPos.X + previewSize, cursorPos.Y + previewSize),
                        ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.6f, 1.0f)),
                        5f,
                        ImDrawFlags.None,
                        2f
                    );
                }
                else
                {
                    // Fallback si no hay textura
                    drawList.AddRectFilled(
                        cursorPos,
                        new Vector2(cursorPos.X + previewSize, cursorPos.Y + previewSize),
                        ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.2f, 1.0f)),
                        5f
                    );

                    drawList.AddRect(
                        cursorPos,
                        new Vector2(cursorPos.X + previewSize, cursorPos.Y + previewSize),
                        ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.6f, 1.0f)),
                        5f,
                        ImDrawFlags.None,
                        2f
                    );

                    // Texto del tile (solo si no hay textura)
                    string tileText = $"Tile [{currentFrame.TileIndexX}, {currentFrame.TileIndexY}]";
                    var textSize = ImGui.CalcTextSize(tileText);
                    var textPos = new Vector2(
                        cursorPos.X + (previewSize - textSize.X) * 0.5f,
                        cursorPos.Y + (previewSize - textSize.Y) * 0.5f
                    );
                    drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), tileText);
                }

                // Indicador de reproducción
                if (_isPreviewPlaying)
                {
                    drawList.AddCircleFilled(
                        new Vector2(cursorPos.X + 10, cursorPos.Y + 10),
                        5f,
                        ImGui.GetColorU32(new Vector4(0.2f, 0.8f, 0.2f, 1f))
                    );
                }

                // Mueve el cursor DENTRO del child para el texto del frame
                ImGui.SetCursorPosX(startX);
                ImGui.SetCursorPosY(startY + previewSize + 5);

                // Usa ImGui.Text normal en lugar de DrawList
                string frameText = $"Frame {_previewFrameIndex + 1}/{_selectedClip.Frames.Count}";
                var frameTextSize = ImGui.CalcTextSize(frameText);
                ImGui.SetCursorPosX((regionAvail.X - frameTextSize.X) * 0.5f);
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.9f, 1f), frameText);
            }

            ImGui.EndChild();
        }

        private void UpdatePreviewAnimation()
        {
            float deltaTime = ImGui.GetIO().DeltaTime;
            _previewTimer += deltaTime;

            float frameDuration = 1.0f / _selectedClip.FrameRate;

            if (_previewTimer >= frameDuration)
            {
                _previewTimer -= frameDuration;
                _previewFrameIndex++;

                if (_previewFrameIndex >= _selectedClip.Frames.Count)
                {
                    if (_selectedClip.Loop)
                    {
                        _previewFrameIndex = 0;
                    }
                    else
                    {
                        _previewFrameIndex = _selectedClip.Frames.Count - 1;
                        _isPreviewPlaying = false;
                    }
                }

                if (_previewFrameIndex >= 0 && _previewFrameIndex < _selectedClip.Frames.Count)
                {
                    var frame = _selectedClip.Frames[_previewFrameIndex];
                    _selectedSprite.SetTile(frame.TileIndexX, frame.TileIndexY);
                }
            }
        }

        private void DrawTimeline()
        {
            if (ImGui.Button("+ Add Frame from Grid", new Vector2(-1, 30)))
            {
                _showGridPicker = true;
            }

            ImGui.Spacing();
            ImGui.Text($"{_selectedClip.Frames.Count} frames:");
            ImGui.Separator();

            ImGui.BeginChild("FramesTimeline", new Vector2(0, 0));

            float thumbnailSize = 60f;
            float frameHeight = 120f;

            // Intentar obtener la textura
            IntPtr textureId = IntPtr.Zero;
            bool hasTexture = false;
            float texWidth = 1f;
            float texHeight = 1f;
            float tileWidth = _selectedSprite.TileWidth;
            float tileHeight = _selectedSprite.TileHeight;

            try
            {
                if (_selectedSprite.Material.AlbedoTexture != null)
                {
                    textureId = _selectedSprite.Material.AlbedoTexture.TextureId;
                    if (textureId != IntPtr.Zero)
                    {
                        hasTexture = true;
                        texWidth = _selectedSprite.TextureWidth;
                        texHeight = _selectedSprite.TextureHeight;
                    }
                }
            }
            catch
            {
                hasTexture = false;
            }

            for (int i = 0; i < _selectedClip.Frames.Count; i++)
            {
                ImGui.PushID(i);

                var frame = _selectedClip.Frames[i];
                bool isSelected = _selectedFrameIndex == i;
                bool isPreview = _previewFrameIndex == i;

                var startPos = ImGui.GetCursorPos();
                var cursorPos = ImGui.GetCursorScreenPos();

                // Dibuja el fondo si está seleccionado o en preview
                if (isSelected)
                {
                    ImGui.GetWindowDrawList().AddRectFilled(
                        cursorPos,
                        new Vector2(cursorPos.X + thumbnailSize + 120, cursorPos.Y + frameHeight),
                        ImGui.GetColorU32(new Vector4(0.3f, 0.5f, 0.8f, 0.3f)),
                        5f
                    );
                }
                else if (isPreview)
                {
                    ImGui.GetWindowDrawList().AddRectFilled(
                        cursorPos,
                        new Vector2(cursorPos.X + thumbnailSize + 120, cursorPos.Y + frameHeight),
                        ImGui.GetColorU32(new Vector4(0.3f, 0.8f, 0.3f, 0.2f)),
                        5f
                    );
                }

                // Número del frame
                ImGui.GetWindowDrawList().AddRectFilled(
                    cursorPos,
                    new Vector2(cursorPos.X + 30, cursorPos.Y + 20),
                    ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.3f, 0.9f)),
                    3f
                );

                ImGui.SetCursorPos(new Vector2(startPos.X + 5, startPos.Y + 2));
                ImGui.Text($"{i + 1}");

                // Dibujar thumbnail del sprite
                if (hasTexture)
                {
                    ImGui.SetCursorPos(new Vector2(startPos.X, startPos.Y + 25));
                    
                    // Calcular UVs con inversión vertical (intercambiar v0 y v1)
                    float u0 = (frame.TileIndexX * tileWidth) / texWidth;
                    float v0 = ((frame.TileIndexY + 1) * tileHeight) / texHeight;
                    float u1 = ((frame.TileIndexX + 1) * tileWidth) / texWidth;
                    float v1 = (frame.TileIndexY * tileHeight) / texHeight;

                    ImGui.Image(textureId, new Vector2(thumbnailSize, thumbnailSize),
                        new Vector2(u0, v0), new Vector2(u1, v1));
                }

                ImGui.SetCursorPos(new Vector2(startPos.X + thumbnailSize + 5, startPos.Y + 25));
                ImGui.Text($"Tile [{frame.TileIndexX}, {frame.TileIndexY}]");

                // Botón invisible para clics
                ImGui.SetCursorPos(startPos);
                ImGui.InvisibleButton($"##frame{i}", new Vector2(thumbnailSize + 120, 50));

                if (ImGui.IsItemClicked())
                {
                    _selectedFrameIndex = i;
                    _selectedSprite.SetTile(frame.TileIndexX, frame.TileIndexY);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.BeginTooltip();
                    ImGui.Text($"Frame {i + 1}: Tile [{frame.TileIndexX}, {frame.TileIndexY}]");
                    ImGui.Text("Click to select - Double-click to preview");
                    ImGui.EndTooltip();
                }

                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    _previewFrameIndex = i;
                    _isPreviewPlaying = false;
                    _selectedSprite.SetTile(frame.TileIndexX, frame.TileIndexY);
                }

                // Botones de control (solo si está seleccionado)
                if (isSelected)
                {
                    ImGui.SetCursorPos(new Vector2(startPos.X + 10, startPos.Y + 55));

                    if (ImGui.Button("Move Up", new Vector2(70, 25)))
                    {
                        MoveFrameUp(i);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Move frame up");

                    ImGui.SameLine();

                    if (ImGui.Button("Move Down", new Vector2(70, 25)))
                    {
                        MoveFrameDown(i);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Move frame down");

                    ImGui.SameLine();

                    if (ImGui.Button("Delete", new Vector2(60, 25)))
                    {
                        DeleteFrame(i);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Delete frame");
                }

                // Avanza al siguiente frame
                ImGui.SetCursorPos(new Vector2(startPos.X, startPos.Y + frameHeight));
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.PopID();
            }

            if (_selectedClip.Frames.Count == 0)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("No frames in this animation.");
                ImGui.TextDisabled("Click 'Add Frame from Grid' to start!");
            }

            ImGui.EndChild();
        }

        private void DrawGridPicker()
        {
            ImGui.SetNextWindowSize(new Vector2(600, 700), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            if (!ImGui.Begin("Pick a Tile", ref open, ImGuiWindowFlags.NoCollapse))
            {
                ImGui.End();
                return;
            }

            if (!open)
            {
                _showGridPicker = false;
                ImGui.End();
                return;
            }

            if (_selectedSprite == null || _selectedClip == null)
            {
                _showGridPicker = false;
                ImGui.End();
                return;
            }

            ImGui.Text($"Sprite Grid: {_selectedSprite.TilesPerRow} x {_selectedSprite.TilesPerColumn}");
            ImGui.Text($"Tile Size: {_selectedSprite.TileWidth} x {_selectedSprite.TileHeight} px");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.BeginChild("GridView", new Vector2(0, -40));

            float cellSize = 60f;
            float spacing = 4f;

            _hoveredTileX = -1;
            _hoveredTileY = -1;

            IntPtr textureId = IntPtr.Zero;
            bool hasTexture = false;
            float texWidth = 1f;
            float texHeight = 1f;
            float tileWidth = _selectedSprite.TileWidth;
            float tileHeight = _selectedSprite.TileHeight;

            try
            {
                if (_selectedSprite.Material.AlbedoTexture != null)
                {
                    textureId = _selectedSprite.Material.AlbedoTexture.TextureId;
                    if (textureId != IntPtr.Zero)
                    {
                        hasTexture = true;
                        texWidth = _selectedSprite.TextureWidth;
                        texHeight = _selectedSprite.TextureHeight;
                    }
                }
            }
            catch
            {
                hasTexture = false;
            }

            for (int y = 0; y < _selectedSprite.TilesPerColumn; y++)
            {
                for (int x = 0; x < _selectedSprite.TilesPerRow; x++)
                {
                    if (x > 0)
                        ImGui.SameLine(0, spacing);

                    ImGui.PushID(y * 1000 + x);

                    ImGui.BeginGroup();

                    if (hasTexture)
                    {
                        // Calcular UVs con inversión vertical (intercambiar v0 y v1)
                        float u0 = (x * tileWidth) / texWidth;
                        float v0 = ((y + 1) * tileHeight) / texHeight;
                        float u1 = ((x + 1) * tileWidth) / texWidth;
                        float v1 = (y * tileHeight) / texHeight;

                        ImGui.Image(textureId, new Vector2(cellSize, cellSize),
                            new Vector2(u0, v0), new Vector2(u1, v1));
                    }
                    else
                    {
                        ImGui.Button($"{x},{y}", new Vector2(cellSize, cellSize));
                    }

                    ImGui.EndGroup();

                    if (ImGui.IsItemHovered())
                    {
                        _hoveredTileX = x;
                        _hoveredTileY = y;
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                        ImGui.BeginTooltip();
                        ImGui.Text($"Tile [{x}, {y}]");
                        ImGui.EndTooltip();
                    }

                    if (ImGui.IsItemClicked())
                    {
                        AddFrameToClip(x, y);
                        _showGridPicker = false;
                    }

                    ImGui.PopID();
                }
            }

            ImGui.EndChild();

            ImGui.Spacing();
            if (_hoveredTileX >= 0 && _hoveredTileY >= 0)
            {
                if (ImGui.Button($"Add Tile [{_hoveredTileX}, {_hoveredTileY}]", new Vector2(-1, 30)))
                {
                    AddFrameToClip(_hoveredTileX, _hoveredTileY);
                    _showGridPicker = false;
                }
            }
            else
            {
                ImGui.BeginDisabled();
                ImGui.Button("Hover over a tile to select", new Vector2(-1, 30));
                ImGui.EndDisabled();
            }

            ImGui.End();
        }

        private void DrawEmptyState()
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 250);

            var text1 = "No sprite selected";
            var size1 = ImGui.CalcTextSize(text1);
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - size1.X) * 0.5f);
            ImGui.TextDisabled(text1);

            ImGui.Spacing();

            var text2 = "Select a sprite from the left panel to get started";
            var size2 = ImGui.CalcTextSize(text2);
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - size2.X) * 0.5f);
            ImGui.TextDisabled(text2);
        }

        private void SelectSprite(SpriteRenderer sprite)
        {
            _selectedSprite = sprite;
            _selectedClip = sprite.Animations.Count > 0 ? sprite.Animations[0] : null;
            _selectedFrameIndex = -1;
            _isPreviewPlaying = false;
            _previewFrameIndex = 0;
        }

        private void SelectClip(SpriteClip clip)
        {
            _selectedClip = clip;
            _selectedFrameIndex = -1;
            _isPreviewPlaying = false;
            _previewFrameIndex = 0;
        }

        private void CreateNewAnimation()
        {
            if (string.IsNullOrWhiteSpace(_newClipName)) return;

            var existingClip = _selectedSprite.GetAnimation(_newClipName);
            if (existingClip == null)
            {
                var newClip = _selectedSprite.AddAnimation(_newClipName);
                newClip.FrameRate = 12f;
                newClip.Loop = true;
                SelectClip(newClip);
                _newClipName = "";
            }
        }

        private void AddFrameToClip(int tileX, int tileY)
        {
            if (_selectedClip == null) return;

            _selectedClip.AddFrame(tileX, tileY);
            _selectedFrameIndex = _selectedClip.Frames.Count - 1;
        }

        private void MoveFrameUp(int index)
        {
            if (index > 0 && index < _selectedClip.Frames.Count)
            {
                var temp = _selectedClip.Frames[index];
                _selectedClip.Frames[index] = _selectedClip.Frames[index - 1];
                _selectedClip.Frames[index - 1] = temp;
                _selectedFrameIndex = index - 1;
            }
        }

        private void MoveFrameDown(int index)
        {
            if (index >= 0 && index < _selectedClip.Frames.Count - 1)
            {
                var temp = _selectedClip.Frames[index];
                _selectedClip.Frames[index] = _selectedClip.Frames[index + 1];
                _selectedClip.Frames[index + 1] = temp;
                _selectedFrameIndex = index + 1;
            }
        }

        private void DeleteFrame(int index)
        {
            if (index >= 0 && index < _selectedClip.Frames.Count)
            {
                _selectedClip.Frames.RemoveAt(index);
                _selectedFrameIndex = -1;
            }
        }
    }
}