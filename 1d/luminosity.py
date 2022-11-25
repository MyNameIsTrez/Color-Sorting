import math
from PIL import Image


# https://www.alanzucconi.com/2015/09/30/colour-sorting/
def main():
	output_file_path = "luminosity.png"

	with Image.open("palette.bmp") as im:
		pixels = list(im.convert("RGB").getdata())

		pixels.sort( key=lambda rgb: lum(*rgb) )

		im = Image.new("RGB", (im.width, im.height))
		im.putdata(pixels)
		im.save(output_file_path)


def lum(r,g,b):
    return math.sqrt(.241 * r + .691 * g + .068 * b)


# def lum(r,g,b):
#     return math.pow((0.299 * r + 0.587 * g + 0.114 * b), 1/2.2)


if __name__ == "__main__":
	main()
