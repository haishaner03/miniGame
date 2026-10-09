"""Export the generated 4x4 icon sheet as transparent 64px Unity sprites."""
import json
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[3]
spec_path = Path(__file__).with_name('asset_spec.json')
spec = json.loads(spec_path.read_text(encoding='utf-8-sig'))
source = Image.open(root / 'output/imagegen/ZombieHUD_Icons.png').convert('RGBA')
destination = root / spec['targetFolder']
destination.mkdir(parents=True, exist_ok=True)
preview = Image.new('RGB', (512, 512), '#202725')
for index, asset in enumerate(spec['atlases'][0]['assets']):
    x, y = index % 4 * 256, index // 4 * 256
    icon = source.crop((x, y, x + 256, y + 256))
    pixels = list(icon.getdata())
    icon.putdata([(r, g, b, 0 if r - g > 70 and b - g > 70 else a)
                  for r, g, b, a in pixels])
    bounds = icon.getbbox()
    if bounds is None:
        raise ValueError(f'Empty icon: {asset["name"]}')
    icon = icon.crop(bounds)
    icon.thumbnail((56, 56), Image.Resampling.NEAREST)
    output = Image.new('RGBA', (64, 64))
    output.alpha_composite(icon, ((64 - icon.width) // 2, (64 - icon.height) // 2))
    output.save(destination / (asset['name'] + '.png'))
    preview.paste(output.resize((128, 128), Image.Resampling.NEAREST),
                  (index % 4 * 128, index // 4 * 128), output.resize((128, 128), Image.Resampling.NEAREST))
preview.save(root / 'output/imagegen/ZombieHUD_Icons_TransparentPreview.png')
spec['status'] = 'icons_exported'
spec['notes'] = [note for note in spec['notes'] if not note.startswith('Do not modify')]
spec_path.write_text(json.dumps(spec, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Exported 16 transparent sprites to {destination}')
