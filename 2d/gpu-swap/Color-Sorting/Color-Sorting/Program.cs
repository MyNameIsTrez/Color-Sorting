using System;
//using System.Globalization;
//using System.Linq;
//using CommunityToolkit.Diagnostics;
using ComputeSharp;
using System.Drawing;
//using System.Drawing.Common;
//using System.Drawing.Imaging;
using Colourful;

//using OpenTK.Windowing.Common;
//using ComputeSharp.Sample;
/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
*/

internal class Program
{
    private static byte get_normalized_lab(double x, double min, double range)
    {
        return Convert.ToByte(((x - min) / range) * 255);
    }
    private static double get_denormalized_lab(double x, double min, double range)
    {
        return ((x / 255) * range) + min;
    }

    private static void Main(string[] args)
    {
        var rgbWorkingSpace = RGBWorkingSpaces.sRGB;
        var rgbToLab = new ConverterBuilder().FromRGB(rgbWorkingSpace).ToLab(Illuminants.D50).Build();
        var labToRgb = new ConverterBuilder().FromLab(Illuminants.D50).ToRGB(rgbWorkingSpace).Build();

        
        // Source: https://stackoverflow.com/a/19099064/13279557
        double maxL = double.MinValue;
        double maxA = double.MinValue;
        double maxB = double.MinValue;
        double minL = double.MaxValue;
        double minA = double.MaxValue;
        double minB = double.MaxValue;

        for (double r = 0; r < 256; ++r)
        {
            Console.WriteLine(String.Format("{0}/255", r));
            for (double g = 0; g < 256; ++g)
                for (double b = 0; b < 256; ++b)
                {
                    var rgb = new RGBColor(r / 255, g / 255, b / 255);
                    var lab = rgbToLab.Convert(rgb);

                    maxL = Math.Max(maxL, lab.L);
                    maxA = Math.Max(maxA, lab.a);
                    maxB = Math.Max(maxB, lab.b);
                    minL = Math.Min(minL, lab.L);
                    minA = Math.Min(minA, lab.a);
                    minB = Math.Min(minB, lab.b);
                }
        }

        var rangeL = maxL - minL;
        var rangeA = maxA - minA;
        var rangeB = maxB - minB;

        Console.WriteLine(String.Format("L in [{0}, {1}], range {2}", minL, maxL, rangeL));
        Console.WriteLine(String.Format("a in [{0}, {1}], range {2}", minA, maxA, rangeA));
        Console.WriteLine(String.Format("b in [{0}, {1}], range {2}", minB, maxB, rangeB));
        

        Console.WriteLine("Turning input image into CIELab pixels...");
        var palette = new Bitmap("I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/palette.bmp");
        
        var pixels = new Rgba32[palette.Height, palette.Width];

        /*var k = new RGBColor(1, 0, 1);
        var l = rgbToLab.Convert(k);
        var m = labToRgb.Convert(l);*/

        for (int y = 0; y < palette.Height; y++)
        {
            for (int x = 0; x < palette.Width; x++)
            {
                var pixel = palette.GetPixel(x, y);
                //pixels[y, x] = new Rgba32(pixel.R, pixel.G, pixel.B);
                //pixels[y, x] = new Rgba32(255, 127, 10);

                var rgb = new RGBColor(Convert.ToDouble(pixel.R) / 255, Convert.ToDouble(pixel.G) / 255, Convert.ToDouble(pixel.B) / 255);
                var lab = rgbToLab.Convert(rgb);

                var normalized_L = get_normalized_lab(lab.L, minL, rangeL);
                var normalized_A = get_normalized_lab(lab.a, minA, rangeA);
                var normalized_B = get_normalized_lab(lab.b, minB, rangeB);
                pixels[y, x] = new Rgba32(normalized_L, normalized_A, normalized_B);

                /*var l = Convert.ToByte(lab.L + 128);
                var rgba = new Rgba32(l, Convert.ToByte(lab.a + 128), Convert.ToByte(lab.b + 128));
                pixels[y, x] = rgba;*/
            }
        }

        
        for (int y = 0; y < palette.Height; y++)
        {
            for (int x = 0; x < palette.Width; x++)
            {
                var pixel = pixels[y, x];

                var L = get_denormalized_lab(pixel.R, minL, rangeL);
                var A = get_denormalized_lab(pixel.G, minA, rangeA);
                var B = get_denormalized_lab(pixel.B, minB, rangeB);
                var lab = new LabColor(L, A, B);
                var rgb = labToRgb.Convert(lab);

                var denormalized_r = Convert.ToByte(Math.Clamp(rgb.R * 255, 0, 255));
                var denormalized_g = Convert.ToByte(Math.Clamp(rgb.G * 255, 0, 255));
                var denormalized_b = Convert.ToByte(Math.Clamp(rgb.B * 255, 0, 255));
                pixels[y, x] = new Rgba32(denormalized_r, denormalized_g, denormalized_b);

                /*var l = pixel.R - 128;
                //var lab = new LabColor(Convert.ToDouble(l), Convert.ToDouble(pixel.G - 128), Convert.ToDouble(pixel.B - 128));
                var lab = new LabColor(60, 94, -60);
                var rgb = labToRgb.Convert(lab);
                pixels[y, x] = new Rgba32(Convert.ToByte(rgb.R), Convert.ToByte(rgb.G), Convert.ToByte(rgb.B));*/
            }
        }
        

        Console.WriteLine("Allocating pixels GPU buffer...");
        using var texture = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<Rgba32, float4>(pixels);
        //using var texture = GraphicsDevice.GetDefault().AllocateReadWriteBuffer(pixels.ToArray());
        //using var texture = GraphicsDevice.GetDefault().LoadReadWriteTexture2D<Rgba32, float4>("I:/Programming/Color-Sorting/Color-Sorting/palette.bmp");

        Console.WriteLine(texture);

        //GraphicsDevice.GetDefault().For(texture.Width, texture.Height, new GrayscaleEffect(texture));

        texture.Save("I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/1.png");
    }
}

[AutoConstructor]
public readonly partial struct GrayscaleEffect : IComputeShader
{
    public readonly IReadWriteNormalizedTexture2D<float4> texture;

    // Other captured resources or values here...

    public void Execute()
    {
        // Our image processing logic here. In this example, we are just
        // applying a naive grayscale effect to all pixels in the image.
        float3 rgb = texture[ThreadIds.XY].RGB;
        float avg = Hlsl.Dot(rgb, new(0.0722f, 0.7152f, 0.2126f));

        texture[ThreadIds.XY].RGB = avg;
    }
}
