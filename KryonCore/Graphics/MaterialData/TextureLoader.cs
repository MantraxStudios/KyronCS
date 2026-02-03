using OpenTK.Graphics.OpenGL4;
using StbImageSharp;
using System;
using System.IO;

namespace KrayonCore
{
    public sealed class TextureLoader : IDisposable
    {
        private int _textureId;
        private bool _isLoaded;
        private readonly string _path;
        private readonly bool _generateMipmaps;
        private readonly bool _flip;

        public string Name { get; }
        public int TextureId => _textureId;
        public bool IsLoaded => _isLoaded;
        public int Width { get; private set; }
        public int Height { get; private set; }

        public string GetTexturePath
        {
            get
            {
                return _path;
            }
        }

        public TextureLoader(string name, string path, bool generateMipmaps = true, bool flip = true)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _generateMipmaps = generateMipmaps;
            _flip = flip;
        }

        public void Load()
        {
            if (_isLoaded)
                return;

            if (!File.Exists(_path))
                throw new FileNotFoundException($"Texture not found: {_path}");
            else
                Console.WriteLine($"Texture found: {_path}");

            StbImage.stbi_set_flip_vertically_on_load(_flip ? 1 : 0);

            using var stream = File.OpenRead(_path);
            ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

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

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            _isLoaded = true;
        }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            if (!_isLoaded)
                Load();

            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, _textureId);
        }

        public void Unload()
        {
            if (!_isLoaded)
                return;

            GL.DeleteTexture(_textureId);
            _textureId = 0;
            _isLoaded = false;
        }

        public void Dispose()
        {
            Unload();
        }
    }
}