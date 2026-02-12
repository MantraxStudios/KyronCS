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
        Point,
        Bilinear,
        Trilinear
    }

    public sealed class TextureLoader : IDisposable
    {
        private int _textureId;
        private bool _isLoaded;
        private readonly bool _generateMipmaps;
        private readonly bool _flip;
        private readonly TextureFilterMode _filterMode;

        private Task<byte[]> _loadingTask;
        private byte[] _bytesResult;
        private bool _bytesReady;
        private bool _loadFailed;
        private bool _isFallbackTexture;

        public Guid TextureGUID { get; set; }
        public string Name { get; }
        public int TextureId => _textureId;
        public bool IsLoaded => _isLoaded;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public TextureFilterMode FilterMode => _filterMode;

        public TextureLoader(string name, Guid textureGuid, bool generateMipmaps = true, bool flip = true)
            : this(name, textureGuid, generateMipmaps, flip, TextureFilterMode.Point)
        {
        }

        public TextureLoader(string name, Guid textureGuid, bool generateMipmaps, bool flip, TextureFilterMode filterMode)
        {
            Name = name;
            TextureGUID = textureGuid;
            _generateMipmaps = generateMipmaps;
            _flip = flip;
            _filterMode = filterMode;

            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("TextureLoader: Name cannot be null or empty, creating fallback texture");
                CreateFallbackTexture();
                return;
            }

            if (textureGuid == Guid.Empty)
            {
                Console.WriteLine("TextureLoader: GUID is empty, creating fallback texture");
                CreateFallbackTexture();
                return;
            }

            _loadingTask = Task.Run(() => AssetManager.GetBytes(textureGuid));
        }

        private TextureLoader(string name, TextureFilterMode filterMode, bool generateMipmaps)
        {
            Name = name;
            TextureGUID = Guid.Empty;
            _filterMode = filterMode;
            _generateMipmaps = generateMipmaps;
            _flip = false;
        }

        public static TextureLoader FromAbsolutePath(string name, string absolutePath, bool generateMipmaps = true, bool flip = true, TextureFilterMode filterMode = TextureFilterMode.Trilinear)
        {
            byte[] fileBytes = File.ReadAllBytes(absolutePath);
            return FromImageBytes(name, fileBytes, flip, generateMipmaps, filterMode);
        }

        public static TextureLoader FromRelativePath(string name, string relativePath, bool generateMipmaps = true, bool flip = true, TextureFilterMode filterMode = TextureFilterMode.Trilinear)
        {
            string absolutePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            return FromAbsolutePath(name, absolutePath, generateMipmaps, flip, filterMode);
        }

        public static TextureLoader ForPixelArt(string name, Guid textureGuid, bool flip = true)
        {
            return new TextureLoader(name, textureGuid, generateMipmaps: false, flip: flip, TextureFilterMode.Point);
        }

        public static TextureLoader ForSprites(string name, Guid textureGuid, bool flip = true)
        {
            return new TextureLoader(name, textureGuid, generateMipmaps: false, flip: flip, TextureFilterMode.Bilinear);
        }

        public static TextureLoader FromBytes(string name, byte[] data, int width, int height, bool generateMipmaps = true, TextureFilterMode filterMode = TextureFilterMode.Trilinear)
        {
            var loader = new TextureLoader(name, filterMode, generateMipmaps);

            loader.Width = width;
            loader.Height = height;

            loader._textureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, loader._textureId);

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba,
                width,
                height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                data
            );

            loader.ApplyTextureFilters();

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            loader._isLoaded = true;
            return loader;
        }

        public static TextureLoader FromImageBytes(string name, byte[] fileBytes, bool flip = true, bool generateMipmaps = true, TextureFilterMode filterMode = TextureFilterMode.Trilinear)
        {
            StbImage.stbi_set_flip_vertically_on_load(flip ? 1 : 0);
            var image = ImageResult.FromMemory(fileBytes, ColorComponents.RedGreenBlueAlpha);
            return FromBytes(name, image.Data, image.Width, image.Height, generateMipmaps, filterMode);
        }

        private void CreateFallbackTexture()
        {
            const int size = 256;
            const int checkerSize = 32;
            byte[] textureData = new byte[size * size * 4];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = (y * size + x) * 4;
                    bool isBlack = ((x / checkerSize) + (y / checkerSize)) % 2 == 0;

                    if (isBlack)
                    {
                        textureData[index] = 0;
                        textureData[index + 1] = 0;
                        textureData[index + 2] = 0;
                        textureData[index + 3] = 255;
                    }
                    else
                    {
                        textureData[index] = 255;
                        textureData[index + 1] = 0;
                        textureData[index + 2] = 255;
                        textureData[index + 3] = 255;
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

            Console.WriteLine($"Fallback texture created for GUID: {TextureGUID}");
        }

        private void ApplyTextureFilters()
        {
            switch (_filterMode)
            {
                case TextureFilterMode.Point:
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                    break;

                case TextureFilterMode.Bilinear:
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    break;

                case TextureFilterMode.Trilinear:
                    if (_generateMipmaps)
                    {
                        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    }
                    else
                    {
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

            if (_loadFailed)
            {
                CreateFallbackTexture();
                return;
            }

            if (!_bytesReady)
            {
                if (_loadingTask != null && _loadingTask.IsCompleted)
                {
                    _bytesResult = _loadingTask.GetAwaiter().GetResult();
                    _bytesReady = true;

                    if (_bytesResult == null)
                    {
                        Console.WriteLine($"Failed to load texture GUID: {TextureGUID}, creating fallback texture");
                        _loadFailed = true;
                        CreateFallbackTexture();
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            try
            {
                StbImage.stbi_set_flip_vertically_on_load(_flip ? 1 : 0);
                var image = ImageResult.FromMemory(_bytesResult, ColorComponents.RedGreenBlueAlpha);

                Width = image.Width;
                Height = image.Height;

                _textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, _textureId);

                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    PixelInternalFormat.Rgba,
                    image.Width,
                    image.Height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    image.Data
                );

                ApplyTextureFilters();

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

                GL.BindTexture(TextureTarget.Texture2D, 0);

                _isLoaded = true;
                _loadingTask = null;
                _bytesResult = null;

                Console.WriteLine($"Texture loaded successfully GUID: {TextureGUID} (Filter: {_filterMode})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading texture to GPU GUID '{TextureGUID}': {ex.Message}, creating fallback texture");
                CreateFallbackTexture();
            }
        }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            if (!_isLoaded)
                Load();

            if (_isLoaded)
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
            _bytesReady = false;
            _bytesResult = null;
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