import colorsys
from PIL import Image
import numpy as np
from scipy.spatial import distance


# https://www.alanzucconi.com/2015/09/30/colour-sorting/
def main():
	output_file_path = "nearest-neighbor-hsv.png"

	with Image.open("palette.bmp") as im:
		pixels = list(im.convert("RGB").getdata())

		A = get_distance_matrix(pixels, im.width * im.height)
		path = NN(A, 0)

		# print(path)

		new_pixels = [ pixels[i] for i in path ]

		# print(new_pixels)

		im = Image.new("RGB", (im.width, im.height))
		im.putdata(new_pixels)
		im.save(output_file_path)


def get_distance_matrix(pixels, color_count):
	A = np.zeros([color_count,color_count])
	for x in range(color_count):
		for y in range(color_count):
			A[x,y] = distance.euclidean(colorsys.rgb_to_hsv(*pixels[x]),colorsys.rgb_to_hsv(*pixels[y]))
	return A


# https://stackoverflow.com/questions/17493494/nearest-neighbour-algorithm
def NN(A, start):
    """Nearest neighbor algorithm.
    A is an NxN array indicating distance between N locations
    start is the index of the starting location
    Returns the path of the found solution
    """
    path = [start]
    N = A.shape[0]
    mask = np.ones(N, dtype=bool)  # boolean values indicating which
                                   # locations have not been visited
    mask[start] = False

    for i in range(N-1):
        last = path[-1]
        next_ind = np.argmin(A[last][mask]) # find minimum of remaining locations
        next_loc = np.arange(N)[mask][next_ind] # convert to original location
        path.append(next_loc)
        mask[next_loc] = False

    return path


if __name__ == "__main__":
	main()
