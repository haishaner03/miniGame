from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
source = Image.open(root / 'output/imagegen/ExperienceShard_Source.png').convert('RGBA')
pixels = source.load()
for y in range(source.height):
    for x in range(source.width):
        r, g, b, a = pixels[x, y]
        if r - g > 12 and b - g > 12:
            pixels[x, y] = (0, 0, 0, 0)
crop = source.crop(source.getbbox())
crop.thumbnail((24, 28), Image.Resampling.NEAREST)
sprite = Image.new('RGBA', (32, 32))
sprite.alpha_composite(crop, ((32 - crop.width) // 2, (32 - crop.height) // 2))
target = root / 'Assets/GameMain/Res/Drops/ExperienceShard.png'
target.parent.mkdir(parents=True, exist_ok=True)
sprite.save(target)
preview = sprite.resize((256, 256), Image.Resampling.NEAREST)
preview.save(root / 'output/imagegen/ExperienceShard_Preview.png')
print(f'Exported {target}: 32x32 RGBA')
