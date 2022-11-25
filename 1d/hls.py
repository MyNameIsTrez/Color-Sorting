import colorsys
from PIL import Image


# https://www.alanzucconi.com/2015/09/30/colour-sorting/
def main():
	output_file_path = "hls.png"

	with Image.open("palette.bmp") as im:
		pixels = list(im.convert("RGB").getdata())

		pixels.sort( key=lambda rgb: colorsys.rgb_to_hls(*rgb) )

		im = Image.new("RGB", (im.width, im.height))
		im.putdata(pixels)
		im.save(output_file_path)


if __name__ == "__main__":
	main()
