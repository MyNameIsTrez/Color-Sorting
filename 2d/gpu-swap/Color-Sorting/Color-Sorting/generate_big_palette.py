from PIL import Image


def main(duplicates_width, duplicates_height):
	img = Image.open('palette.bmp')
	img_w, img_h = img.size
	background = Image.new('RGBA', (img_w * duplicates_width, img_h * duplicates_height))
	for duplicate_height in range(duplicates_height):
		for duplicate_width in range(duplicates_width):
			offset = (16 * duplicate_width, 16 * duplicate_height)
			background.paste(img, offset)
	background.save('big-palette.png')


if __name__ == "__main__":
	main(160, 90)
