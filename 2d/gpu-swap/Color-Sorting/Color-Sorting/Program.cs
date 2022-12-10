﻿using Colourful;
using ComputeSharp;
using System.Drawing;

internal class Program
{
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/cat.jpg";

    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/palette.bmp";
    const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/10x10_palette.bmp";

    const string OUTPUT_IMAGES_DIRECTORY_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting";

    static private LabInformation labInfo;
    static private Bitmap img;
    static private Rgba32[,] pixels;

    private static void Main(string[] args)
    {
        img = new Bitmap(INPUT_IMAGE_PATH);
        var width = img.Width;
        var height = img.Height;

        /*
        labInfo = new LabInformation();
        // TODO: Try using Rgb everywhere instead, to save bytes.
        pixels = new Rgba32[height, width];

        Console.WriteLine("Lab normalizing pixels...");
        LabNormalizePixels();

        Console.WriteLine("Lab denormalizing pixels...");
        LabDenormalizePixels();

        Console.WriteLine("Allocating pixels GPU texture...");
        using var texture = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<Rgba32, float4>(pixels);
        */


        var positions = Enumerable.Range(0, width * height).ToList();

        var available = positions.ToList();
        var availableCount = width * height;

        var availableIndices = new List<int>();

        var rnd = new Random();

        Console.Clear();
        PrintGrid(positions, availableCount, width, height);
        while (availableCount > 0)
        {
            Thread.Sleep(500);

            var availableIndex = available[rnd.Next(availableCount)];
            availableIndices.Add(availableIndex);

            //availableCount = MarkUnavailable(index, available, positions, availableCount);
            availableCount = MarkNeighborsAndSelfUnavailable(availableIndex, available, positions, availableCount, width, height);

            Console.Clear();
            Console.WriteLine(availableIndex.ToString("D2"));
            Console.WriteLine(availableCount);
            PrintGrid(positions, availableCount, width, height);
        }

        availableIndices.ForEach(Console.WriteLine);

        // If `availableIndices` is `[ A, B, C ]`, then `[ A, B ]` is the only pair of indices that will be swapped
        // Using availableIndices[ThreadIds.X * 2] and availableIndices[ThreadIds.X * 2 + 1]
        //GraphicsDevice.GetDefault().For((int)(availableIndices.Count / 2), new Foo(availableIndices, img));

        //Console.WriteLine(GraphicsDevice.GetDefault().IsReadWriteTexture2DSupportedForType<Rgba32>());


        //using var texture = GraphicsDevice.GetDefault().AllocateReadWriteBuffer(pixels.ToArray());
        //using var texture = GraphicsDevice.GetDefault().LoadReadWriteTexture2D<Rgba32, float4>("I:/Programming/Color-Sorting/Color-Sorting/palette.bmp");

        //GraphicsDevice.GetDefault().For(texture.Width, texture.Height, new GrayscaleEffect(texture));


        //Console.WriteLine("Saving result...");
        //texture.Save(Path.Combine(OUTPUT_IMAGES_DIRECTORY_PATH, "1.png"));

    }

    /*
     * Prints this:
     * +--+--+
     * |00|01|
     * |  |XX|
     * +--+--+
     * |02|03|
     * |  |xx|
     * +--+--+
     */
    private static void PrintGrid(List<int> positions, int availableCount, int width, int height)
    {
        for (var y = 0; y < height; ++y)
        {
            PrintHorizontalLine(width);

            for (var x = 0; x < width; ++x)
            {
                var index = GetIndex(x, y, width);
                Console.Write("|{0}", index.ToString("D2"));
            }

            Console.WriteLine("|");

            for (var x = 0; x < width; ++x)
            {
                var index = GetIndex(x, y, width);
                Console.Write("|{0}", IsAvailable(index, positions, availableCount) ? "  " : "XX");
            }

            Console.WriteLine("|");
        }

        PrintHorizontalLine(width);
    }

    /*
     * Prints this:
     * +--+--+
     */
    private static void PrintHorizontalLine(int width)
    {
        for (var x = 0; x < width; ++x)
        {
            Console.Write("+--");
        }

        Console.WriteLine("+");
    }

    private static void PrintAvailable(List<int> available, int availableCount)
    {
        Console.Write(String.Format("available: [ {0} ", String.Join(", ", available.Take(availableCount))));
        Console.WriteLine(String.Format("| {0} ]", String.Join(", ", available.Skip(availableCount))));
    }

    private static void PrintPositions(List<int> positions)
    {
        Console.WriteLine(String.Format("positions: [ {0} ]", String.Join(", ", positions)));
    }

    private static int MarkNeighborsAndSelfUnavailable(int availableIndex, List<int> available, List<int> positions, int availableCount, int width, int height)
    {
        int x = availableIndex % width;
        int y = (int)(availableIndex / width);

        for (var dy = -2; dy <= 2; ++dy)
        {
            if (y + dy < 0 || y + dy >= height)
                continue;
            for (var dx = -2; dx <= 2; ++dx)
            {
                if (x + dx < 0 || x + dx >= width)
                    continue;
                var neighborOrOwnIndex = GetIndex(x + dx, y + dy, width);
                availableCount = MarkUnavailable(neighborOrOwnIndex, available, positions, availableCount);
            }
        }

        return availableCount;
    }

    private static int GetIndex(int x, int y, int width)
    {
        return x + y * width;
    }

    /*
     * Example usage of this function:
     * Whatever is to the right of the | in these lists is "unavailable" due to availableCount
     * 
     * availableCount = 4
     * available = [ 0, 1, 2, 3 | ]
     * positions = [ 0, 1, 2, 3 ]
     * 
     * MarkUnavailable(2)
     * available == [ 0, 1, 3, | 2 ]
     * positions == [ 0, 1, 3, 2 ]
     * availableCount == 3
     * 
     * MarkUnavailable(2) // Nothing happens since this index was already removed, because `positions[2] < availableCount` -> `3 < 3` -> `false`
     * available == [ 0, 1, 3, | 2 ]
     * positions == [ 0, 1, 3, 2 ]
     * availableCount == 3
     * 
     * MarkUnavailable(0)
     * available == [ 3, 1, | 0, 2 ] 
     * positions == [ 2, 1, 3, 0 ] // Note how you still need the "available" list since the 2 and 0 swapping here makes no sense otherwise
     * availableCount == 2
     */
    private static int MarkUnavailable(int index, List<int> available, List<int> positions, int availableCount)
    {
        if (IsAvailable(index, positions, availableCount))
        {
            var availableIndex = positions[index];

            var a = available[availableIndex];
            var b = available[availableCount - 1];

            available[availableIndex] = b;
            available[availableCount - 1] = a;

            positions[index] = availableCount - 1;
            positions[b] = availableIndex;

            --availableCount;
        }

        return availableCount;
    }

    /*
     * See the MarkUnavailable() example in its documentation.
     */
    private static bool IsAvailable(int index, List<int> positions, int availableCount)
    {
        var availableIndex = positions[index];
        return availableIndex < availableCount;
    }

    private static void LabNormalizePixels()
    {
        for (int y = 0; y < img.Height; ++y)
        {
            for (int x = 0; x < img.Width; ++x)
            {
                var pixel = img.GetPixel(x, y);

                var rgb = new RGBColor(Convert.ToDouble(pixel.R) / 255, Convert.ToDouble(pixel.G) / 255, Convert.ToDouble(pixel.B) / 255);
                var lab = labInfo.RGBToLab.Convert(rgb);

                var normalizedL = GetNormalizedLab(lab.L, labInfo.minL, labInfo.rangeL);
                var normalizedA = GetNormalizedLab(lab.a, labInfo.minA, labInfo.rangeA);
                var normalizedB = GetNormalizedLab(lab.b, labInfo.minB, labInfo.rangeB);
                pixels[y, x] = new Rgba32(normalizedL, normalizedA, normalizedB);
            }
        }
    }

    private static byte GetNormalizedLab(double x, double min, double range)
    {
        return Convert.ToByte(((x - min) / range) * 255);
    }

    private static void LabDenormalizePixels()
    {
        for (int y = 0; y < img.Height; ++y)
        {
            for (int x = 0; x < img.Width; ++x)
            {
                var pixel = pixels[y, x];

                var L = GetDenormalizedLab(pixel.R, labInfo.minL, labInfo.rangeL);
                var A = GetDenormalizedLab(pixel.G, labInfo.minA, labInfo.rangeA);
                var B = GetDenormalizedLab(pixel.B, labInfo.minB, labInfo.rangeB);
                var lab = new LabColor(L, A, B);
                var rgb = labInfo.LabToRGB.Convert(lab);

                var denormalizedR = Convert.ToByte(Math.Clamp(rgb.R * 255, 0, 255));
                var denormalizedG = Convert.ToByte(Math.Clamp(rgb.G * 255, 0, 255));
                var denormalizedB = Convert.ToByte(Math.Clamp(rgb.B * 255, 0, 255));
                pixels[y, x] = new Rgba32(denormalizedR, denormalizedG, denormalizedB);
            }
        }
    }

    private static double GetDenormalizedLab(double x, double min, double range)
    {
        return ((x / 255) * range) + min;
    }

    class LabInformation
    {
        public IColorConverter<RGBColor, LabColor> RGBToLab;
        public IColorConverter<LabColor, RGBColor> LabToRGB;

        public double minL;
        public double minA;
        public double minB;

        private double maxL = double.MinValue;
        private double maxA = double.MinValue;
        private double maxB = double.MinValue;

        public double rangeL;
        public double rangeA;
        public double rangeB;

        public LabInformation()
        {
            var rgbWorkingSpace = RGBWorkingSpaces.sRGB;
            RGBToLab = new ConverterBuilder().FromRGB(rgbWorkingSpace).ToLab(Illuminants.D50).Build();
            LabToRGB = new ConverterBuilder().FromLab(Illuminants.D50).ToRGB(rgbWorkingSpace).Build();

            Calculate();
        }

        // Source: https://stackoverflow.com/a/19099064/13279557
        private void Calculate()
        {
            minL = double.MaxValue;
            minA = double.MaxValue;
            minB = double.MaxValue;

            for (double r = 0; r < 256; ++r)
            {
                Console.Write(String.Format("\rCalculating Lab min, max and range. Progress: {0}/255", r));

                for (double g = 0; g < 256; ++g)
                    for (double b = 0; b < 256; ++b)
                    {
                        var rgb = new RGBColor(r / 255, g / 255, b / 255);
                        var lab = RGBToLab.Convert(rgb);

                        minL = Math.Min(minL, lab.L);
                        minA = Math.Min(minA, lab.a);
                        minB = Math.Min(minB, lab.b);

                        maxL = Math.Max(maxL, lab.L);
                        maxA = Math.Max(maxA, lab.a);
                        maxB = Math.Max(maxB, lab.b);
                    }
            }

            Console.Write("\n");

            rangeL = maxL - minL;
            rangeA = maxA - minA;
            rangeB = maxB - minB;
        }

        public void Print()
        {
            Console.WriteLine(String.Format("L is always between [{0}, {1}], range {2}", minL, maxL, rangeL));
            Console.WriteLine(String.Format("a is always between [{0}, {1}], range {2}", minA, maxA, rangeA));
            Console.WriteLine(String.Format("b is always between [{0}, {1}], range {2}", minB, maxB, rangeB));
        }
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
