using KrayonCore.Core.Attributes;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KrayonCore
{
    public enum TextureFilterMode
    {
        Point,          // Nearest - Sin filtro, pixelado (ideal para pixel art)
        Bilinear,       // Linear - Filtro suave
        Trilinear       // Linear con mipmaps - Filtro suave con mipmaps
    }

    public sealed class TextureLoader : IDisposable
    {
        private int _textureId;
        private bool _isLoaded;
        private readonly string _path;
        private readonly bool _generateMipmaps;
        private readonly bool _flip;
        private readonly bool _useAbsolutePath;
        private readonly TextureFilterMode _filterMode;

        private Task<ImageResult> _loadingTask;
        private ImageResult _imageResult;
        private bool _imageReady;
        private bool _loadFailed;
        private bool _isFallbackTexture;

        public string Name { get; }
        public int TextureId => _textureId;
        public bool IsLoaded => _isLoaded;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public TextureFilterMode FilterMode => _filterMode;

        public string GetTexturePath
        {
            get
            {
                return _path;
            }
        }

        public TextureLoader(string name, string path, bool generateMipmaps = true, bool flip = true)
            : this(name, path, generateMipmaps, flip, TextureFilterMode.Point, false)
        {
        }

        public TextureLoader(string name, string path, bool generateMipmaps, bool flip, TextureFilterMode filterMode)
            : this(name, path, generateMipmaps, flip, filterMode, false)
        {
        }

        private TextureLoader(string name, string path, bool generateMipmaps, bool flip, TextureFilterMode filterMode, bool useAbsolutePath)
        {
            Name = name;
            _path = path;
            _generateMipmaps = generateMipmaps;
            _flip = flip;
            _useAbsolutePath = useAbsolutePath;
            _filterMode = filterMode;

            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("TextureLoader: Name cannot be null or empty, creating fallback texture");
                CreateFallbackTexture();
                return;
            }

            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("TextureLoader: Path cannot be null or empty, creating fallback texture");
                CreateFallbackTexture();
                return;
            }

            // Iniciar la carga automáticamente de forma asíncrona
            _loadingTask = Task.Run(() => LoadImageFromDisk());
        }

        /// <summary>
        /// Crea un TextureLoader desde una ruta absoluta (sin usar AssetManager)
        /// </summary>
        public static TextureLoader FromAbsolutePath(string name, string absolutePath, bool generateMipmaps = true, bool flip = true, TextureFilterMode filterMode = TextureFilterMode.Trilinear)
        {
            return new TextureLoader(name, absolutePath, generateMipmaps, flip, filterMode, useAbsolutePath: true);
        }

        /// <summary>
        /// Crea un TextureLoader desde una ruta relativa al directorio de ejecución
        /// </summary>
        public static TextureLoader FromRelativePath(string name, string relativePath, bool generateMipmaps = true, bool flip = true, TextureFilterMode filterMode = TextureFilterMode.Trilinear)
        {
            string absolutePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            return new TextureLoader(name, absolutePath, generateMipmaps, flip, filterMode, useAbsolutePath: true);
        }

        /// <summary>
        /// Crea un TextureLoader optimizado para pixel art (sin filtro, sin mipmaps)
        /// </summary>
        public static TextureLoader ForPixelArt(string name, string path, bool flip = true)
        {
            return new TextureLoader(name, path, generateMipmaps: false, flip: flip, TextureFilterMode.Point, useAbsolutePath: false);
        }

        /// <summary>
        /// Crea un TextureLoader optimizado para sprites 2D (sin mipmaps, filtro bilinear)
        /// </summary>
        public static TextureLoader ForSprites(string name, string path, bool flip = true)
        {
            return new TextureLoader(name, path, generateMipmaps: false, flip: flip, TextureFilterMode.Bilinear, useAbsolutePath: false);
        }

        private ImageResult LoadImageFromDisk()
        {
            try
            {
                string fullPath = _useAbsolutePath ? _path : AssetManager.BasePath + _path;

                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"Texture not found: {fullPath}, will create fallback texture");
                    _loadFailed = true;
                    return null;
                }

                StbImage.stbi_set_flip_vertically_on_load(_flip ? 1 : 0);
                using var stream = File.OpenRead(fullPath);
                return ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading texture '{_path}': {ex.Message}, will create fallback texture");
                _loadFailed = true;
                return null;
            }
        }

        private void CreateFallbackTexture()
        {
            // Crear textura procedural de 256x256 con patrón de tablero magenta/negro (estilo Unreal)
            const int size = 256;
            const int checkerSize = 32;
            byte[] textureData = new byte[size * size * 4];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = (y * size + x) * 4;

                    // Determinar si estamos en un cuadrado magenta o negro
                    bool isBlack = ((x / checkerSize) + (y / checkerSize)) % 2 == 0;

                    if (isBlack)
                    {
                        // Negro
                        textureData[index] = 0;     // R
                        textureData[index + 1] = 0; // G
                        textureData[index + 2] = 0; // B
                        textureData[index + 3] = 255; // A
                    }
                    else
                    {
                        // Magenta
                        textureData[index] = 255;   // R
                        textureData[index + 1] = 0; // G
                        textureData[index + 2] = 255; // B
                        textureData[index + 3] = 255; // A
                    }
                }
            }

            Width = size;
            Height = size;

            _textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _textureId);

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba,
                size,
                size,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                textureData
            );

            ApplyTextureFilters();

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            _isLoaded = true;
            _isFallbackTexture = true;
            _loadFailed = false;

            Console.WriteLine($"Fallback texture created for: {_path ?? "null"}");
        }

        private void ApplyTextureFilters()
        {
            switch (_filterMode)
            {
                case TextureFilterMode.Point:
                    // Sin filtro - Nearest neighbor (pixelado, ideal para pixel art)
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                    break;

                case TextureFilterMode.Bilinear:
                    // Filtro bilinear - Suave, sin mipmaps
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    break;

                case TextureFilterMode.Trilinear:
                    // Filtro trilinear - Suave con mipmaps
                    if (_generateMipmaps)
                    {
                        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    }
                    else
                    {
                        // Si no hay mipmaps, usar bilinear
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    }
                    break;
            }
        }

        public void Load()
        {
            if (_isLoaded)
                return;

            // Si ya falló, crear textura fallback
            if (_loadFailed)
            {
                CreateFallbackTexture();
                return;
            }

            // Verificar si la carga asíncrona ya terminó (sin bloquear)
            if (!_imageReady)
            {
                if (_loadingTask != null && _loadingTask.IsCompleted)
                {
                    _imageResult = _loadingTask.GetAwaiter().GetResult();
                    _imageReady = true;

                    if (_imageResult == null)
                    {
                        Console.WriteLine($"Failed to load texture: {_path}, creating fallback texture");
                        CreateFallbackTexture();
                        return;
                    }
                }
                else
                {
                    // La imagen aún no está lista, salir sin bloquear
                    return;
                }
            }

            try
            {
                // La imagen está lista, subirla a GPU
                Width = _imageResult.Width;
                Height = _imageResult.Height;

                _textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, _textureId);

                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    PixelInternalFormat.Rgba,
                    _imageResult.Width,
                    _imageResult.Height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    _imageResult.Data
                );

                ApplyTextureFilters();

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

                GL.BindTexture(TextureTarget.Texture2D, 0);

                _isLoaded = true;
                _loadingTask = null;
                _imageResult = null; // Liberar memoria

                Console.WriteLine($"Texture loaded successfully: {_path} (Filter: {_filterMode})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading texture to GPU '{_path}': {ex.Message}, creating fallback texture");
                CreateFallbackTexture();
            }
        }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            if (!_isLoaded)
                Load(); // Intenta cargar, pero no bloquea si no está lista

            if (_isLoaded) // Solo hacer bind si ya está cargada
            {
                GL.ActiveTexture(unit);
                GL.BindTexture(TextureTarget.Texture2D, _textureId);
            }
        }

        public void Unload()
        {
            if (!_isLoaded)
                return;

            GL.DeleteTexture(_textureId);
            _textureId = 0;
            _isLoaded = false;
            _imageReady = false;
            _imageResult = null;
            _isFallbackTexture = false;
        }

        public void Dispose()
        {
            Unload();
        }

        public bool IsFallbackTexture()
        {
            return _isFallbackTexture;
        }
    }
}