import math, colorsys
from PIL import Image


# https://www.alanzucconi.com/2015/09/30/colour-sorting/
def main():
	output_file_path = "step.png"
	repetitions = 20

	with Image.open("palette.bmp") as im:
		pixels = list(im.convert("RGB").getdata())

		pixels.sort( key=lambda rgb: step(rgb[0], rgb[1],rgb[2],repetitions) )

		im = Image.new("RGB", (im.width, im.height))
		im.putdata(pixels)
		im.save(output_file_path)


def step(r,g,b, repetitions):
    lum = math.sqrt( .241 * r + .691 * g + .068 * b )
    h, s, v = colorsys.rgb_to_hsv(r,g,b)
    h2 = int(h * repetitions)
    lum2 = int(lum * repetitions) # lum2 was not used in original
    v2 = int(v * repetitions)
    return (h2, lum, v2)


if __name__ == "__main__":
	main()
