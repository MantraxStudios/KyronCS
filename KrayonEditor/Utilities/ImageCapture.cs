using System;
using System.IO;
using SkiaSharp;

namespace KrayonEditor.Utilities
{
    public static class ImageCapture
    {
        public static void SaveFramebuffer(byte[] framebuffer, int width, int height, string path)
        {
            if (framebuffer == null)
                throw new ArgumentNullException(nameof(framebuffer));

            if (framebuffer.Length != width * height * 4)
                throw new ArgumentException("El tamaño del framebuffer no coincide con width*height*4");

            // 🔹 Voltear verticalmente (OpenGL viene invertido)
            byte[] flipped = new byte[framebuffer.Length];
            int stride = width * 4;

            for (int y = 0; y < height; y++)
            {
                Buffer.BlockCopy(
                    framebuffer,
                    y * stride,
                    flipped,
                    (height - y - 1) * stride,
                    stride
                );
            }

            // 🔹 Crear bitmap
            using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

            unsafe
            {
                fixed (byte* ptr = flipped)
                {
                    bitmap.SetPixels((IntPtr)ptr);
                }
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            // 🔹 Crear directorio si no existe
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
        }
    }
}