import random
from PIL import Image


def main():
	color_count = 64
	im_height = 50
	output_file_path = "data-new.png"

	pixels = [ tuple(random.choices(range(256), k=3)) for _ in range(color_count) ] * im_height

	im = Image.new("RGB", (color_count, im_height))
	im.putdata(pixels)
	im.save(output_file_path)


if __name__ == "__main__":
	main()
