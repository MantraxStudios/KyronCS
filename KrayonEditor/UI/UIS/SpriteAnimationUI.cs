using ImGuiNET;
using KrayonCore;
using KrayonCore.Components.RenderComponents;
using KrayonCore.GraphicsData;
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

        // Drag & Drop para reordenar frames
        private int _dragSourceFrame = -1;
        private bool _isDraggingFrame = false;

        // Cache de textura
        private IntPtr _cachedTextureId = IntPtr.Zero;
        private bool _hasCachedTexture = false;
        private float _cachedTexWidth = 1f;
        private float _cachedTexHeight = 1f;

        // NUEVO: Para editar el material de la animación
        private string _editingMaterialPath = "";
        private Material _cachedAnimationMaterial = null;
        private int _cachedTextureTilesX = 0;
        private int _cachedTextureTilesY = 0;

        // Colores del tema
        private static readonly Vector4 ColAccent = new Vector4(0.30f, 0.65f, 1.00f, 1.00f);
        private static readonly Vector4 ColAccentDim = new Vector4(0.30f, 0.65f, 1.00f, 0.25f);
        private static readonly Vector4 ColSuccess = new Vector4(0.25f, 0.85f, 0.45f, 1.00f);
        private static readonly Vector4 ColDanger = new Vector4(0.90f, 0.30f, 0.30f, 1.00f);
        private static readonly Vector4 ColDangerDim = new Vector4(0.90f, 0.30f, 0.30f, 0.20f);
        private static readonly Vector4 ColWarning = new Vector4(1.00f, 0.75f, 0.20f, 1.00f);
        private static readonly Vector4 ColWarningDim = new Vector4(1.00f, 0.75f, 0.20f, 0.25f);
        private static readonly Vector4 ColHeader = new Vector4(0.75f, 0.88f, 1.00f, 1.00f);
        private static readonly Vector4 ColMuted = new Vector4(0.50f, 0.55f, 0.65f, 1.00f);
        private static readonly Vector4 ColBg = new Vector4(0.10f, 0.11f, 0.14f, 1.00f);
        private static readonly Vector4 ColBgPanel = new Vector4(0.13f, 0.14f, 0.18f, 1.00f);
        private static readonly Vector4 ColBorder = new Vector4(0.22f, 0.24f, 0.30f, 1.00f);

        // --- Utilidades de color ---
        private uint C(Vector4 v) => ImGui.GetColorU32(v);

        private void SectionLabel(string text)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ColHeader);
            ImGui.TextUnformatted(text);
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Separator, ColBorder);
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }

        private bool SmallButton(string label, Vector4 color, float width = 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, color with { W = 0.18f });
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color with { W = 0.35f });
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, color with { W = 0.55f });
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            bool result = ImGui.Button(label, width > 0 ? new Vector2(width, 26) : new Vector2(0, 26));
            ImGui.PopStyleColor(4);
            return result;
        }

        // --- Cache de textura ---
        private void RefreshTextureCache()
        {
            _hasCachedTexture = false;
            _cachedTextureId = IntPtr.Zero;
            _cachedTexWidth = 1f;
            _cachedTexHeight = 1f;
            _cachedAnimationMaterial = null;
            _cachedTextureTilesX = 0;
            _cachedTextureTilesY = 0;

            if (_selectedSprite == null) return;

            Material materialToUse = null;

            // NUEVO: Cargar el material de la animación si está configurado
            if (_selectedClip != null && !string.IsNullOrEmpty(_selectedClip.MaterialPath))
            {
                try
                {
                    _cachedAnimationMaterial = GraphicsEngine.Instance.Materials.Get(_selectedClip.MaterialPath);
                    if (_cachedAnimationMaterial != null)
                    {
                        Console.WriteLine($"[SpriteAnimationUI] ✓ Material de animación cargado: {_selectedClip.MaterialPath}");
                        materialToUse = _cachedAnimationMaterial;
                    }
                    else
                    {
                        Console.WriteLine($"[SpriteAnimationUI] ✗ No se pudo cargar material: {_selectedClip.MaterialPath}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SpriteAnimationUI] ✗ Error cargando material: {ex.Message}");
                }
            }

            // Si no hay material de animación, usar el material base del sprite
            if (materialToUse == null)
            {
                materialToUse = _selectedSprite.Material;
            }

            if (materialToUse == null) return;

            try
            {
                if (materialToUse.AlbedoTexture != null)
                {
                    var id = materialToUse.AlbedoTexture.TextureId;
                    if (id != IntPtr.Zero)
                    {
                        _cachedTextureId = id;
                        _hasCachedTexture = true;
                        _cachedTexWidth = materialToUse.AlbedoTexture.Width;
                        _cachedTexHeight = materialToUse.AlbedoTexture.Height;

                        // Calcular tiles basado en el material actual
                        _cachedTextureTilesX = (int)_cachedTexWidth / _selectedSprite.TileWidth;
                        _cachedTextureTilesY = (int)_cachedTexHeight / _selectedSprite.TileHeight;

                        Console.WriteLine($"[SpriteAnimationUI] ✓ Textura cacheada: {_cachedTexWidth}x{_cachedTexHeight}px, Grid: {_cachedTextureTilesX}x{_cachedTextureTilesY}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpriteAnimationUI] ✗ Error al cachear textura: {ex.Message}");
            }
        }

        private (float u0, float v0, float u1, float v1) GetTileUV(int tx, int ty)
        {
            float tw = _selectedSprite.TileWidth;
            float th = _selectedSprite.TileHeight;
            float u0 = (tx * tw) / _cachedTexWidth;
            float v0 = ((ty + 1) * th) / _cachedTexHeight;  // invertido Y
            float u1 = ((tx + 1) * tw) / _cachedTexWidth;
            float v1 = (ty * th) / _cachedTexHeight;
            return (u0, v0, u1, v1);
        }

        // --- Punto de entrada ---
        public override void OnDrawUI()
        {
            if (!_isVisible)
                return;

            if (_selectedSprite != null && !IsSpriteValid())
                ClearSelection();

            ImGui.SetNextWindowSize(new Vector2(1280, 740), ImGuiCond.FirstUseEver);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, ColBg);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.Begin("Animation Editor", ref _isVisible, ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoScrollbar);
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();

            DrawMenuBar();

            // Layout de 3 columnas
            var avail = ImGui.GetContentRegionAvail();
            const float LEFT = 200f;
            const float MIDDLE = 280f;

            // Columna izquierda: sprites
            ImGui.PushStyleColor(ImGuiCol.ChildBg, ColBgPanel);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 12));
            ImGui.BeginChild("SpriteSelector", new Vector2(LEFT, avail.Y));
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
            DrawSpriteSelector();
            ImGui.EndChild();

            ImGui.SameLine(0, 1);

            // Linea divisoria
            var p = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(p, new Vector2(p.X + 1, p.Y + avail.Y), C(ColBorder));
            ImGui.SetCursorScreenPos(new Vector2(p.X + 1, p.Y));

            if (_selectedSprite != null)
            {
                // Columna central: animaciones
                ImGui.PushStyleColor(ImGuiCol.ChildBg, ColBgPanel);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 12));
                ImGui.BeginChild("AnimPanel", new Vector2(MIDDLE, avail.Y));
                ImGui.PopStyleVar();
                ImGui.PopStyleColor();
                DrawAnimationPanel();
                ImGui.EndChild();

                ImGui.SameLine(0, 1);
                p = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddRectFilled(p, new Vector2(p.X + 1, p.Y + avail.Y), C(ColBorder));
                ImGui.SetCursorScreenPos(new Vector2(p.X + 1, p.Y));

                // Columna derecha: timeline
                float rightW = avail.X - LEFT - MIDDLE - 3;
                ImGui.PushStyleColor(ImGuiCol.ChildBg, ColBg);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12));
                ImGui.BeginChild("TimelinePanel", new Vector2(rightW, avail.Y));
                ImGui.PopStyleVar();
                ImGui.PopStyleColor();
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
                DrawGridPicker();
        }

        // --- Menu ---
        private void DrawMenuBar()
        {
            if (ImGui.BeginMenuBar())
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ColAccent);
                ImGui.Text("🎬 Animation Editor");
                ImGui.PopStyleColor();
                ImGui.Separator();

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
                    if (ImGui.MenuItem("Multi-Material Setup")) { }
                    ImGui.EndMenu();
                }
                ImGui.EndMenuBar();
            }
        }

        // --- Panel izquierdo: lista de sprites ---
        private void DrawSpriteSelector()
        {
            SectionLabel("SPRITES");

            var scene = SceneManager.ActiveScene;
            if (scene == null)
            {
                ImGui.TextColored(ColMuted, "No active scene loaded");
                return;
            }

            var gameObjects = scene.GetAllGameObjects();
            bool found = false;

            foreach (var go in gameObjects)
            {
                var sprite = go.GetComponent<SpriteRenderer>();
                if (sprite == null) continue;
                found = true;

                bool isSelected = _selectedSprite == sprite;
                var dl = ImGui.GetWindowDrawList();
                var pos = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X;

                // Fondo del item
                if (isSelected)
                    dl.AddRectFilled(pos, new Vector2(pos.X + w, pos.Y + 52), C(ColAccentDim), 6f);

                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ColAccentDim);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, ColAccentDim);
                if (ImGui.Selectable($"##spr{go.GetHashCode()}", isSelected, ImGuiSelectableFlags.None, new Vector2(w, 52)))
                    SelectSprite(sprite);
                ImGui.PopStyleColor(2);

                // Contenido encima del selectable
                ImGui.SetCursorScreenPos(new Vector2(pos.X + 10, pos.Y + 8));
                ImGui.TextColored(isSelected ? ColAccent : new Vector4(0.9f, 0.9f, 0.95f, 1f), go.Name);

                ImGui.SetCursorScreenPos(new Vector2(pos.X + 10, pos.Y + 28));
                ImGui.TextColored(ColMuted, $"{sprite.TilesPerRow}x{sprite.TilesPerColumn} - {sprite.Animations.Count} anims");

                // Acento lateral si esta seleccionado
                if (isSelected)
                    dl.AddRectFilled(pos, new Vector2(pos.X + 3, pos.Y + 52), C(ColAccent), 2f);

                ImGui.Spacing();
            }

            if (!found)
            {
                ImGui.Spacing();
                ImGui.TextColored(ColMuted, "No sprites in scene.");
                ImGui.Spacing();
                ImGui.TextWrapped("Add a SpriteRenderer to a GameObject to get started.");
            }
        }

        // --- Panel central: clips de animacion ---
        private void DrawAnimationPanel()
        {
            SectionLabel("PLAYBACK");
            DrawQuickActions();

            ImGui.Spacing();
            SectionLabel("ANIMATIONS");

            // Lista de clips
            ImGui.BeginChild("ClipList", new Vector2(0, 180));
            for (int i = 0; i < _selectedSprite.Animations.Count; i++)
            {
                var clip = _selectedSprite.Animations[i];
                bool isSel = _selectedClip == clip;

                var pos = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X;
                var dl = ImGui.GetWindowDrawList();

                if (isSel)
                    dl.AddRectFilled(pos, new Vector2(pos.X + w, pos.Y + 46), C(ColAccentDim), 5f);

                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ColAccentDim);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, ColAccentDim);
                if (ImGui.Selectable($"##clip{i}", isSel, ImGuiSelectableFlags.None, new Vector2(w, 46)))
                    SelectClip(clip);
                ImGui.PopStyleColor(2);

                // Icono + nombre
                ImGui.SetCursorScreenPos(new Vector2(pos.X + 10, pos.Y + 4));
                string icon = clip.Loop ? "[Loop]" : "[Once]";
                ImGui.TextColored(isSel ? ColAccent : ColMuted, icon);
                ImGui.SameLine();
                ImGui.TextColored(isSel ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.8f, 0.8f, 0.9f, 1f), clip.Name);

                // NUEVO: Indicador de material personalizado
                if (!string.IsNullOrEmpty(clip.MaterialPath))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ColWarning, "🎨");
                }

                // Metadata
                ImGui.SetCursorScreenPos(new Vector2(pos.X + 10, pos.Y + 24));
                ImGui.TextColored(ColMuted, $"{clip.Frames.Count} frames - {clip.FrameRate:F0} fps");

                if (isSel)
                    dl.AddRectFilled(pos, new Vector2(pos.X + 3, pos.Y + 46), C(ColAccent), 2f);

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextColored(ColAccent, clip.Name);
                    ImGui.TextColored(ColMuted, $"{clip.Frames.Count} frames @ {clip.FrameRate:F1} fps - {(clip.Loop ? "Loop" : "One-shot")}");
                    if (!string.IsNullOrEmpty(clip.MaterialPath))
                    {
                        ImGui.TextColored(ColWarning, $"📁 Custom material: {clip.MaterialPath}");
                    }
                    ImGui.EndTooltip();
                }
            }
            ImGui.EndChild();

            ImGui.Spacing();

            // Crear nueva animacion
            ImGui.SetNextItemWidth(-1);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.18f, 0.20f, 0.26f, 1f));
            ImGui.InputTextWithHint("##NewName", "New animation name...", ref _newClipName, 128);
            ImGui.PopStyleColor();

            ImGui.Spacing();
            bool canCreate = !string.IsNullOrWhiteSpace(_newClipName);
            if (!canCreate) ImGui.BeginDisabled();
            if (SmallButton("+ Create Animation", ColAccent, -1))
                CreateNewAnimation();
            if (!canCreate) ImGui.EndDisabled();

            // Settings del clip seleccionado
            if (_selectedClip != null)
            {
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Separator, ColBorder);
                ImGui.Separator();
                ImGui.PopStyleColor();
                ImGui.Spacing();
                DrawAnimationSettings();
            }
        }

        private void DrawQuickActions()
        {
            // Playing toggle
            bool isPlaying = _selectedSprite.IsPlaying;
            ImGui.PushStyleColor(ImGuiCol.CheckMark, ColSuccess);
            if (ImGui.Checkbox("Playing in scene", ref isPlaying))
                _selectedSprite.IsPlaying = isPlaying;
            ImGui.PopStyleColor();

            ImGui.Spacing();

            // Speed slider
            float speed = _selectedSprite.AnimationSpeed;
            ImGui.TextColored(ColMuted, "Speed");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.PushStyleColor(ImGuiCol.SliderGrab, ColAccent);
            if (ImGui.SliderFloat("##Speed", ref speed, 0.1f, 3.0f, "%.1fx"))
                _selectedSprite.AnimationSpeed = speed;
            ImGui.PopStyleColor();
        }

        private void DrawAnimationSettings()
        {
            ImGui.TextColored(ColHeader, $"⚙  {_selectedClip.Name}");
            ImGui.Spacing();

            // Nombre
            string name = _selectedClip.Name;
            ImGui.TextColored(ColMuted, "Name");
            ImGui.SetNextItemWidth(-1);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.18f, 0.20f, 0.26f, 1f));
            if (ImGui.InputText("##ClipName", ref name, 256))
                _selectedClip.Name = name;
            ImGui.PopStyleColor();

            ImGui.Spacing();

            // NUEVO: Material Path
            ImGui.TextColored(ColMuted, "Material (optional)");
            string matPath = _selectedClip.MaterialPath ?? "";
            ImGui.SetNextItemWidth(-1);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.18f, 0.20f, 0.26f, 1f));
            if (ImGui.InputTextWithHint("##MatPath", "Leave empty to use sprite's base material", ref matPath, 512))
            {
                _selectedClip.MaterialPath = matPath;
                RefreshTextureCache(); // NUEVO: Refrescar cache al cambiar material
            }
            ImGui.PopStyleColor();

            if (!string.IsNullOrEmpty(_selectedClip.MaterialPath))
            {
                ImGui.SameLine();
                if (SmallButton("Clear", ColWarning, 60))
                {
                    _selectedClip.MaterialPath = "";
                    RefreshTextureCache(); // NUEVO: Refrescar cache al limpiar material
                }
            }

            ImGui.Spacing();
            if (!string.IsNullOrEmpty(_selectedClip.MaterialPath))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ColWarning);
                ImGui.TextWrapped($"🎨 This animation will use: {_selectedClip.MaterialPath}");
                ImGui.PopStyleColor();
                ImGui.Spacing();
            }
            else
            {
                ImGui.TextColored(ColMuted, "Using sprite's base material");
                ImGui.Spacing();
            }

            // FPS
            float fps = _selectedClip.FrameRate;
            ImGui.TextColored(ColMuted, "Frame Rate");
            ImGui.SetNextItemWidth(-1);
            ImGui.PushStyleColor(ImGuiCol.SliderGrab, ColAccent);
            if (ImGui.SliderFloat("##FPS", ref fps, 1f, 60f, "%.0f fps"))
                _selectedClip.FrameRate = fps;
            ImGui.PopStyleColor();

            ImGui.Spacing();

            // Loop
            bool loop = _selectedClip.Loop;
            ImGui.PushStyleColor(ImGuiCol.CheckMark, ColAccent);
            if (ImGui.Checkbox("Loop", ref loop))
                _selectedClip.Loop = loop;
            ImGui.PopStyleColor();

            ImGui.Spacing();

            // Acciones
            if (SmallButton("▶  Play This Animation", ColSuccess, -1))
                _selectedSprite.Play(_selectedClip.Name);

            ImGui.Spacing();

            bool canDelete = _selectedSprite.Animations.Count > 1;
            if (!canDelete) ImGui.BeginDisabled();
            if (SmallButton("🗑  Delete Animation", ColDanger, -1))
            {
                _selectedSprite.RemoveAnimation(_selectedClip.Name);
                SelectClip(null);
            }
            if (!canDelete)
            {
                ImGui.EndDisabled();
                ImGui.TextColored(ColMuted, "  Keep at least 1 animation");
            }
        }

        // --- Panel derecho: preview + timeline ---
        private void DrawTimelinePanel()
        {
            if (_selectedClip == null)
            {
                DrawTimelinePlaceholder("Select an animation to edit its frames");
                return;
            }

            SectionLabel("PREVIEW");
            DrawPreviewControls();

            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Separator, ColBorder);
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();

            SectionLabel("FRAMES");
            DrawTimeline();
        }

        private void DrawTimelinePlaceholder(string msg)
        {
            var avail = ImGui.GetContentRegionAvail();
            var sz = ImGui.CalcTextSize(msg);
            ImGui.SetCursorPos(new Vector2((avail.X - sz.X) * 0.5f, (avail.Y - sz.Y) * 0.5f));
            ImGui.TextColored(ColMuted, msg);
        }

        // --- Controles de preview ---
        private void DrawPreviewControls()
        {
            if (_selectedClip.Frames.Count == 0)
            {
                ImGui.TextColored(ColMuted, "No frames yet - add frames in the timeline below.");
                return;
            }

            // Fila de botones + contador
            string playLabel = _isPreviewPlaying ? "⏸ Pause" : "▶ Play";
            if (SmallButton(playLabel, ColAccent, 90))
            {
                _isPreviewPlaying = !_isPreviewPlaying;
                if (_isPreviewPlaying)
                {
                    _previewTimer = 0f;
                    _previewFrameIndex = Math.Max(0, Math.Min(_previewFrameIndex, _selectedClip.Frames.Count - 1));
                }
            }

            ImGui.SameLine();
            if (SmallButton("⏹ Stop", ColMuted, 80))
            {
                _isPreviewPlaying = false;
                _previewFrameIndex = 0;
                _previewTimer = 0f;
                if (_selectedClip.Frames.Count > 0)
                {
                    var f = _selectedClip.Frames[0];
                    _selectedSprite.SetTile(f.TileIndexX, f.TileIndexY);
                }
            }

            ImGui.SameLine();
            ImGui.TextColored(ColMuted, $"   Frame {_previewFrameIndex + 1} / {_selectedClip.Frames.Count}");
            ImGui.SameLine();
            float dur = _selectedClip.Frames.Count / _selectedClip.FrameRate;
            ImGui.TextColored(ColMuted, $"  {dur:F2}s @ {_selectedClip.FrameRate:F0} fps");

            // Progress bar
            ImGui.Spacing();
            float progress = _selectedClip.Frames.Count > 1
                ? (float)_previewFrameIndex / (_selectedClip.Frames.Count - 1)
                : 0f;
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, ColAccent);
            ImGui.ProgressBar(progress, new Vector2(-1, 6));
            ImGui.PopStyleColor();

            // Scrubber
            int scrub = _previewFrameIndex;
            ImGui.SetNextItemWidth(-1);
            ImGui.PushStyleColor(ImGuiCol.SliderGrab, ColAccent);
            if (ImGui.SliderInt("##Scrub", ref scrub, 0, _selectedClip.Frames.Count - 1, "Frame %d"))
            {
                _previewFrameIndex = scrub;
                _isPreviewPlaying = false;
                if (_previewFrameIndex >= 0 && _previewFrameIndex < _selectedClip.Frames.Count)
                {
                    var f = _selectedClip.Frames[_previewFrameIndex];
                    _selectedSprite.SetTile(f.TileIndexX, f.TileIndexY);
                }
            }
            ImGui.PopStyleColor();

            // Preview visual
            ImGui.Spacing();
            DrawVisualPreview();

            if (_isPreviewPlaying)
                UpdatePreviewAnimation();
        }

        private void DrawVisualPreview()
        {
            if (_selectedClip.Frames.Count == 0) return;
            if (_previewFrameIndex < 0 || _previewFrameIndex >= _selectedClip.Frames.Count) return;

            var frame = _selectedClip.Frames[_previewFrameIndex];
            var dl = ImGui.GetWindowDrawList();
            var avail = ImGui.GetContentRegionAvail();

            const float SZ = 96f;
            var cursorPos = ImGui.GetCursorScreenPos();
            float cx = cursorPos.X + (avail.X - SZ) * 0.5f;
            float cy = cursorPos.Y;

            var min = new Vector2(cx, cy);
            var max = new Vector2(cx + SZ, cy + SZ);

            // Fondo
            dl.AddRectFilled(min, max, C(ColBgPanel), 8f);

            if (_hasCachedTexture)
            {
                var (u0, v0, u1, v1) = GetTileUV(frame.TileIndexX, frame.TileIndexY);
                dl.AddImage(_cachedTextureId, min, max, new Vector2(u0, v0), new Vector2(u1, v1));
            }
            else
            {
                string t = $"[{frame.TileIndexX},{frame.TileIndexY}]";
                var ts = ImGui.CalcTextSize(t);
                dl.AddText(new Vector2(cx + (SZ - ts.X) * 0.5f, cy + (SZ - ts.Y) * 0.5f), C(ColMuted), t);
            }

            // Borde
            dl.AddRect(min, max, C(ColBorder), 8f, ImDrawFlags.None, 1.5f);

            // Indicador de reproduccion
            if (_isPreviewPlaying)
                dl.AddCircleFilled(new Vector2(cx + SZ - 10, cy + 10), 5f, C(ColSuccess));

            // Avanzar cursor
            ImGui.SetCursorScreenPos(new Vector2(cursorPos.X, cy + SZ + 4));

            string frameLabel = $"Frame {_previewFrameIndex + 1} / {_selectedClip.Frames.Count}";
            var fsz = ImGui.CalcTextSize(frameLabel);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail.X - fsz.X) * 0.5f);
            ImGui.TextColored(ColMuted, frameLabel);
            ImGui.Spacing();
        }

        private void UpdatePreviewAnimation()
        {
            _previewTimer += ImGui.GetIO().DeltaTime;
            float frameDur = 1f / _selectedClip.FrameRate;

            while (_previewTimer >= frameDur)
            {
                _previewTimer -= frameDur;
                _previewFrameIndex++;

                if (_previewFrameIndex >= _selectedClip.Frames.Count)
                {
                    if (_selectedClip.Loop)
                        _previewFrameIndex = 0;
                    else
                    {
                        _previewFrameIndex = _selectedClip.Frames.Count - 1;
                        _isPreviewPlaying = false;
                        break;
                    }
                }
            }

            if (_previewFrameIndex >= 0 && _previewFrameIndex < _selectedClip.Frames.Count)
            {
                var f = _selectedClip.Frames[_previewFrameIndex];
                _selectedSprite.SetTile(f.TileIndexX, f.TileIndexY);
            }
        }

        // --- Timeline de frames (horizontal con drag&drop) ---
        private void DrawTimeline()
        {
            // NUEVO: Mostrar qué material se está usando
            if (!string.IsNullOrEmpty(_selectedClip.MaterialPath))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ColWarning);
                ImGui.TextUnformatted($"🎨 Using: {_selectedClip.MaterialPath}");
                ImGui.PopStyleColor();
                ImGui.Spacing();
            }

            if (SmallButton("+ Add Frame from Grid", ColAccent, -1))
            {
                RefreshTextureCache();
                _showGridPicker = true;
            }

            ImGui.Spacing();

            if (_selectedClip.Frames.Count == 0)
            {
                ImGui.TextColored(ColMuted, "No frames yet. Click the button above to add some!");
                return;
            }

            ImGui.TextColored(ColMuted, $"{_selectedClip.Frames.Count} frame(s) - drag to reorder");
            ImGui.Spacing();

            // Scroll horizontal
            ImGui.BeginChild("FramesScroll", new Vector2(0, 0));

            const float THUMB = 72f;
            const float CARD_W = 88f;
            const float CARD_H = 150f;
            const float GAP = 8f;

            RefreshTextureCache();

            int frameToDelete = -1;
            int dragTarget = -1;

            for (int i = 0; i < _selectedClip.Frames.Count; i++)
            {
                ImGui.PushID(i);

                var frame = _selectedClip.Frames[i];
                bool isSel = _selectedFrameIndex == i;
                bool isPreview = _previewFrameIndex == i;

                var cardMin = ImGui.GetCursorScreenPos();
                var cardMax = new Vector2(cardMin.X + CARD_W, cardMin.Y + CARD_H);
                var dl = ImGui.GetWindowDrawList();

                // Fondo de la tarjeta
                uint bgCol = isSel ? C(ColAccentDim)
                           : isPreview ? C(new Vector4(0.25f, 0.8f, 0.4f, 0.18f))
                                       : C(ColBgPanel);
                dl.AddRectFilled(cardMin, cardMax, bgCol, 8f);

                // Borde
                uint borderCol = isSel ? C(ColAccent)
                               : isPreview ? C(ColSuccess)
                                           : C(ColBorder);
                dl.AddRect(cardMin, cardMax, borderCol, 8f, ImDrawFlags.None, isSel ? 2f : 1f);

                // Thumbnail del tile
                var thumbMin = new Vector2(cardMin.X + (CARD_W - THUMB) * 0.5f, cardMin.Y + 8f);
                var thumbMax = new Vector2(thumbMin.X + THUMB, thumbMin.Y + THUMB);

                if (_hasCachedTexture)
                {
                    var (u0, v0, u1, v1) = GetTileUV(frame.TileIndexX, frame.TileIndexY);
                    dl.AddRectFilled(thumbMin, thumbMax, C(ColBg), 4f);
                    dl.AddImage(_cachedTextureId, thumbMin, thumbMax, new Vector2(u0, v0), new Vector2(u1, v1));
                    dl.AddRect(thumbMin, thumbMax, C(ColBorder), 4f, ImDrawFlags.None, 1f);
                }
                else
                {
                    dl.AddRectFilled(thumbMin, thumbMax, C(ColBg), 4f);
                    string t = $"{frame.TileIndexX},{frame.TileIndexY}";
                    var ts = ImGui.CalcTextSize(t);
                    dl.AddText(new Vector2(thumbMin.X + (THUMB - ts.X) * 0.5f, thumbMin.Y + (THUMB - ts.Y) * 0.5f),
                               C(ColMuted), t);
                    dl.AddRect(thumbMin, thumbMax, C(ColBorder), 4f, ImDrawFlags.None, 1f);
                }

                // Numero de frame (badge)
                string num = $"{i + 1}";
                var ns = ImGui.CalcTextSize(num);
                dl.AddRectFilled(new Vector2(cardMin.X + 4, cardMin.Y + 4),
                                 new Vector2(cardMin.X + 4 + ns.X + 6, cardMin.Y + 4 + ns.Y + 2),
                                 C(new Vector4(0.1f, 0.1f, 0.15f, 0.85f)), 3f);
                dl.AddText(new Vector2(cardMin.X + 7, cardMin.Y + 5), C(ColMuted), num);

                // Tile label
                string tileLabel = $"[{frame.TileIndexX},{frame.TileIndexY}]";
                var tlSz = ImGui.CalcTextSize(tileLabel);
                dl.AddText(new Vector2(cardMin.X + (CARD_W - tlSz.X) * 0.5f, thumbMin.Y + THUMB + 6),
                           C(ColMuted), tileLabel);

                // Botones < > dentro de la tarjeta (solo si esta seleccionado)
                if (isSel)
                {
                    float btnY = cardMin.Y + THUMB + 28f;
                    float btnW = (CARD_W - 20) * 0.5f;

                    // Move Left
                    ImGui.SetCursorScreenPos(new Vector2(cardMin.X + 8, btnY));
                    bool canLeft = i > 0;
                    if (!canLeft) ImGui.BeginDisabled();
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.3f, 0.5f, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.5f, 0.8f, 0.6f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.6f, 1.0f, 0.8f));
                    if (ImGui.Button("<", new Vector2(btnW, 24))) MoveFrameUp(i);
                    ImGui.PopStyleColor(3);
                    if (!canLeft) ImGui.EndDisabled();

                    ImGui.SameLine(0, 4);

                    // Move Right
                    bool canRight = i < _selectedClip.Frames.Count - 1;
                    if (!canRight) ImGui.BeginDisabled();
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.3f, 0.5f, 0.4f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.5f, 0.8f, 0.6f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.6f, 1.0f, 0.8f));
                    if (ImGui.Button(">", new Vector2(btnW, 24))) MoveFrameDown(i);
                    ImGui.PopStyleColor(3);
                    if (!canRight) ImGui.EndDisabled();

                    // Delete (centrado abajo)
                    float delW = CARD_W - 16f;
                    ImGui.SetCursorScreenPos(new Vector2(cardMin.X + 8, btnY + 30));
                    ImGui.PushStyleColor(ImGuiCol.Button, ColDangerDim);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 0.35f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.3f, 0.3f, 0.55f));
                    ImGui.PushStyleColor(ImGuiCol.Text, ColDanger);
                    if (ImGui.Button("Delete", new Vector2(delW, 22)))
                        frameToDelete = i;
                    ImGui.PopStyleColor(4);
                }

                // Area invisible de click (toda la tarjeta, para seleccion)
                ImGui.SetCursorScreenPos(cardMin);
                ImGui.InvisibleButton($"##card{i}", new Vector2(CARD_W, isSel ? CARD_H : THUMB + 24));

                if (ImGui.IsItemClicked())
                {
                    _selectedFrameIndex = i;
                    _selectedSprite.SetTile(frame.TileIndexX, frame.TileIndexY);
                }

                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    _previewFrameIndex = i;
                    _isPreviewPlaying = false;
                    _selectedSprite.SetTile(frame.TileIndexX, frame.TileIndexY);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    if (!isSel)
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextColored(ColAccent, $"Frame {i + 1}");
                        ImGui.TextColored(ColMuted, $"Tile [{frame.TileIndexX}, {frame.TileIndexY}]");
                        ImGui.TextColored(ColMuted, "Click to select - Double-click to preview");
                        ImGui.EndTooltip();
                    }
                }

                // Drag & Drop (reordenar)
                if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceAllowNullID))
                {
                    _dragSourceFrame = i;
                    ImGui.SetDragDropPayload("FRAME_REORDER", IntPtr.Zero, 0);
                    ImGui.TextColored(ColAccent, $"Moving Frame {i + 1}");
                    ImGui.EndDragDropSource();
                }

                if (ImGui.BeginDragDropTarget())
                {
                    unsafe
                    {
                        var payload = ImGui.AcceptDragDropPayload("FRAME_REORDER");
                        if (payload.NativePtr != null && _dragSourceFrame >= 0 && _dragSourceFrame != i)
                        {
                            // Mover el frame de _dragSourceFrame a i
                            var temp = _selectedClip.Frames[_dragSourceFrame];
                            _selectedClip.Frames.RemoveAt(_dragSourceFrame);
                            _selectedClip.Frames.Insert(i, temp);
                            _selectedFrameIndex = i;
                            _dragSourceFrame = -1;
                        }
                    }
                    ImGui.EndDragDropTarget();
                }

                // Siguiente tarjeta
                ImGui.SetCursorScreenPos(new Vector2(cardMax.X + GAP, cardMin.Y));
                ImGui.PopID();
            }

            // Dummy para que el child tenga altura
            ImGui.SetCursorScreenPos(new Vector2(
                ImGui.GetCursorScreenPos().X,
                ImGui.GetWindowPos().Y + 8 + CARD_H + 20));
            ImGui.Dummy(Vector2.Zero);

            ImGui.EndChild();

            // Aplicar eliminacion fuera del loop
            if (frameToDelete >= 0)
                DeleteFrame(frameToDelete);
        }

        // --- Grid Picker ---
        private void DrawGridPicker()
        {
            ImGui.SetNextWindowSize(new Vector2(640, 620), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool open = true;
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.10f, 0.11f, 0.14f, 0.98f));
            if (!ImGui.Begin("Pick a Tile", ref open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar))
            {
                ImGui.PopStyleColor();
                ImGui.End();
                return;
            }
            ImGui.PopStyleColor();

            if (!open) { _showGridPicker = false; ImGui.End(); return; }
            if (_selectedSprite == null || _selectedClip == null) { _showGridPicker = false; ImGui.End(); return; }

            // Determinar qué grid mostrar
            int tilesX = _cachedTextureTilesX > 0 ? _cachedTextureTilesX : _selectedSprite.TilesPerRow;
            int tilesY = _cachedTextureTilesY > 0 ? _cachedTextureTilesY : _selectedSprite.TilesPerColumn;

            // Header con información del material actual
            if (_cachedAnimationMaterial != null)
            {
                ImGui.TextColored(ColWarning, $"🎨 Animation Material: {_selectedClip.MaterialPath}");
                ImGui.TextColored(ColHeader, $"Sprite Grid - {tilesX} x {tilesY}");
            }
            else
            {
                ImGui.TextColored(ColHeader, $"Sprite Grid (Base Material) - {tilesX} x {tilesY}");
            }

            ImGui.TextColored(ColMuted, $"Tile size: {_selectedSprite.TileWidth} x {_selectedSprite.TileHeight} px - Click a tile to add it as a frame");
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Separator, ColBorder);
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();

            ImGui.BeginChild("GridArea", new Vector2(0, -50));

            // NUEVO: Verificar si no hay textura cargada
            if (!_hasCachedTexture)
            {
                var avail = ImGui.GetContentRegionAvail();
                string errorMsg = "⚠ No texture loaded";
                string detailMsg = _cachedAnimationMaterial == null && !string.IsNullOrEmpty(_selectedClip.MaterialPath)
                    ? $"Failed to load material: {_selectedClip.MaterialPath}"
                    : "Material has no texture";

                var sz1 = ImGui.CalcTextSize(errorMsg);
                var sz2 = ImGui.CalcTextSize(detailMsg);

                ImGui.SetCursorPos(new Vector2((avail.X - sz1.X) * 0.5f, avail.Y * 0.5f - 20));
                ImGui.TextColored(ColDanger, errorMsg);

                ImGui.SetCursorPos(new Vector2((avail.X - sz2.X) * 0.5f, avail.Y * 0.5f + 5));
                ImGui.TextColored(ColMuted, detailMsg);

                ImGui.EndChild();
                ImGui.End();
                return;
            }

            const float CELL = 64f;
            const float PAD = 5f;

            _hoveredTileX = -1;
            _hoveredTileY = -1;

            for (int y = 0; y < tilesY; y++)
            {
                for (int x = 0; x < tilesX; x++)
                {
                    if (x > 0) ImGui.SameLine(0, PAD);
                    ImGui.PushID(y * 1000 + x);

                    var cellMin = ImGui.GetCursorScreenPos();
                    var cellMax = new Vector2(cellMin.X + CELL, cellMin.Y + CELL);
                    var dl = ImGui.GetWindowDrawList();

                    // Fondo de la celda
                    dl.AddRectFilled(cellMin, cellMax, C(ColBgPanel), 5f);

                    if (_hasCachedTexture)
                    {
                        var (u0, v0, u1, v1) = GetTileUV(x, y);
                        dl.AddImage(_cachedTextureId, cellMin, cellMax, new Vector2(u0, v0), new Vector2(u1, v1));
                    }
                    else
                    {
                        string t = $"{x},{y}";
                        var ts = ImGui.CalcTextSize(t);
                        dl.AddText(new Vector2(cellMin.X + (CELL - ts.X) * 0.5f, cellMin.Y + (CELL - ts.Y) * 0.5f),
                                   C(ColMuted), t);
                    }

                    // Boton invisible
                    ImGui.InvisibleButton($"##t{x}_{y}", new Vector2(CELL, CELL));

                    if (ImGui.IsItemHovered())
                    {
                        _hoveredTileX = x;
                        _hoveredTileY = y;
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                        // Highlight hover
                        dl.AddRectFilled(cellMin, cellMax, C(ColAccentDim), 5f);
                        dl.AddRect(cellMin, cellMax, C(ColAccent), 5f, ImDrawFlags.None, 2f);

                        ImGui.BeginTooltip();
                        ImGui.TextColored(ColAccent, $"Tile [{x}, {y}]");
                        ImGui.TextColored(ColMuted, "Click to add as frame");
                        ImGui.EndTooltip();
                    }
                    else
                    {
                        dl.AddRect(cellMin, cellMax, C(ColBorder), 5f, ImDrawFlags.None, 1f);
                    }

                    if (ImGui.IsItemClicked())
                    {
                        AddFrameToClip(x, y);
                        _showGridPicker = false;
                    }

                    ImGui.PopID();
                }

                ImGui.Spacing();
            }

            ImGui.EndChild();

            // Barra de accion inferior
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Separator, ColBorder);
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();

            if (_hoveredTileX >= 0 && _hoveredTileY >= 0)
            {
                ImGui.TextColored(ColAccent, $"Selected: [{_hoveredTileX}, {_hoveredTileY}]");
                ImGui.SameLine();
                if (SmallButton($"+ Add Tile [{_hoveredTileX}, {_hoveredTileY}]", ColAccent, -1))
                {
                    AddFrameToClip(_hoveredTileX, _hoveredTileY);
                    _showGridPicker = false;
                }
            }
            else
            {
                ImGui.BeginDisabled();
                ImGui.Button("Hover over a tile to select", new Vector2(-1, 26));
                ImGui.EndDisabled();
            }

            ImGui.End();
        }

        // --- Estado vacio ---
        private void DrawEmptyState()
        {
            var avail = ImGui.GetContentRegionAvail();
            string line1 = "No sprite selected";
            string line2 = "Select a sprite from the left panel to get started";

            var s1 = ImGui.CalcTextSize(line1);
            var s2 = ImGui.CalcTextSize(line2);

            ImGui.SetCursorPos(new Vector2((avail.X - s1.X) * 0.5f, avail.Y * 0.5f - 20));
            ImGui.TextColored(new Vector4(0.6f, 0.65f, 0.75f, 1f), line1);

            ImGui.SetCursorPos(new Vector2((avail.X - s2.X) * 0.5f, avail.Y * 0.5f));
            ImGui.TextColored(ColMuted, line2);
        }

        // --- Helpers de seleccion ---
        private void SelectSprite(SpriteRenderer sprite)
        {
            _selectedSprite = sprite;
            _selectedClip = sprite.Animations.Count > 0 ? sprite.Animations[0] : null;
            _selectedFrameIndex = -1;
            _isPreviewPlaying = false;
            _previewFrameIndex = 0;
            RefreshTextureCache();
        }

        private void SelectClip(SpriteClip clip)
        {
            _selectedClip = clip;
            _selectedFrameIndex = -1;
            _isPreviewPlaying = false;
            _previewFrameIndex = 0;
            RefreshTextureCache(); // NUEVO: Refrescar cache al seleccionar clip
        }

        private void CreateNewAnimation()
        {
            if (string.IsNullOrWhiteSpace(_newClipName)) return;
            if (_selectedSprite.GetAnimation(_newClipName) == null)
            {
                var clip = _selectedSprite.AddAnimation(_newClipName);
                clip.FrameRate = 12f;
                clip.Loop = true;
                SelectClip(clip);
                _newClipName = "";
            }
        }

        private void AddFrameToClip(int tx, int ty)
        {
            if (_selectedClip == null) return;
            _selectedClip.AddFrame(tx, ty);
            _selectedFrameIndex = _selectedClip.Frames.Count - 1;
        }

        private void MoveFrameUp(int index)
        {
            if (index > 0 && index < _selectedClip.Frames.Count)
            {
                var tmp = _selectedClip.Frames[index];
                _selectedClip.Frames[index] = _selectedClip.Frames[index - 1];
                _selectedClip.Frames[index - 1] = tmp;
                _selectedFrameIndex = index - 1;
            }
        }

        private void MoveFrameDown(int index)
        {
            if (index >= 0 && index < _selectedClip.Frames.Count - 1)
            {
                var tmp = _selectedClip.Frames[index];
                _selectedClip.Frames[index] = _selectedClip.Frames[index + 1];
                _selectedClip.Frames[index + 1] = tmp;
                _selectedFrameIndex = index + 1;
            }
        }

        private void DeleteFrame(int index)
        {
            if (index >= 0 && index < _selectedClip.Frames.Count)
            {
                _selectedClip.Frames.RemoveAt(index);
                _selectedFrameIndex = -1;
                if (_previewFrameIndex >= _selectedClip.Frames.Count)
                    _previewFrameIndex = Math.Max(0, _selectedClip.Frames.Count - 1);
            }
        }

        private bool IsSpriteValid()
        {
            if (_selectedSprite == null) return false;
            var scene = SceneManager.ActiveScene;
            if (scene == null) return false;
            foreach (var go in scene.GetAllGameObjects())
                if (go.GetComponent<SpriteRenderer>() == _selectedSprite)
                    return true;
            return false;
        }

        private void ClearSelection()
        {
            _selectedSprite = null;
            _selectedClip = null;
            _selectedFrameIndex = -1;
            _isPreviewPlaying = false;
            _previewFrameIndex = 0;
            _previewTimer = 0f;
            _hasCachedTexture = false;
        }
    }
}