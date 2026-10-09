from pathlib import Path
import csv
from PIL import Image, ImageDraw, ImageFont

here = Path(__file__).parent
im = Image.open(here / 'Street_Overview.png').convert('RGB')
draw = ImageDraw.Draw(im)
font = ImageFont.truetype('C:/Windows/Fonts/consolab.ttf', 22)


def pixel(x, y):
    return round(525 + (x - 20) * 25), round(625 - (y - 24) * 25)


with (here / 'ValidatedRoute.csv').open() as handle:
    path = [pixel(float(row['x']), float(row['y'])) for row in csv.DictReader(handle)]
draw.line(path, fill='#21342c', width=8)
draw.line(path, fill='#8bd79b', width=3)
for i in [0, -1]:
    x, y = path[i]
    draw.ellipse((x - 8, y - 8, x + 8, y + 8), fill='#eff8df', outline='#203826', width=2)
for text, x, y in [('START', 7.5, 5), ('EXIT', 30.4, 43.4)]:
    px, py = pixel(x, y)
    draw.text((px + 14, py - 12), text, font=font, fill='white', stroke_width=2, stroke_fill='#182016')
im.save(here / 'Street_Overview_Route.png')
print(here / 'Street_Overview_Route.png')
