// Sources:
// https://codegolf.stackexchange.com/a/22326
// https://patrickwu.space/2016/06/12/csharp-color/#rgb2lab
//
// Compile and run: C:/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe /out:simulated-annealing.exe simulated-annealing.cs && ./simulated-annealing.exe
// Create mp4: ffmpeg -f image2 -framerate 80 -i simulated-annealing-output/%d.png -c:v libx264 -pix_fmt yuv420p -vf scale=1024x1024:flags=neighbor -crf 1 palette.mp4

// TODO: Comment this out for performance boost
// #define DEBUG
// #define TRACE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;

class Program
{
    // algorithm settings, feel free to mess with it
    const bool AVERAGE = true;
    const int WIDTH = 16;
    const int HEIGHT = 16;
    const int STARTX = WIDTH/2;
    const int STARTY = HEIGHT/2;
	const string OUTPUT_DIRECTORY_NAME = "simulated-annealing-output";
	const double COOLING_RATE = 0.99999;

    // represent a coordinate
    struct XY
    {
        public int x, y;
        public XY(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        public override int GetHashCode()
        {
            return x ^ y;
        }
        public override bool Equals(object obj)
        {
            var that = (XY)obj;
            return this.x == that.x && this.y == that.y;
        }
    }

	/// <summary>
	/// Structure to define CIE L*a*b*.
	/// </summary>
	public struct CIELab
	{
		/// <summary>
		/// Gets an empty CIELab structure.
		/// </summary>
		public static readonly CIELab Empty = new CIELab();

		private double l;
		private double a;
		private double b;

		// Replicating how Color works:
		// https://referencesource.microsoft.com/#System.Drawing/commonui/System/Drawing/Color.cs,1433
		private static short StateKnownColorValid = 0x0001;
		// private readonly short state;
		public readonly short state;
		public bool IsEmpty {
            get {
                return state == 0;
            }
        }

		public static bool operator ==(CIELab item1, CIELab item2)
		{
			return (
				item1.L == item2.L
				&& item1.A == item2.A
				&& item1.B == item2.B
				);
		}

		public static bool operator !=(CIELab item1, CIELab item2)
		{
			return (
				item1.L != item2.L
				|| item1.A != item2.A
				|| item1.B != item2.B
				);
		}


		/// <summary>
		/// Gets or sets L component.
		/// </summary>
		public double L
		{
			get
			{
				return this.l;
			}
			set
			{
				this.l = value;
			}
		}

		/// <summary>
		/// Gets or sets a component.
		/// </summary>
		public double A
		{
			get
			{
				return this.a;
			}
			set
			{
				this.a = value;
			}
		}

		/// <summary>
		/// Gets or sets a component.
		/// </summary>
		public double B
		{
			get
			{
				return this.b;
			}
			set
			{
				this.b = value;
			}
		}

		public CIELab(double l, double a, double b)
		{
			this.l = l;
			this.a = a;
			this.b = b;
			this.state = StateKnownColorValid;
		}

		public override bool Equals(Object obj)
		{
			if(obj==null || GetType()!=obj.GetType()) return false;

			return (this == (CIELab)obj);
		}

		public override int GetHashCode()
		{
			return L.GetHashCode() ^ a.GetHashCode() ^ b.GetHashCode();
		}

	}

	/// <summary>
	/// Structure to define CIE XYZ.
	/// </summary>
	public struct CIEXYZ
	{
		/// <summary>
		/// Gets an empty CIEXYZ structure.
		/// </summary>
		public static readonly CIEXYZ Empty = new CIEXYZ();
		/// <summary>
		/// Gets the CIE D65 (white) structure.
		/// </summary>
		public static readonly CIEXYZ D65 = new CIEXYZ(0.9505, 1.0, 1.0890);


		private double x;
		private double y;
		private double z;

		public static bool operator ==(CIEXYZ item1, CIEXYZ item2)
		{
			return (
				item1.X == item2.X
				&& item1.Y == item2.Y
				&& item1.Z == item2.Z
				);
		}

		public static bool operator !=(CIEXYZ item1, CIEXYZ item2)
		{
			return (
				item1.X != item2.X
				|| item1.Y != item2.Y
				|| item1.Z != item2.Z
				);
		}

		/// <summary>
		/// Gets or sets X component.
		/// </summary>
		public double X
		{
			get
			{
				return this.x;
			}
			set
			{
				this.x = (value>0.9505)? 0.9505 : ((value<0)? 0 : value);
			}
		}

		/// <summary>
		/// Gets or sets Y component.
		/// </summary>
		public double Y
		{
			get
			{
				return this.y;
			}
			set
			{
				this.y = (value>1.0)? 1.0 : ((value<0)?0 : value);
			}
		}

		/// <summary>
		/// Gets or sets Z component.
		/// </summary>
		public double Z
		{
			get
			{
				return this.z;
			}
			set
			{
				this.z = (value>1.089)? 1.089 : ((value<0)? 0 : value);
			}
		}

		public CIEXYZ(double x, double y, double z)
		{
			this.x = (x>0.9505)? 0.9505 : ((x<0)? 0 : x);
			this.y = (y>1.0)? 1.0 : ((y<0)? 0 : y);
			this.z = (z>1.089)? 1.089 : ((z<0)? 0 : z);
		}

		public override bool Equals(Object obj)
		{
			if(obj==null || GetType()!=obj.GetType()) return false;

			return (this == (CIEXYZ)obj);
		}

		public override int GetHashCode()
		{
			return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
		}

	}

	/// <summary>
	/// RGB structure.
	/// </summary>
	public struct RGB
	{
		/// <summary>
		/// Gets an empty RGB structure;
		/// </summary>
		public static readonly RGB Empty = new RGB();

		private int red;
		private int green;
		private int blue;

		public static bool operator ==(RGB item1, RGB item2)
		{
			return (
				item1.Red == item2.Red
				&& item1.Green == item2.Green
				&& item1.Blue == item2.Blue
				);
		}

		public static bool operator !=(RGB item1, RGB item2)
		{
			return (
				item1.Red != item2.Red
				|| item1.Green != item2.Green
				|| item1.Blue != item2.Blue
				);
		}

		/// <summary>
		/// Gets or sets red value.
		/// </summary>
		public int Red
		{
			get
			{
				return red;
			}
			set
			{
					red = (value>255)? 255 : ((value<0)?0 : value);
			}
		}

		/// <summary>
		/// Gets or sets red value.
		/// </summary>
		public int Green
		{
			get
			{
				return green;
			}
			set
			{
				green = (value>255)? 255 : ((value<0)?0 : value);
			}
		}

		/// <summary>
		/// Gets or sets red value.
		/// </summary>
		public int Blue
		{
			get
			{
				return blue;
			}
			set
			{
				blue = (value>255)? 255 : ((value<0)?0 : value);
			}
		}

		public RGB(int R, int G, int B)
		{
			this.red = (R>255)? 255 : ((R<0)?0 : R);
			this.green = (G>255)? 255 : ((G<0)?0 : G);
			this.blue = (B>255)? 255 : ((B<0)?0 : B);
		}

		public override bool Equals(Object obj)
		{
			if(obj==null || GetType()!=obj.GetType()) return false;

			return (this == (RGB)obj);
		}

		public override int GetHashCode()
		{
			return Red.GetHashCode() ^ Green.GetHashCode() ^ Blue.GetHashCode();
		}
	}

	/// <summary>
	/// XYZ to L*a*b* transformation function.
	/// </summary>
	private static double Fxyz(double t)
	{
		return ((t > 0.008856)? Math.Pow(t, (1.0/3.0)) : (7.787*t + 16.0/116.0));
	}

	/// <summary>
	/// Converts CIEXYZ to CIELab.
	/// </summary>
	public static CIELab XYZtoLab(double x, double y, double z)
	{
		var L = 116.0 * Fxyz( y/CIEXYZ.D65.Y ) -16;
		var A = 500.0 * (Fxyz( x/CIEXYZ.D65.X ) - Fxyz( y/CIEXYZ.D65.Y) );
		var B = 200.0 * (Fxyz( y/CIEXYZ.D65.Y ) - Fxyz( z/CIEXYZ.D65.Z) );

		CIELab lab = new CIELab(L, A, B);

		return lab;
	}

	/// <summary>
	/// Converts RGB to CIE XYZ (CIE 1931 color space)
	/// </summary>
	public static CIEXYZ RGBtoXYZ(int red, int green, int blue)
	{
		// normalize red, green, blue values
		double rLinear = (double)red/255.0;
		double gLinear = (double)green/255.0;
		double bLinear = (double)blue/255.0;

		// convert to a sRGB form
		double r = (rLinear > 0.04045)? Math.Pow((rLinear + 0.055)/(
			1 + 0.055), 2.2) : (rLinear/12.92) ;
		double g = (gLinear > 0.04045)? Math.Pow((gLinear + 0.055)/(
			1 + 0.055), 2.2) : (gLinear/12.92) ;
		double b = (bLinear > 0.04045)? Math.Pow((bLinear + 0.055)/(
			1 + 0.055), 2.2) : (bLinear/12.92) ;

		// converts
		return new CIEXYZ(
			(r*0.4124 + g*0.3576 + b*0.1805),
			(r*0.2126 + g*0.7152 + b*0.0722),
			(r*0.0193 + g*0.1192 + b*0.9505)
			);
	}

	/// <summary>
	/// Converts CIEXYZ to RGB structure.
	/// </summary>
	public static RGB XYZtoRGB(double x, double y, double z)
	{
		double[] Clinear = new double[3];
		Clinear[0] = x*3.2410 - y*1.5374 - z*0.4986; // red
		Clinear[1] = -x*0.9692 + y*1.8760 - z*0.0416; // green
		Clinear[2] = x*0.0556 - y*0.2040 + z*1.0570; // blue

		for(int i=0; i<3; i++)
		{
			Clinear[i] = (Clinear[i]<=0.0031308)? 12.92*Clinear[i] : (
				1+0.055)* Math.Pow(Clinear[i], (1.0/2.4)) - 0.055;
		}

		return new RGB(
			Convert.ToInt32( Double.Parse(String.Format("{0:0.00}",
				Clinear[0]*255.0)) ),
			Convert.ToInt32( Double.Parse(String.Format("{0:0.00}",
				Clinear[1]*255.0)) ),
			Convert.ToInt32( Double.Parse(String.Format("{0:0.00}",
				Clinear[2]*255.0)) )
			);
	}

	/// <summary>
	/// Converts CIELab to CIEXYZ.
	/// </summary>
	public static CIEXYZ LabtoXYZ(double l, double a, double b)
	{
		double delta = 6.0/29.0;

		double fy = (l+16)/116.0;
		double fx = fy + (a/500.0);
		double fz = fy - (b/200.0);

		return new CIEXYZ(
			(fx > delta)? CIEXYZ.D65.X * (fx*fx*fx) : (fx - 16.0/116.0)*3*(
				delta*delta)*CIEXYZ.D65.X,
			(fy > delta)? CIEXYZ.D65.Y * (fy*fy*fy) : (fy - 16.0/116.0)*3*(
				delta*delta)*CIEXYZ.D65.Y,
			(fz > delta)? CIEXYZ.D65.Z * (fz*fz*fz) : (fz - 16.0/116.0)*3*(
				delta*delta)*CIEXYZ.D65.Z
			);
	}

	/// <summary>
	/// Converts CIELab to RGB.
	/// </summary>
	public static RGB LabtoRGB(double l, double a, double b)
	{
		var xyz = LabtoXYZ(l, a, b);
		return XYZtoRGB( xyz.X, xyz.Y, xyz.Z );
	}

	/// <summary>
	/// Converts RGB to CIELab.
	/// </summary>
	public static CIELab RGBtoLab(int red, int green, int blue)
	{
		var xyz = RGBtoXYZ(red, green, blue);
		return XYZtoLab( xyz.X, xyz.Y, xyz.Z );
	}

    static int coldiff(CIELab c1, CIELab c2)
    {
        var l2 = (c1.L - c2.L);
        var a2 = (c1.A - c2.A);
        var b2 = (c1.B - c2.B);
        return (int)(l2 * l2 + a2 * a2 + b2 * b2);
    }

    // gets the neighbors (3..8) of the given coordinate
    static List<XY> getneighbors(int index)
    {
		XY xy = new XY(index % WIDTH, (int)(index / WIDTH));

        var ret = new List<XY>(8);
        for (var dy = -1; dy <= 1; dy++)
        {
            if (xy.y + dy == -1 || xy.y + dy == HEIGHT)
                continue;
            for (var dx = -1; dx <= 1; dx++)
            {
                if (xy.x + dx == -1 || xy.x + dx == WIDTH || (xy.x + dx == 0 && xy.y + dy == 0))
                    continue;
                ret.Add(new XY(xy.x + dx, xy.y + dy));
            }
        }
        return ret;
    }

    // calculates how well a color fits at the given coordinates
    static int get_score(List<CIELab> pixels, int index)
    {
		CIELab c = pixels[index];
        // get the diffs for each neighbor separately
        var diffs = new List<int>(8);
        foreach (var nxy in getneighbors(index))
        {
            var nc = pixels[nxy.x + nxy.y * WIDTH];
            if (!nc.IsEmpty)
                diffs.Add(coldiff(nc, c));
        }

        // average or minimum selection
        if (AVERAGE)
            return (int)diffs.Average();
        else
            return diffs.Min();
    }

	// TODO: Maybe adding "ref" in front of List<CIELab> is an optimization?
	static int get_total_score(List<CIELab> pixels)
	{
		var score = 0;
		for (var y = 0; y < HEIGHT; y++)
			for (var x = 0; x < WIDTH; x++)
				score += get_score(pixels, x + y * WIDTH);
		return (score);
	}

	// /* A simplified explanation of this function:

	// pixels:
	// [4, 3, 5]
	// [9, 5, 6]
	// [4, 2, 9]

	// index_from: 4
	// index_to  : 8

	// old score of index_to: ( (5-9)**2 + (6-9)**2 + (2-9)**2 ) / 3 -> 24.66 -> 24

	// new score of index_to: ( (9-5)**2 + (6-5)**2 + (2-5)**2 ) / 3 -> 8.66 -> 8

	// */
	// // TODO: Maybe adding "ref" in front of List<CIELab> is an optimization?
	// static int get_score(List<CIELab> pixels, int index_from, int index_to)
	// {
	// 	var score_diff = 0;
	// 	// score_diff += get_score(pixels, ); // TODO: Write
	// 	return (score_diff);
	// }

	static int get_self_plus_neighbor_score(List<CIELab> pixels, int index)
	{
		var score = 0;
		var xy = new XY(index % WIDTH, (int)(index / WIDTH));
		for (var dy = -1; dy <= 1; dy++)
		{
			if (xy.y + dy == -1 || xy.y + dy == HEIGHT)
				continue;
			for (var dx = -1; dx <= 1; dx++)
			{
				if (xy.x + dx == -1 || xy.x + dx == WIDTH)
					continue;
				score += get_score(pixels, xy.x + dx + (xy.y + dy) * WIDTH);
			}
		}
		return (score);
	}

    static void Main(string[] args)
    {
		var rnd = new Random();

		var pixels = new List<CIELab>();
		Bitmap palette = new Bitmap("palette.bmp");
		for (int y = 0; y < palette.Height; y++)
		{
			for (int x = 0; x < palette.Width; x++)
			{
				Color pixel = palette.GetPixel(x,y);
				CIELab lab = RGBtoLab(pixel.R, pixel.G, pixel.B);
				pixels.Add(lab);
			}
		}

		pixels.Sort(new Comparison<CIELab>((c1, c2) => rnd.Next(3) - 1));

		var loops = 0;
		var lowest_score = int.MaxValue;
		var imgs_saved = 0;
		var starting_time = DateTimeOffset.Now.ToUnixTimeSeconds();
		var score = get_total_score(pixels);

		while (true)
		{
			int index_1 = rnd.Next(WIDTH * HEIGHT);
			int index_2;
			do {
				index_2 = rnd.Next(WIDTH * HEIGHT);
			} while (index_1 == index_2);

			var old_score = score;

			var old_index_1_pixel = pixels[index_1];
			var old_index_2_pixel = pixels[index_2];

			score -= get_self_plus_neighbor_score(pixels, index_1);
			pixels[index_1] = old_index_2_pixel;
			score += get_self_plus_neighbor_score(pixels, index_1);

			score -= get_self_plus_neighbor_score(pixels, index_2);
			pixels[index_2] = old_index_1_pixel;
			score += get_self_plus_neighbor_score(pixels, index_2);

			if (score < lowest_score)
			{
				lowest_score = score;
				Console.WriteLine("Score {0}, Image {1}, Loop {2}, Seconds {3}", score, imgs_saved, loops, DateTimeOffset.Now.ToUnixTimeSeconds() - starting_time);

				var img = new Bitmap(WIDTH, HEIGHT, PixelFormat.Format24bppRgb);
				for (var y = 0; y < HEIGHT; y++)
				{
					for (var x = 0; x < WIDTH; x++)
					{
						CIELab lab = pixels[x + y * WIDTH];
						RGB rgb = LabtoRGB(lab.L, lab.A, lab.B);

						Color color = new Color(); // TODO: Necessary?
						color = Color.FromArgb(rgb.Red, rgb.Green, rgb.Blue);

						img.SetPixel(x, y, color);
					}
				}

				imgs_saved++;

				img.Save(String.Format("{0}/{1}.png", OUTPUT_DIRECTORY_NAME, imgs_saved));

				// img.Save(String.Format("{0}/1.png", OUTPUT_DIRECTORY_NAME, imgs_saved));
			}
			else
			{
				pixels[index_1] = old_index_1_pixel;
				pixels[index_2] = old_index_2_pixel;
				score = old_score;
			}

			loops++;
		}
    }
}
