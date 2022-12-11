using Colourful;
using ComputeSharp;
using System.Drawing;
using System.Text.Json;

internal class Program
{
    const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/cat.jpg";

    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/palette.bmp";
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/10x10_palette.bmp";
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/rainbow.png";
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/shouldnt-swap.png";

    const string OUTPUT_IMAGES_DIRECTORY_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting";

    const string LAB_INFO_JSON_FILE_PATH = "LabInfo.json";

    static private LabInformation labInfo;
    static private Bitmap img;
    static private Rgba32[,] pixels;

    private static void Main(string[] args)
    {
        img = new Bitmap(INPUT_IMAGE_PATH);
        var width = img.Width;
        var height = img.Height;


        labInfo = new LabInformation();
        labInfo.Init();

        // TODO: Try using Rgb everywhere instead, to save bytes.
        pixels = new Rgba32[height, width];

        Console.WriteLine("Lab normalizing pixels...");
        LabNormalizePixels();

        /*
        for (int y = 0; y < img.Height; ++y)
        {
            for (int x = 0; x < img.Width; ++x)
            {
                var pixel = img.GetPixel(x, y);
                pixels[y, x] = new Rgba32(pixel.R, pixel.G, pixel.B);
            }
        }
        */

        Console.WriteLine("Allocating pixels GPU texture...");
        using var texture = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<Rgba32, float4>(pixels);


        var rnd = new Random();

        var indicesList = Enumerable.Range(0, width * height).ToList();


        for (int i = 0; i < 10000; i++)
        {
            var availableCount = width * height;

            var positions = indicesList.ToList();
            var available = indicesList.ToList();
            var availableIndices = new List<int>();

            //Console.Clear();
            //PrintGrid(positions, availableCount, width, height);
            while (availableCount > 0)
            {
                //Thread.Sleep(500);

                var availableIndex = available[rnd.Next(availableCount)];
                availableIndices.Add(availableIndex);

                //availableCount = MarkUnavailable(index, available, positions, availableCount);
                availableCount = MarkNeighborsAndSelfUnavailable(availableIndex, available, positions, availableCount, width, height);

                //Console.Clear();
                //Console.WriteLine(availableIndex.ToString("D2"));
                //Console.WriteLine(availableCount);
                //PrintGrid(positions, availableCount, width, height);
            }


            //var availableIndices = new List<int> { 1, 2 };
            //availableIndices.ForEach(Console.WriteLine);


            // If `availableIndices` is `[ A, B, C ]`, then `[ A, B ]` is the only pair of indices that will be swapped
            using var availableIndicesBuffer = GraphicsDevice.GetDefault().AllocateReadOnlyBuffer(availableIndices.ToArray());
            GraphicsDevice.GetDefault().For((int)(availableIndices.Count / 2), new SwapComputeShader(availableIndicesBuffer, texture, width, height));

        }

        //using var texture = GraphicsDevice.GetDefault().AllocateReadWriteBuffer(pixels.ToArray());
        //using var texture = GraphicsDevice.GetDefault().LoadReadWriteTexture2D<Rgba32, float4>("I:/Programming/Color-Sorting/Color-Sorting/palette.bmp");

        //GraphicsDevice.GetDefault().For(texture.Width, texture.Height, new GrayscaleEffect(texture));

        Console.WriteLine("Lab denormalizing pixels...");
        pixels = texture.ToArray();
        LabDenormalizePixels();
        using var textureResult = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<Rgba32, float4>(pixels);
        Console.WriteLine("Saving result...");
        textureResult.Save(Path.Combine(OUTPUT_IMAGES_DIRECTORY_PATH, "1.png"));

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

        public double minL { get; set; }
        public double minA { get; set; }
        public double minB { get; set; }

        private double maxL = double.MinValue;
        private double maxA = double.MinValue;
        private double maxB = double.MinValue;

        public double rangeL { get; set; }
        public double rangeA { get; set; }
        public double rangeB { get; set; }

        public void Init()
        {
            var rgbWorkingSpace = RGBWorkingSpaces.sRGB;
            RGBToLab = new ConverterBuilder().FromRGB(rgbWorkingSpace).ToLab(Illuminants.D50).Build();
            LabToRGB = new ConverterBuilder().FromLab(Illuminants.D50).ToRGB(rgbWorkingSpace).Build();

            if (File.Exists(LAB_INFO_JSON_FILE_PATH))
            {
                string jsonString = File.ReadAllText(LAB_INFO_JSON_FILE_PATH);

                LabInformation deserializedLabInfo = JsonSerializer.Deserialize<LabInformation>(jsonString)!;

                minL = deserializedLabInfo.minL;
                minA = deserializedLabInfo.minA;
                minB = deserializedLabInfo.minB;

                rangeL = deserializedLabInfo.rangeL;
                rangeA = deserializedLabInfo.rangeA;
                rangeB = deserializedLabInfo.rangeB;
            }
            else
            {
                Calculate();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(LAB_INFO_JSON_FILE_PATH, json);
            }
        }

        // Source: https://stackoverflow.com/a/19099064/13279557
        private void Calculate()
        {
            minL = double.MaxValue;
            minA = double.MaxValue;
            minB = double.MaxValue;

            // TODO: Change this back to 256!!!
            for (double r = 0; r < 256; ++r)
            {
                Console.Write(String.Format("\rCalculating Lab min, max and range. The result will be saved to a file. Progress: {0}/255", r));

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
public readonly partial struct SwapComputeShader : IComputeShader
{
    public readonly ReadOnlyBuffer<int> availableIndicesBuffer;

    public readonly IReadWriteNormalizedTexture2D<float4> texture;

    public readonly int width;
    public readonly int height;

    public void Execute()
    {
        int aIndex1D = availableIndicesBuffer[ThreadIds.X * 2 + 0];
        int bIndex1D = availableIndicesBuffer[ThreadIds.X * 2 + 1];

        int2 aIndex = new int2(getX(aIndex1D), getY(aIndex1D));
        int2 bIndex = new int2(getX(bIndex1D), getY(bIndex1D));

        float4 a = texture[aIndex];
        float4 b = texture[bIndex];

        int score = 0;

        int change;

        /*
        change = getSelfPlusNeighborScore(aIndex);
        score -= change;
        texture[aIndex] = b;
        change = getSelfPlusNeighborScore(aIndex);
        score += change;

        change = getSelfPlusNeighborScore(bIndex);
        score -= change;
        texture[bIndex] = a;
        change = getSelfPlusNeighborScore(bIndex);
        score += change;
        */

        //texture[new int2(0, 3)] = new float4(aIndex.X / 256f, aIndex.Y / 256f, 0, 1);
        //texture[new int2(0, 4)] = new float4(bIndex.X / 256f, bIndex.Y / 256f, 0, 1);

        change = getSelfPlusNeighborScore(aIndex);
        //texture[new int2(0, 3)] = new float4(change > 0 ? 1 : (change == 0 ? 0.5f : 0), change / 256f, 0, 1);


        score -= change;
        texture[aIndex] = b;
        change = getSelfPlusNeighborScore(aIndex);
        //texture[new int2(0, 4)] = new float4(change > 0 ? 1 : (change == 0 ? 0.5f : 0), 0, 0, 1);
        score += change;

        change = getSelfPlusNeighborScore(bIndex);
        //texture[new int2(0, 5)] = new float4(change > 0 ? 1 : (change == 0 ? 0.5f : 0), 0, 0, 1);
        score -= change;
        texture[bIndex] = a;
        change = getSelfPlusNeighborScore(bIndex);
        //texture[new int2(0, 6)] = new float4(change > 0 ? 1 : (change == 0 ? 0.5f : 0), 0, 0, 1);
        score += change;


        // If swapping pixels `a` and `b` worsened the image, revert the swap
        if (score > 0)
        {
            texture[new int2(0, 15)] = new float4(1, 0, 0, 1);

            texture[aIndex] = a;
            texture[bIndex] = b;
        }
        else
        {
            texture[new int2(0, 15)] = new float4(0, 1, 0, 1);
        }

    }

    private int getX(int index)
    {
        return index % width;
    }

    private int getY(int index)
    {
        return index / width;
    }

    private int getSelfPlusNeighborScore(int2 index)
    {
        int score = 0;
        int additionalScore;

        int i = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            if (index.Y + dy == -1 || index.Y + dy == height)
                continue;

            for (int dx = -1; dx <= 1; dx++)
            {
                if (index.X + dx == -1 || index.X + dx == width)
                    continue;

                additionalScore = getScore(index + new int2(dx, dy));
                score += additionalScore;
                //texture[new int2(0, 3 + i)] = new float4((index.X + dx + (index.Y + dy) * width) / 256f, 0, 0, 1);
                //texture[new int2(0, 3 + i)] = new float4(additionalScore / 256f, 0, 0, 1);
                i++;
            }
        }

        return (score);
    }

    private int getScore(int2 centerIndex)
    {
        float4 centerPixel = texture[centerIndex];
        int score = 0;

        int i = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            if (centerIndex.Y + dy == -1 || centerIndex.Y + dy == height)
                continue;

            for (var dx = -1; dx <= 1; dx++)
            {
                // TODO: Is the check for itself with `(dx == 0 && dy == 0)` really necessary?
                if (centerIndex.X + dx == -1 || centerIndex.X + dx == width || (dx == 0 && dy == 0))
                    continue;

                //texture[new int2(0, 3 + i)] = new float4(dx / 256f, dy / 256f, 0, 1);

                float4 neighborPixel = texture[centerIndex + new int2(dx, dy)];
                score += getColorDifference(centerPixel, neighborPixel, i);
                i++;
            }
        }

        return score;
    }

    private int getColorDifference(float4 c1, float4 c2, int i)
    {
        var l = (c1.R * 255) - (c2.R * 255);
        var a = (c1.G * 255) - (c2.G * 255);
        var b = (c1.B * 255) - (c2.B * 255);

        //texture[new int2(0, 3 + i)] = new float4(l / 255, 0, 0, 1);
        //texture[new int2(1, 3 + i)] = new float4(a / 255, 0, 0, 1);
        //texture[new int2(2, 3 + i)] = new float4(b / 255, 0, 0, 1);

        //texture[new int2(0, 3 + i)] = new float4((l * l) / 255f, 0, 0, 1);
        //texture[new int2(1, 3 + i)] = new float4((a * a) / 255f, 0, 0, 1);
        //texture[new int2(2, 3 + i)] = new float4((b * b) / 255f, 0, 0, 1);

        return (int)(l * l + a * a + b * b);
    }
}

/*
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
*/