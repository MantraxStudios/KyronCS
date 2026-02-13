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
                string texturesDir = "textures";
                if (!System.IO.Directory.Exists(texturesDir))
                {
                    System.IO.Directory.CreateDirectory(texturesDir);
                }

                int width = 512;
                int height = 512;
                int checkerSize = 32;

                byte[] pixels = new byte[width * height * 4];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width + x) * 4;

                        bool isWhite = ((x / checkerSize) + (y / checkerSize)) % 2 == 0;
                        byte color = (byte)(isWhite ? 255 : 100);

                        pixels[index + 0] = color;    
                        pixels[index + 1] = color;     
                        pixels[index + 2] = color;     
                        pixels[index + 3] = 255;       
                    }
                }

                string outputPath = System.IO.Path.Combine(texturesDir, "procedural_checker.png");

                using (var stream = System.IO.File.OpenWrite(outputPath))
                {
                    var writer = new StbImageWriteSharp.ImageWriter();
                    writer.WritePng(pixels, width, height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
                }

                return outputPath;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
