from pathlib import Path
from PIL import Image

src = Path(r'D:\UnityProject\miniGame\MyTestGame\Assets\GameMain\Res\Map\StreetBlock01\Polished\GptImage2\Curb_StreetEdge_4x4_GptImage2_Raw.png')
dst = src.with_name('Curb_StreetEdge_4x4_GptImage2.png')
im = Image.open(src).convert('RGBA')
alpha = im.getchannel('A')
pixels = alpha.load()
cols = []
for x in range(im.width):
    if any(pixels[x, y] > 8 for y in range(im.height)):
        if not cols or x > cols[-1][1] + 1:
            cols.append([x, x])
        else:
            cols[-1][1] = x
rows = []
for y in range(im.height):
    if any(pixels[x, y] > 8 for x in range(im.width)):
        if not rows or y > rows[-1][1] + 1:
            rows.append([y, y])
        else:
            rows[-1][1] = y
if len(cols) != 4 or len(rows) != 4:
    raise RuntimeError(f'Expected 4x4 occupied runs, got {cols} / {rows}')

atlas = Image.new('RGBA', (256, 256), (0, 0, 0, 0))
for row, (y0, y1) in enumerate(rows):
    for col, (x0, x1) in enumerate(cols):
        crop = im.crop((x0, y0, x1 + 1, y1 + 1))
        box = crop.getchannel('A').getbbox()
        if box:
            crop = crop.crop(box)
        scale = min(60 / crop.width, 60 / crop.height)
        size = (max(1, round(crop.width * scale)), max(1, round(crop.height * scale)))
        crop = crop.resize(size, Image.Resampling.LANCZOS)
        x = col * 64 + (64 - crop.width) // 2
        y = (3 - row) * 64 + (64 - crop.height) // 2
        atlas.alpha_composite(crop, (x, y))
atlas.save(dst)
print(dst, atlas.size, 'columns=', cols, 'rows=', rows)
