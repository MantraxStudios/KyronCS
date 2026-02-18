using KrayonCore.Core.Attributes;
using KrayonCore.Graphics.GameUI;
using OpenTK.Mathematics;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KrayonCore.Graphics.GameUI
{
    /// <summary>
    /// Loads a complete UICanvas hierarchy from a .ui / .uijson file via AssetManager.
    ///
    /// Usage:
    ///   var canvas = UILoader.Load("UI/MainMenu.ui", renderer);
    ///   canvas?.Find<UIButton>("BtnPlay")?.OnClick = () => ...;
    /// </summary>
    public static class UILoader
    {
        // ── Public API ────────────────────────────────────────────────────

        public static UICanvas? Load(Guid assetGuid, SceneRenderer renderer)
        {
            byte[]? bytes = AssetManager.GetBytes(assetGuid);
            if (bytes is null) { Console.WriteLine($"[UILoader] AssetManager returned null for GUID {assetGuid}"); return null; }
            return ParseAndBuild(bytes, renderer);
        }

        public static UICanvas? Load(string assetPath, SceneRenderer renderer)
        {
            byte[]? bytes = AssetManager.GetBytes(assetPath);
            if (bytes is null) { Console.WriteLine($"[UILoader] AssetManager returned null for path '{assetPath}'. Verify the file exists in BasePath."); return null; }
            return ParseAndBuild(bytes, renderer);
        }

        public static UICanvas? LoadDetached(Guid assetGuid)
        {
            byte[]? bytes = AssetManager.GetBytes(assetGuid);
            if (bytes is null) { Console.WriteLine($"[UILoader] AssetManager returned null for GUID {assetGuid}"); return null; }
            return ParseAndBuild(bytes, null);
        }

        public static UICanvas? LoadDetached(string assetPath)
        {
            byte[]? bytes = AssetManager.GetBytes(assetPath);
            if (bytes is null) { Console.WriteLine($"[UILoader] AssetManager returned null for path '{assetPath}'"); return null; }
            return ParseAndBuild(bytes, null);
        }

        // ── Core parser ───────────────────────────────────────────────────

        private static UICanvas? ParseAndBuild(byte[] bytes, SceneRenderer? renderer)
        {
            try
            {
                // Strip UTF-8 BOM if present
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                    bytes = bytes[3..];

                string json = Encoding.UTF8.GetString(bytes);

                var opts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                };

                var def = JsonSerializer.Deserialize<UICanvasDefinition>(json, opts);
                if (def is null)
                {
                    Console.WriteLine("[UILoader] JSON deserialized to null. Check the file format.");
                    return null;
                }

                return BuildCanvas(def, renderer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UILoader] Parse error: {ex.Message}");
                return null;
            }
        }

        private static UICanvas BuildCanvas(UICanvasDefinition def, SceneRenderer? renderer)
        {
            string canvasName = def.Name ?? "canvas";

            // Always start fresh: destroy any stale canvas with the same name.
            if (UICanvasManager.Has(canvasName))
            {
                Console.WriteLine($"[UILoader] Canvas '{canvasName}' already exists, destroying and recreating.");
                UICanvasManager.Destroy(canvasName);
            }

            UICanvas canvas = renderer is not null
                ? UICanvasManager.Create(canvasName, renderer, def.SortOrder)
                : UICanvasManager.CreateDetached(canvasName, def.SortOrder);

            if (def.ScaleMode is not null &&
                Enum.TryParse<UIScaleMode>(def.ScaleMode, ignoreCase: true, out var sm))
                canvas.ScaleMode = sm;

            if (def.ReferenceWidth > 0 && def.ReferenceHeight > 0)
                canvas.SetReferenceResolution(def.ReferenceWidth, def.ReferenceHeight);

            if (def.Visible is bool v)
                canvas.Visible = v;

            int built = 0;
            foreach (var elemDef in def.Elements ?? [])
            {
                var elem = BuildElement(elemDef);
                if (elem is not null)
                {
                    canvas.Add(elem);
                    built++;
                    Console.WriteLine($"[UILoader]   + {elem.GetType().Name} name='{elem.Name}' pos={elem.Position} size={elem.Size}");
                }
            }

            Console.WriteLine($"[UILoader] Canvas '{canvasName}' loaded with {built} elements.");
            return canvas;
        }

        private static UIElement? BuildElement(UIElementDefinition def)
        {
            UIElement? elem = def.Type?.ToLowerInvariant() switch
            {
                "label" => BuildLabel(def),
                "image" => BuildImage(def),
                "button" => BuildButton(def),
                "slider" => BuildSlider(def),
                "inputtext" => BuildInputText(def),
                _ => null
            };

            if (elem is null)
            {
                Console.WriteLine($"[UILoader] Unknown or missing element type: '{def.Type}' (name='{def.Name}') -- skipped.");
                return null;
            }

            // ── Common UIElement properties ───────────────────────────────
            if (def.Name is not null) elem.Name = def.Name;
            if (def.ZOrder is int z) elem.ZOrder = z;
            if (def.Visible is bool vis) elem.Visible = vis;
            if (def.Enabled is bool en) elem.Enabled = en;
            if (def.Position is not null) elem.Position = ParseVec2(def.Position);
            if (def.Size is not null) elem.Size = ParseVec2(def.Size);
            if (def.Color is not null) elem.Color = ParseVec4(def.Color);

            return elem;
        }

        // ── Element builders ──────────────────────────────────────────────

        private static UILabel BuildLabel(UIElementDefinition d)
        {
            var lbl = new UILabel();
            if (d.Text is not null) lbl.Text = d.Text;
            if (d.FontName is not null) lbl.FontName = d.FontName;
            if (d.FontSize is float fs) lbl.FontSize = fs;
            if (d.AutoSize is bool a) lbl.AutoSize = a;
            if (d.Rotation is float r) lbl.Rotation = r;
            if (d.SuperSample is int ss) lbl.SuperSample = ss;
            if (d.FontStyle is not null && Enum.TryParse<System.Drawing.FontStyle>(d.FontStyle, true, out var fs2))
                lbl.FontStyle = fs2;
            if (d.TextColor is not null) lbl.TextColor = ParseSdColor(d.TextColor);
            if (d.ShadowColor is not null) lbl.ShadowColor = ParseSdColor(d.ShadowColor);
            if (d.ShadowOffset is not null) lbl.ShadowOffset = new PointF(d.ShadowOffset[0], d.ShadowOffset[1]);
            return lbl;
        }

        private static UIImage BuildImage(UIElementDefinition d)
        {
            var img = new UIImage();
            if (d.Rotation is float r) img.Rotation = r;
            if (d.TexturePath is not null) img.Tag = d.TexturePath; // resolve at runtime
            return img;
        }

        private static UIButton BuildButton(UIElementDefinition d)
        {
            var btn = new UIButton();
            if (d.Text is not null) btn.Text = d.Text;
            if (d.FontName is not null) btn.FontName = d.FontName;
            if (d.FontSize is float fs) btn.FontSize = fs;
            if (d.CornerRadius is float cr) btn.CornerRadius = cr;
            if (d.BorderWidth is float bw) btn.BorderWidth = bw;
            if (d.TransitionSpeed is float ts) btn.TransitionSpeed = ts;
            if (d.TextColor is not null) btn.TextColor = ParseSdColor(d.TextColor);
            if (d.NormalColor is not null) btn.NormalColor = ParseVec4(d.NormalColor);
            if (d.NormalTop is not null) btn.NormalTop = ParseVec4(d.NormalTop);
            if (d.NormalBottom is not null) btn.NormalBottom = ParseVec4(d.NormalBottom);
            if (d.HoverColor is not null) btn.HoverColor = ParseVec4(d.HoverColor);
            if (d.HoverTop is not null) btn.HoverTop = ParseVec4(d.HoverTop);
            if (d.HoverBottom is not null) btn.HoverBottom = ParseVec4(d.HoverBottom);
            if (d.PressedColor is not null) btn.PressedColor = ParseVec4(d.PressedColor);
            if (d.PressedTop is not null) btn.PressedTop = ParseVec4(d.PressedTop);
            if (d.PressedBottom is not null) btn.PressedBottom = ParseVec4(d.PressedBottom);
            if (d.BorderColor is not null) btn.BorderColor = ParseVec4(d.BorderColor);
            if (d.ShadowColor is not null) btn.ShadowColor = ParseVec4(d.ShadowColor);
            return btn;
        }

        private static UISlider BuildSlider(UIElementDefinition d)
        {
            var sld = new UISlider();
            if (d.Min is float mn) sld.Min = mn;
            if (d.Max is float mx) sld.Max = mx;
            if (d.Value is float val) sld.Value = val;
            if (d.ThumbRadius is float tr) sld.ThumbRadius = tr;
            if (d.TrackBorder is float tb) sld.TrackBorder = tb;
            if (d.ShowValue is bool sv) sld.ShowValue = sv;
            if (d.ValueFormat is not null) sld.ValueFormat = d.ValueFormat;
            if (d.LabelFontSize is float lfs) sld.LabelFontSize = lfs;
            if (d.LabelFontName is not null) sld.LabelFontName = d.LabelFontName;
            if (d.LabelColor is not null) sld.LabelColor = ParseSdColor(d.LabelColor);
            if (d.TrackColor is not null) sld.TrackColor = ParseVec4(d.TrackColor);
            if (d.FillColorA is not null) sld.FillColorA = ParseVec4(d.FillColorA);
            if (d.FillColorB is not null) sld.FillColorB = ParseVec4(d.FillColorB);
            if (d.ThumbColor is not null) sld.ThumbColor = ParseVec4(d.ThumbColor);
            return sld;
        }

        private static UIInputText BuildInputText(UIElementDefinition d)
        {
            var txt = new UIInputText();
            if (d.Placeholder is not null) txt.Placeholder = d.Placeholder;
            if (d.MaxLength is int ml) txt.MaxLength = ml;
            if (d.IsPassword is bool pw) txt.IsPassword = pw;
            if (d.IsNumericOnly is bool nm) txt.IsNumericOnly = nm;
            if (d.FontName is not null) txt.FontName = d.FontName;
            if (d.FontSize is float fs) txt.FontSize = fs;
            if (d.CornerRadius is float cr) txt.CornerRadius = cr;
            if (d.BorderWidth is float bw) txt.BorderWidth = bw;
            if (d.TextColor is not null) txt.TextColor = ParseSdColor(d.TextColor);
            if (d.NormalColor is not null) txt.NormalColor = ParseVec4(d.NormalColor);
            if (d.HoverColor is not null) txt.HoverColor = ParseVec4(d.HoverColor);
            if (d.FocusedColor is not null) txt.FocusedColor = ParseVec4(d.FocusedColor);
            if (d.BorderColor is not null) txt.BorderNormal = ParseVec4(d.BorderColor);
            if (d.BorderFocusedColor is not null) txt.BorderFocused = ParseVec4(d.BorderFocusedColor);
            return txt;
        }

        // ── Parse helpers ─────────────────────────────────────────────────

        private static Vector4 ParseVec4(JsonElement? e)
        {
            if (e is null) return Vector4.One;
            var arr = e.Value;
            float r = arr[0].GetSingle();
            float g = arr[1].GetSingle();
            float b = arr[2].GetSingle();
            float a = arr.GetArrayLength() > 3 ? arr[3].GetSingle() : 1f;
            if (r > 1f || g > 1f || b > 1f || a > 1f)
            { r /= 255f; g /= 255f; b /= 255f; a /= 255f; }
            return new Vector4(r, g, b, a);
        }

        private static Vector2 ParseVec2(JsonElement? e)
        {
            if (e is null) return Vector2.Zero;
            var arr = e.Value;
            return new Vector2(arr[0].GetSingle(), arr[1].GetSingle());
        }

        private static Color ParseSdColor(JsonElement? e)
        {
            if (e is null) return Color.White;
            var el = e.Value;

            if (el.ValueKind == JsonValueKind.String)
            {
                string hex = el.GetString()!.TrimStart('#');
                if (hex.Length == 6) hex += "FF";
                uint val = Convert.ToUInt32(hex, 16);
                return Color.FromArgb(
                    (int)((val) & 0xFF),  // A (last two hex digits)
                    (int)((val >> 24) & 0xFF),  // R
                    (int)((val >> 16) & 0xFF),  // G
                    (int)((val >> 8) & 0xFF)); // B
            }

            var v4 = ParseVec4(e);
            return Color.FromArgb(
                (int)(v4.W * 255),
                (int)(v4.X * 255),
                (int)(v4.Y * 255),
                (int)(v4.Z * 255));
        }
    }

    // ── JSON schema ───────────────────────────────────────────────────────

    internal sealed class UICanvasDefinition
    {
        public string? Name { get; set; }
        public int SortOrder { get; set; } = 0;
        public bool? Visible { get; set; }
        public string? ScaleMode { get; set; }
        public float ReferenceWidth { get; set; } = 1920f;
        public float ReferenceHeight { get; set; } = 1080f;
        public List<UIElementDefinition>? Elements { get; set; }
    }

    internal sealed class UIElementDefinition
    {
        public string? Type { get; set; }
        public string? Name { get; set; }
        public int? ZOrder { get; set; }
        public bool? Visible { get; set; }
        public bool? Enabled { get; set; }
        [JsonPropertyName("position")]
        public JsonElement? Position { get; set; }
        [JsonPropertyName("size")]
        public JsonElement? Size { get; set; }
        [JsonPropertyName("color")]
        public JsonElement? Color { get; set; }

        // Label
        public string? Text { get; set; }
        public string? FontName { get; set; }
        public float? FontSize { get; set; }
        public string? FontStyle { get; set; }
        public bool? AutoSize { get; set; }
        public float? Rotation { get; set; }
        public int? SuperSample { get; set; }
        [JsonPropertyName("textColor")]
        public JsonElement? TextColor { get; set; }
        [JsonPropertyName("shadowColor")]
        public JsonElement? ShadowColor { get; set; }
        public float[]? ShadowOffset { get; set; }

        // Image
        public string? TexturePath { get; set; }

        // Button
        public float? CornerRadius { get; set; }
        public float? BorderWidth { get; set; }
        public float? TransitionSpeed { get; set; }
        [JsonPropertyName("normalColor")] public JsonElement? NormalColor { get; set; }
        [JsonPropertyName("normalTop")] public JsonElement? NormalTop { get; set; }
        [JsonPropertyName("normalBottom")] public JsonElement? NormalBottom { get; set; }
        [JsonPropertyName("hoverColor")] public JsonElement? HoverColor { get; set; }
        [JsonPropertyName("hoverTop")] public JsonElement? HoverTop { get; set; }
        [JsonPropertyName("hoverBottom")] public JsonElement? HoverBottom { get; set; }
        [JsonPropertyName("pressedColor")] public JsonElement? PressedColor { get; set; }
        [JsonPropertyName("pressedTop")] public JsonElement? PressedTop { get; set; }
        [JsonPropertyName("pressedBottom")] public JsonElement? PressedBottom { get; set; }
        [JsonPropertyName("borderColor")] public JsonElement? BorderColor { get; set; }

        // Slider
        public float? Min { get; set; }
        public float? Max { get; set; }
        public float? Value { get; set; }
        public float? ThumbRadius { get; set; }
        public float? TrackBorder { get; set; }
        public bool? ShowValue { get; set; }
        public string? ValueFormat { get; set; }
        public float? LabelFontSize { get; set; }
        public string? LabelFontName { get; set; }
        [JsonPropertyName("labelColor")] public JsonElement? LabelColor { get; set; }
        [JsonPropertyName("trackColor")] public JsonElement? TrackColor { get; set; }
        [JsonPropertyName("fillColorA")] public JsonElement? FillColorA { get; set; }
        [JsonPropertyName("fillColorB")] public JsonElement? FillColorB { get; set; }
        [JsonPropertyName("thumbColor")] public JsonElement? ThumbColor { get; set; }

        // InputText
        public string? Placeholder { get; set; }
        public int? MaxLength { get; set; }
        public bool? IsPassword { get; set; }
        public bool? IsNumericOnly { get; set; }
        [JsonPropertyName("focusedColor")] public JsonElement? FocusedColor { get; set; }
        [JsonPropertyName("borderFocusedColor")] public JsonElement? BorderFocusedColor { get; set; }
    }
}