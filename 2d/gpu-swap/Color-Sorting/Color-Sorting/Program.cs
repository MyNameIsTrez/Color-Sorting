using Colourful;
using ComputeSharp;
using System.Diagnostics;
using System.Drawing;
using System.Text.Json;

static public class ShuffleExtension
{
    private static Random rng = new Random(42); // TODO: Make preseeding an option, instead of always doing it

    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    // The idea is to help the GPU out by sorting every pair in list by the first value in the pair
    /*public static void SortPairs(this IList<int> list)
    {
        var pairs = new List<List<int>>(list.Count / 2);

        for (int i = 0; i < list.Count / 2; i++)
        {
            pairs.Add(new List<int>() { list[i * 2], list[i * 2 + 1] });
        }

        pairs.Sort((l, r) => l[0] - r[0]);
        */
    /*
    Console.WriteLine("list[0]: {0}, list[1]: {1}", list[0], list[1]);
    Console.WriteLine("pairs[0][0]: {0}, pairs[0][1]: {1}", pairs[0][0], pairs[0][1]);
    Console.WriteLine("pairs[1][0]: {0}, pairs[1][1]: {1}", pairs[1][0], pairs[1][1]);
    Console.WriteLine("pairs[2][0]: {0}, pairs[2][1]: {1}", pairs[2][0], pairs[2][1]);
    Console.WriteLine("pairs[3][0]: {0}, pairs[3][1]: {1}", pairs[3][0], pairs[3][1]);
    */
    /*
    for (int i = 0; i<list.Count / 2; i++)
    {
        list[i * 2] = pairs[i][0];
        list[i * 2 + 1] = pairs[i][1];
    }
    */
    /*
    Console.WriteLine("list[0]: {0}, list[1]: {1}", list[0], list[1]);
    Console.WriteLine("list[2]: {0}, list[3]: {1}", list[2], list[3]);
    */
    /*
}
*/
}

internal class Program
{
    const int ITERATIONS = 5;

    // WARNING: The max KERNEL_RADIUS value that should ever be used is 7!
    // TODO: Actually test whether what happens when the value is set higher
    //
    // The reason for this is that each pixel stores a sum of all the RGB values of its neighbors, for caching purposes
    // The largest pixel type that can be used is Rgba64, so 16 bits per color channel
    // If you take a KERNEL_RADIUS of 8, you get:
    // 8 * 2 + 1 = 17 // side length
    // 17^2 = 289 // area
    // 289 - 1 = 288 // area minus center
    // 288 * 255 = 73440 // times the maximum difference between the center and a neighbor, which is greater than the limit of 65535!
    const int KERNEL_RADIUS = 15;

    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/debug.png";
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/output/big_palette_r15_i100_t1671399405.png";
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/ultra_20000.png";
    const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/big_palette.png";
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/cat.jpg";
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/palette.bmp";
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/10x10_palette.bmp";
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/rainbow.png";
    //const string INPUT_IMAGE_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/shouldnt-swap.png";

    const string OUTPUT_IMAGES_DIRECTORY_PATH = "I:/Programming/Color-Sorting/2d/gpu-swap/Color-Sorting/Color-Sorting/output";

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

        Console.WriteLine("Allocating pixels GPU textures...");
        using var texture = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<Rgba32, float4>(pixels);

        var indicesList = Enumerable.Range(0, width * height).ToList();

        int pairCount = indicesList.Count / 2;

        using var indicesBuffer = GraphicsDevice.GetDefault().AllocateReadWriteBuffer(indicesList.ToArray());

        using var readTexture = GraphicsDevice.GetDefault().AllocateReadOnlyTexture2D<Rgba32, float4>(pixels);
        using var writeTexture = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<Rgba32, float4>(pixels);

        var initialNeighborTotals = GetInitialNeighborTotals(pixels, width, height);
        using var neighborTotalsReadTexture = GraphicsDevice.GetDefault().AllocateReadOnlyTexture2D<Rgba64, float4>(initialNeighborTotals);

        var emptyChangeWriteArray = new Rgba64[height, width];
        // TODO: Initializing here with emptyChangeWriteArray may not be necessary since it's already done in the for-loop below?
        using var changeWriteTexture = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<Rgba64, float4>(emptyChangeWriteArray);

        var timer = new Stopwatch();
        timer.Start();

        for (int i = 0; i < ITERATIONS; i++)
        {
            Console.Write("\rIteration {0}/{1}...", i + 1, ITERATIONS);

            var iterationStopwatch = new Stopwatch();
            iterationStopwatch.Start();

            var shuffleStopwatch = new Stopwatch();
            shuffleStopwatch.Start();
            indicesList.Shuffle();
            shuffleStopwatch.Stop();
            Console.WriteLine("\n{0} ticks shuffle", shuffleStopwatch.ElapsedTicks);

            /*var sortPairsStopwatch = new Stopwatch();
            sortPairsStopwatch.Start();
            indicesList.SortPairs();
            sortPairsStopwatch.Stop();
            Console.WriteLine("\n{0} ticks sort pairs", sortPairsStopwatch.ElapsedTicks);*/

            var copyIndicesListStopwatch = new Stopwatch();
            copyIndicesListStopwatch.Start();
            // TODO: Try turning indicesList into indicesArray
            indicesBuffer.CopyFrom(indicesList.ToArray());
            copyIndicesListStopwatch.Stop();
            Console.WriteLine("{0} ticks copy indicesList", copyIndicesListStopwatch.ElapsedTicks);

            var copyFromPixelsStopWatch = new Stopwatch();
            copyFromPixelsStopWatch.Start();
            readTexture.CopyFrom(pixels);
            copyFromPixelsStopWatch.Stop();
            Console.WriteLine("{0} ticks copy from pixels", copyFromPixelsStopWatch.ElapsedTicks);

            var clearChangeWriteTextureStopWatch = new Stopwatch();
            clearChangeWriteTextureStopWatch.Start();
            // TODO: This is really slow for some reason, so try bzero'ing it instead
            changeWriteTexture.CopyFrom(emptyChangeWriteArray);
            clearChangeWriteTextureStopWatch.Stop();
            Console.WriteLine("{0} ticks clearing changeWriteTexture", clearChangeWriteTextureStopWatch.ElapsedTicks);

            var shaderStopwatch = new Stopwatch();
            shaderStopwatch.Start();
            GraphicsDevice.GetDefault().For(pairCount, new SwapComputeShader(indicesBuffer, readTexture, writeTexture, neighborTotalsReadTexture, changeWriteTexture, width, height, KERNEL_RADIUS));

            // TODO: REMOVE THIS DEBUG STUFF
            /*
            var changeArray2 = new Rgba64[height, width];
            changeWriteTexture.CopyTo(changeArray2);
            changeArray2[0, 4].R = (ushort)(changeArray2[0, 4].R * 255 / 65535);
            changeArray2[0, 4].G = (ushort)(changeArray2[0, 4].G * 255 / 65535);
            changeArray2[0, 4].B = (ushort)(changeArray2[0, 4].B * 255 / 65535);
            */

            shaderStopwatch.Stop();
            Console.WriteLine("{0} ticks shader", shaderStopwatch.ElapsedTicks);

            var copyToPixelsStopwatch = new Stopwatch();
            copyToPixelsStopwatch.Start();
            writeTexture.CopyTo(pixels);
            copyToPixelsStopwatch.Stop();
            Console.WriteLine("{0} ticks copy to pixels", copyToPixelsStopwatch.ElapsedTicks);

            var updateNeighborTotalsStopwatch = new Stopwatch();
            updateNeighborTotalsStopwatch.Start();
            UpdateNeighborTotals(neighborTotalsReadTexture, changeWriteTexture, width, height);
            updateNeighborTotalsStopwatch.Stop();
            Console.WriteLine("{0} ticks updating neighbor totals", updateNeighborTotalsStopwatch.ElapsedTicks);

            iterationStopwatch.Stop();
            Console.WriteLine("{0} ticks per iteration", iterationStopwatch.ElapsedTicks);
        }

        Console.Write("\n");

        timer.Stop();
        Console.WriteLine("Iteration time: {0:%h} hours, {0:%m} minutes, {0:%s} seconds", timer.Elapsed);

        Console.WriteLine("Lab denormalizing pixels...");

        LabDenormalizePixels();

        using var textureResult = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<Rgba32, float4>(pixels);
        Console.WriteLine("Saving result...");
        Directory.CreateDirectory(OUTPUT_IMAGES_DIRECTORY_PATH);
        var filename = String.Format("{0}_r{1}_i{2}_t{3}.png", Path.GetFileNameWithoutExtension(INPUT_IMAGE_PATH), KERNEL_RADIUS, ITERATIONS, DateTimeOffset.Now.ToUnixTimeSeconds());
        textureResult.Save(Path.Combine(OUTPUT_IMAGES_DIRECTORY_PATH, filename));
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

    public static Rgba64[,] GetInitialNeighborTotals(Rgba32[,] pixels, int width, int height)
    {
        var neighborTotals = new Rgba64[height, width];

        for (int y = 0; y < height; ++y)
        {
            Console.Write(String.Format("\rGetting neighbor RGB totals. Progress: {0}/{1}", y + 1, height));

            for (int x = 0; x < width; ++x)
            {
                // TODO: Probably not necessary
                neighborTotals[y, x].R = 0;
                neighborTotals[y, x].G = 0;
                neighborTotals[y, x].B = 0;

                for (int dy = -KERNEL_RADIUS; dy <= KERNEL_RADIUS; dy++)
                {
                    if (y + dy < 0 || y + dy >= height)
                        continue;

                    for (int dx = -KERNEL_RADIUS; dx <= KERNEL_RADIUS; dx++)
                    {
                        if (x + dx < 0 || x + dx >= width || (dx == 0 && dy == 0))
                            continue;

                        var neighbor = pixels[y + dy, x + dx];
                        neighborTotals[y, x].R += neighbor.R;
                        neighborTotals[y, x].G += neighbor.G;
                        neighborTotals[y, x].B += neighbor.B;
                    }
                }
            }
        }

        Console.Write("\n");

        return neighborTotals;
    }

    public static void UpdateNeighborTotals(ReadOnlyTexture2D<Rgba64, float4> neighborTotalsReadTexture, ReadWriteTexture2D<Rgba64, float4> changeWriteTexture, int width, int height)
    {
        var neighborTotalsArray = new Rgba64[height, width];
        neighborTotalsReadTexture.CopyTo(neighborTotalsArray);

        var changeArray = new Rgba64[height, width];
        changeWriteTexture.CopyTo(changeArray);

        /*
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                // TODO: Verify this is correct with a 2x2 debug input image
                neighborTotalsArray[y, x].R = (ushort)(neighborTotalsArray[y, x].R * 255 / 65535);
                neighborTotalsArray[y, x].G = (ushort)(neighborTotalsArray[y, x].G * 255 / 65535);
                neighborTotalsArray[y, x].B = (ushort)(neighborTotalsArray[y, x].B * 255 / 65535);
            }
        }
        */

        // TODO: Instead of running this on every single cycle, think of a smart algorithm that only recalculates neighborTotals for pixels that got swapped

        int i = 0;

        for (int y = 0; y < height; ++y)
        {
            //Console.Write(String.Format("\rUpdating neighbor totals. Progress: {0}/{1}", y + 1, height));

            for (int x = 0; x < width; ++x)
            {
                Rgba64 change = changeArray[y, x];

                if (change.R == 0 && change.G == 0 && change.B == 0)
                    continue;

                // TODO: With debug.png the changeR/G/B values for (x=0; y=0) should be (189, -71, -49)
                // TODO: Change these back to ints?
                float changeR = (float)change.R - (65535f / 2) - 0.5f;
                float changeG = (float)change.G - (65535f / 2) - 0.5f;
                float changeB = (float)change.B - (65535f / 2) - 0.5f;

                i++;

                for (int dy = -KERNEL_RADIUS; dy <= KERNEL_RADIUS; dy++)
                {
                    if (y + dy < 0 || y + dy >= height)
                        continue;

                    for (int dx = -KERNEL_RADIUS; dx <= KERNEL_RADIUS; dx++)
                    {
                        if (x + dx < 0 || x + dx >= width || (dx == 0 && dy == 0))
                            continue;

                        neighborTotalsArray[y + dy, x + dx].R = (ushort)(neighborTotalsArray[y + dy, x + dx].R + changeR);
                        neighborTotalsArray[y + dy, x + dx].G = (ushort)(neighborTotalsArray[y + dy, x + dx].G + changeG);
                        neighborTotalsArray[y + dy, x + dx].B = (ushort)(neighborTotalsArray[y + dy, x + dx].B + changeB);
                    }
                }
            }
        }

        Console.WriteLine("Changes {0}%", (double)i / (width * height) * 100);

        //Console.Write("\n");

        neighborTotalsReadTexture.CopyFrom(neighborTotalsArray);
    }
}

[AutoConstructor]
// [EmbeddedBytecode(DispatchAxis.X)] // Doesn't seem to speed the program up
public readonly partial struct SwapComputeShader : IComputeShader
{
    public readonly ReadWriteBuffer<int> indices;

    public readonly IReadOnlyNormalizedTexture2D<float4> readTexture;
    public readonly IReadWriteNormalizedTexture2D<float4> writeTexture;

    public readonly IReadOnlyNormalizedTexture2D<float4> neighborTotalsReadTexture;
    public readonly IReadWriteNormalizedTexture2D<float4> changeWriteTexture;

    public readonly int width;
    public readonly int height;

    public readonly int KERNEL_RADIUS;

    public void Execute()
    {
        int aIndex1D = indices[ThreadIds.X * 2 + 0];
        int bIndex1D = indices[ThreadIds.X * 2 + 1];

        int2 aIndex = new int2(GetX(aIndex1D), GetY(aIndex1D));
        int2 bIndex = new int2(GetX(bIndex1D), GetY(bIndex1D));

        float4 a = readTexture[aIndex];
        float4 b = readTexture[bIndex];

        int score = GetScoreDifference(aIndex, a, b) + GetScoreDifference(bIndex, b, a);

        // If swapping pixels `a` and `b` would improve the image, do the swap
        if (score < 0)
        {
            writeTexture[aIndex] = b;
            writeTexture[bIndex] = a;


            //changeWriteTexture[aIndex] = 32768f / 65535 + (a * 255 / 65535);

            changeWriteTexture[aIndex] = 32768f / 65535 + (-a * 255 / 65535) + (b * 255 / 65535);
            changeWriteTexture[bIndex] = 32768f / 65535 + (-b * 255 / 65535) + (a * 255 / 65535);

            //changeWriteTexture[aIndex] = 0.5f + (-a * 255 / 65535) + (b * 255 / 65535);
            //changeWriteTexture[bIndex] = 0.5f + (-b * 255 / 65535) + (a * 255 / 65535);


            //ChangeNeighborTotalsTexture(aIndex, a, b);
            //ChangeNeighborTotalsTexture(bIndex, b, a);
        }

        // TODO: Possibly faster
        //writeTexture[aIndex] = score < 0 ? b : a;
        //writeTexture[bIndex] = score < 0 ? a : b;
    }

    private int GetX(int index)
    {
        return index % width;
    }

    private int GetY(int index)
    {
        return index / width;
    }

    private int GetScoreDifference(int2 centerIndex, float4 oldCenterPixel, float4 newCenterPixel)
    {
        // TODO: Probably need to have a separate neighborTotals read and write texture!

        //int neighborCount;

        //int kernelSideLength = KERNEL_RADIUS + 1 + KERNEL_RADIUS;

        /*
        // If centerIndex is a corner
        if ((centerIndex.X == 0 && centerIndex.Y == 0)
         || (centerIndex.X == width - 1 && centerIndex.Y == 0)
         || (centerIndex.X == 0 && centerIndex.Y == height - 1)
         || (centerIndex.X == width - 1 && centerIndex.Y == height - 1)
         )
        {
            int foo = 1 + KERNEL_RADIUS;
            neighborCount = foo * foo - 1;
        }
        // Else if centerIndex is an edge
        else if (centerIndex.X == 0 || centerIndex.X == width - 1 || centerIndex.Y == 0 || centerIndex.Y == height - 1)
        {
            neighborCount = kernelSideLength * (1 + KERNEL_RADIUS) - 1;
        }
        else
        {
            neighborCount = kernelSideLength * kernelSideLength - 1;
        }
        */

        int up = Hlsl.Min(centerIndex.Y, KERNEL_RADIUS);
        int down = Hlsl.Min(height - 1 - centerIndex.Y, KERNEL_RADIUS);

        int left = Hlsl.Min(centerIndex.X, KERNEL_RADIUS);
        int right = Hlsl.Min(width - 1 - centerIndex.X, KERNEL_RADIUS);

        int neighborCount = (up + 1 + down) * (left + 1 + right) - 1;

        float4 neighborsAverage = neighborTotalsReadTexture[centerIndex] * 65535 / neighborCount;
        //float neighborsAverageR = neighborTotalsReadTexture[centerIndex].R * 65535 / neighborCount;
        //float neighborsAverageG = neighborTotalsReadTexture[centerIndex].G * 65535 / neighborCount;
        //float neighborsAverageB = neighborTotalsReadTexture[centerIndex].B * 65535 / neighborCount;

        return -GetColorDifference(oldCenterPixel, neighborsAverage) + GetColorDifference(newCenterPixel, neighborsAverage);

        //return -GetColorDifference(oldCenterPixel, neighborsAverageR, neighborsAverageG, neighborsAverageB)
        //    + GetColorDifference(newCenterPixel, neighborsAverageR, neighborsAverageG, neighborsAverageB);
    }

    private int GetColorDifference(float4 centerPixel, float4 neighborsAverage)
    //private int GetColorDifference(float4 centerPixel, float neighborsAverageR, float neighborsAverageG, float neighborsAverageB)
    {
        var l = (centerPixel.R * 255) - neighborsAverage.R;
        var a = (centerPixel.G * 255) - neighborsAverage.G;
        var b = (centerPixel.B * 255) - neighborsAverage.B;
        //var l = (centerPixel.R * 255) - neighborsAverageR;
        //var a = (centerPixel.G * 255) - neighborsAverageG;
        //var b = (centerPixel.B * 255) - neighborsAverageB;

        return (int)(l * l + a * a + b * b);
    }

    /*
    public void ChangeNeighborTotalsTexture(int2 centerIndex, float4 oldCenterPixel, float4 newCenterPixel)
    {
        float4 change = -oldCenterPixel + newCenterPixel;

        for (int dy = -KERNEL_RADIUS; dy <= KERNEL_RADIUS; dy++)
        {
            if (centerIndex.Y + dy < 0 || centerIndex.Y + dy >= height)
                continue;

            for (int dx = -KERNEL_RADIUS; dx <= KERNEL_RADIUS; dx++)
            {
                if (centerIndex.X + dx < 0 || centerIndex.X + dx >= width || (dx == 0 && dy == 0))
                    continue;

                int2 neighborIndex = new Int2(centerIndex.X + dx, centerIndex.Y + dy);

                // TODO: Not sure if correct
                neighborTotalsTexture[neighborIndex] += change * 255 / 65535;
                //neighborTotalsTexture[neighborIndex].R += change.R * 255 / 65535;
                //neighborTotalsTexture[neighborIndex].G += change.G * 255 / 65535;
                //neighborTotalsTexture[neighborIndex].B += change.B * 255 / 65535;
                //neighborTotalsTexture[neighborIndex].R += (-oldCenterPixel.R + newCenterPixel.R) * 255 / 65535;
                //neighborTotalsTexture[neighborIndex].G += (-oldCenterPixel.G + newCenterPixel.G) * 255 / 65535;
                //neighborTotalsTexture[neighborIndex].B += (-oldCenterPixel.B + newCenterPixel.B) * 255 / 65535;
            }
        }
    }
    */
}