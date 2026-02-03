using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrayonCore.Graphics
{
    public class ProceduralTexture
    {
        public static string CreateProceduralTextureFile()
        {
            try
            {
                // Crear directorio de texturas si no existe
                string texturesDir = "textures";
                if (!System.IO.Directory.Exists(texturesDir))
                {
                    System.IO.Directory.CreateDirectory(texturesDir);
                }

                // Crear textura procedural de tablero de ajedrez
                int width = 256;
                int height = 256;
                int checkerSize = 32;

                byte[] pixels = new byte[width * height * 4];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width + x) * 4;

                        bool isWhite = ((x / checkerSize) + (y / checkerSize)) % 2 == 0;
                        byte color = (byte)(isWhite ? 255 : 100);

                        pixels[index + 0] = color;     // R
                        pixels[index + 1] = color;     // G
                        pixels[index + 2] = color;     // B
                        pixels[index + 3] = 255;       // A
                    }
                }

                // Guardar como archivo PNG
                string outputPath = System.IO.Path.Combine(texturesDir, "procedural_checker.png");

                using (var stream = System.IO.File.OpenWrite(outputPath))
                {
                    var writer = new StbImageWriteSharp.ImageWriter();
                    writer.WritePng(pixels, width, height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
                }

                // EngineEditor.LogMessage($"Procedural texture saved to: {outputPath}");
                return outputPath;
            }
            catch (Exception ex)
            {
                // EngineEditor.LogMessage($"Error creating procedural texture file: {ex.Message}");
                return null;
            }
        }
    }
}
