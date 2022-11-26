
from PIL import Image, ImageDraw
from cmath import phase
from sys import maxsize
from math import log10


# TODO: Actually let this use the input palette


# https://codegolf.stackexchange.com/a/22511
def main():
	output_file_path = "fractal.png"
	dim  = (1024, 1024)
	bits = 8

	with Image.open("palette.bmp") as im:
		pixels = list(im.convert("RGB").getdata())

		cmax = (1<<bits)-1
		cfac = 255/cmax

		img  = Image.new('RGB', dim)
		draw = ImageDraw.Draw(img)

		xstart = -2
		ystart = -2

		xd = 4 / dim[0]
		yd = 4 / dim[1]

		tol = 1e-6

		a = [[], [], [], [], []]

		for x in range(dim[0]):
			print(x, "\r", end=' ')
			for y in range(dim[1]):
				z = d = complex(xstart + x*xd, ystart + y*yd)
				c = 0.0
				l = 1

				while abs(l-z) > tol and abs(z) > tol:
					l = z
					z -= (z**5-1)/(5*z**4)
					c += 1.0

				if z == 0: c = maxsize
				p = int(phase(z))

				if abs(l-z) > 0.0:
					c += log10(abs(l-z)) / 6

				a[p] += (c, x, y),

		for i in range(5):
			a[i].sort(reverse = False)

		pnum = [len(a[i]) for i in range(5)]
		ptot = dim[0]*dim[1]

		bounds = []
		lbound = 0
		for i in range(4):
			nbound = lbound + pnum[i]/ptot
			bounds += nbound,
			lbound = nbound

		t = [[], [], [], [], []]
		for i in range(ptot-1, -1, -1):
			r = (i>>bits*2)*cfac
			g = (cmax&i>>bits)*cfac
			b = (cmax&i)*cfac
			(h, s, v) = RGBtoHSV(r, g, b)
			h = (h+0.1)%1
			if   h < bounds[0] and len(t[0]) < pnum[0]: p=0
			elif h < bounds[1] and len(t[1]) < pnum[1]: p=1
			elif h < bounds[2] and len(t[2]) < pnum[2]: p=2
			elif h < bounds[3] and len(t[3]) < pnum[3]: p=3
			else: p=4
			t[p] += (int(r), int(g), int(b)),

		for i in range(5):
			t[i].sort(key = lambda c: c[0]*2126 + c[1]*7152 + c[2]*722, reverse = True)

		r = [0, 0, 0, 0, 0]
		for p in range(5):
			for c,x,y in a[p]:
				draw.point((x,y), t[p][r[p]])
				r[p] += 1

		img.save("fractal.png")

		# pixels.sort( key=lambda rgb: colorsys.rgb_to_hsv(*rgb) )

		# im = Image.new("RGB", (im.width, im.height))
		# im.putdata(pixels)
		# im.save(output_file_path)


def RGBtoHSV(R, G, B):
	R /= 255
	G /= 255
	B /= 255

	cmin = min(R, G, B)
	cmax = max(R, G, B)
	dmax = cmax - cmin

	V = cmax

	if dmax == 0:
		H = 0
		S = 0

	else:
		S = dmax/cmax

		dR = ((cmax - R)/6 + dmax/2)/dmax
		dG = ((cmax - G)/6 + dmax/2)/dmax
		dB = ((cmax - B)/6 + dmax/2)/dmax

		if   R == cmax: H = (dB - dG)%1
		elif G == cmax: H = (1/3 + dR - dB)%1
		elif B == cmax: H = (2/3 + dG - dR)%1

	return (H, S, V)


if __name__ == "__main__":
	main()
