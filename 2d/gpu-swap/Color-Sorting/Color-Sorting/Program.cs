using Colourful;
using ComputeSharp;
using System.Drawing;

internal class Program
{
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/cat.jpg";

    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/palette.bmp";
    const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/10x10_palette.bmp";

    const string OUTPUT_IMAGES_DIRECTORY_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting";

    static private LabInformation LabInfo;
    static private Bitmap palette;
    static private Rgba32[,] pixels;

    private static void Main(string[] args)
    {
        palette = new Bitmap(INPUT_IMAGE_PATH);
        var width = palette.Width;
        var height = palette.Height;

        var positions = new List<int>(Enumerable.Range(0, width * height));

        var available = positions.ToList();
        var availableCount = width * height;

        var rnd = new Random();

        Console.Clear();
        PrintGrid(positions, availableCount, width, height);
        while (availableCount > 0)
        {
            Thread.Sleep(50);

            var index = available[rnd.Next(availableCount)];
            availableCount = MarkUnavailable(index, available, positions, availableCount);
            Console.Clear();
            Console.WriteLine(index.ToString("D2"));
            Console.WriteLine(availableCount);
            PrintGrid(positions, availableCount, width, height);
        }

        /*
        LabInfo = new LabInformation();
        pixels = new Rgba32[palette.Height, palette.Width];

        Console.WriteLine("Lab normalizing pixels...");
        LabNormalizePixels();

        Console.WriteLine("Lab denormalizing pixels...");
        LabDenormalizePixels();

        Console.WriteLine("Allocating pixels GPU texture...");
        using var texture = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<Rgba32, float4>(pixels);

        //using var texture = GraphicsDevice.GetDefault().AllocateReadWriteBuffer(pixels.ToArray());
        //using var texture = GraphicsDevice.GetDefault().LoadReadWriteTexture2D<Rgba32, float4>("I:/Programming/Color-Sorting/Color-Sorting/palette.bmp");

        //GraphicsDevice.GetDefault().For(texture.Width, texture.Height, new GrayscaleEffect(texture));

        Console.WriteLine("Saving result...");
        texture.Save(Path.Combine(OUTPUT_IMAGES_DIRECTORY_PATH, "1.png"));
        */
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
                var index = x + y * width;
                Console.Write("|{0}", index.ToString("D2"));
            }

            Console.WriteLine("|");

            for (var x = 0; x < width; ++x)
            {
                var index = x + y * width;
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
            var available_index = positions[index];

            var a = available[available_index];
            var b = available[availableCount - 1];

            available[available_index] = b;
            available[availableCount - 1] = a;

            positions[index] = availableCount - 1;
            positions[b] = available_index;

            --availableCount;
        }

        return availableCount;
    }

    /*
     * See the MarkUnavailable() example in its documentation.
     */
    private static bool IsAvailable(int index, List<int> positions, int availableCount)
    {
        var available_index = positions[index];
        return available_index < availableCount;
    }

    private static void LabNormalizePixels()
    {
        for (int y = 0; y < palette.Height; ++y)
        {
            for (int x = 0; x < palette.Width; ++x)
            {
                var pixel = palette.GetPixel(x, y);

                var rgb = new RGBColor(Convert.ToDouble(pixel.R) / 255, Convert.ToDouble(pixel.G) / 255, Convert.ToDouble(pixel.B) / 255);
                var lab = LabInfo.RGBToLab.Convert(rgb);

                var normalized_L = GetNormalizedLab(lab.L, LabInfo.MinL, LabInfo.RangeL);
                var normalized_A = GetNormalizedLab(lab.a, LabInfo.MinA, LabInfo.RangeA);
                var normalized_B = GetNormalizedLab(lab.b, LabInfo.MinB, LabInfo.RangeB);
                pixels[y, x] = new Rgba32(normalized_L, normalized_A, normalized_B);
            }
        }
    }

    private static byte GetNormalizedLab(double x, double min, double range)
    {
        return Convert.ToByte(((x - min) / range) * 255);
    }

    private static void LabDenormalizePixels()
    {
        for (int y = 0; y < palette.Height; ++y)
        {
            for (int x = 0; x < palette.Width; ++x)
            {
                var pixel = pixels[y, x];

                var L = GetDenormalizedLab(pixel.R, LabInfo.MinL, LabInfo.RangeL);
                var A = GetDenormalizedLab(pixel.G, LabInfo.MinA, LabInfo.RangeA);
                var B = GetDenormalizedLab(pixel.B, LabInfo.MinB, LabInfo.RangeB);
                var lab = new LabColor(L, A, B);
                var rgb = LabInfo.LabToRGB.Convert(lab);

                var denormalized_r = Convert.ToByte(Math.Clamp(rgb.R * 255, 0, 255));
                var denormalized_g = Convert.ToByte(Math.Clamp(rgb.G * 255, 0, 255));
                var denormalized_b = Convert.ToByte(Math.Clamp(rgb.B * 255, 0, 255));
                pixels[y, x] = new Rgba32(denormalized_r, denormalized_g, denormalized_b);
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

        public double MinL;
        public double MinA;
        public double MinB;

        private double MaxL = double.MinValue;
        private double MaxA = double.MinValue;
        private double MaxB = double.MinValue;

        public double RangeL;
        public double RangeA;
        public double RangeB;

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
            MinL = double.MaxValue;
            MinA = double.MaxValue;
            MinB = double.MaxValue;

            for (double r = 0; r < 256; ++r)
            {
                Console.Write(String.Format("\rCalculating Lab min, max and range. Progress: {0}/255", r));

                for (double g = 0; g < 256; ++g)
                    for (double b = 0; b < 256; ++b)
                    {
                        var rgb = new RGBColor(r / 255, g / 255, b / 255);
                        var lab = RGBToLab.Convert(rgb);

                        MinL = Math.Min(MinL, lab.L);
                        MinA = Math.Min(MinA, lab.a);
                        MinB = Math.Min(MinB, lab.b);

                        MaxL = Math.Max(MaxL, lab.L);
                        MaxA = Math.Max(MaxA, lab.a);
                        MaxB = Math.Max(MaxB, lab.b);
                    }
            }

            Console.Write("\n");

            RangeL = MaxL - MinL;
            RangeA = MaxA - MinA;
            RangeB = MaxB - MinB;
        }

        public void Print()
        {
            Console.WriteLine(String.Format("L is always between [{0}, {1}], range {2}", MinL, MaxL, RangeL));
            Console.WriteLine(String.Format("a is always between [{0}, {1}], range {2}", MinA, MaxA, RangeA));
            Console.WriteLine(String.Format("b is always between [{0}, {1}], range {2}", MinB, MaxB, RangeB));
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
