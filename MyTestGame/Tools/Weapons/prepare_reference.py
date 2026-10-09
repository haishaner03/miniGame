from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
base = ROOT / 'Assets/GameMain/Res/Characters/Survivor/Generated'
paths = [
    base / 'Normalized/IdleDown/Survivor_IdleDown_00.png',
    base / 'Normalized/RunUp/Survivor_RunUp_00.png',
    base / 'Continuous/RunRight/Survivor_RunRight_Continuous_00.png',
    base / 'Continuous/RunLeft/Survivor_RunLeft_Continuous_00.png',
]
sheet = Image.new('RGBA', (1024, 320), '#353e37')
draw = ImageDraw.Draw(sheet)
for i, (path, label) in enumerate(zip(paths, ['FRONT / DOWN', 'BACK / UP', 'RIGHT PROFILE', 'LEFT PROFILE'])):
    im = Image.open(path).convert('RGBA')
    print(path.name, 'size', im.size, 'bbox', im.getbbox())
    sheet.alpha_composite(im.resize((256, 256), Image.Resampling.NEAREST), (i * 256, 32))
    draw.text((i * 256 + 30, 8), label, fill='white')
out = ROOT / 'output/imagegen/Weapons'
out.mkdir(parents=True, exist_ok=True)
sheet.save(out / 'SurvivorReference.png')
