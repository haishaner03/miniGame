"""Export generated isolated weapons and prepare a body identity reference."""
from pathlib import Path
from PIL import Image
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/imagegen/BowAndMeta'
ART = ROOT / 'Assets/GameMain/Res/Weapons/SurvivorUnlocks'
ART.mkdir(parents=True, exist_ok=True)

sheet = Image.open(OUT / 'WeaponSheet.png').convert('RGBA')
data = np.array(sheet)
r, g, b = [data[:, :, i].astype(int) for i in range(3)]
data[(r-g > 25) & (b-g > 25)] = 0
clean = Image.fromarray(data)
for name, bounds, fit in [
    ('HuntingBow', (0, 0, 341, 1024), (24, 58)),
    ('HeavyHammer', (341, 0, 650, 1024), (27, 57)),
    ('Arrow', (650, 0, 1024, 1024), (52, 14)),
]:
    region = clean.crop(bounds)
    region = region.crop(region.getbbox())
    scale = min(fit[0] / region.width, fit[1] / region.height)
    region = region.resize((round(region.width*scale), round(region.height*scale)), Image.Resampling.NEAREST)
    frame = Image.new('RGBA', (64, 64))
    frame.alpha_composite(region, ((64-region.width)//2, (64-region.height)//2))
    frame.save(ART / f'{name}.png')

refs = Image.new('RGB', (768, 384), (255, 0, 255))
for i, direction in enumerate(['Down', 'Up', 'Right']):
    sprite = Image.open(ROOT / f'Assets/GameMain/Res/Characters/Survivor/Combat/Attack{direction}/Survivor_Attack{direction}_00.png').convert('RGBA')
    sprite = sprite.resize((256, 256), Image.Resampling.NEAREST)
    refs.paste(sprite, (i*256, 64), sprite)
refs.save(OUT / 'BodyIdentityReference.png')
print('Exported 3 transparent 64x64 weapon sprites and body reference.')
