from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path('D:/UnityProject/miniGame/MyTestGame/Assets/GameMain/Res/Characters/Survivor')
HERE = Path(__file__).parent
HERE.mkdir(parents=True, exist_ok=True)
frames = [Image.open(ROOT / 'RunRight' / f'Survivor_RunRight_{i:02}.png').convert('RGBA') for i in range(8)]
font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 20)
out = Image.new('RGB', (1120, 750), '#34383e')
draw = ImageDraw.Draw(out)
for i, im in enumerate(frames):
    x, y = (i % 4) * 280, (i // 4) * 375
    zoom = im.crop((18, 18, 45, 51)).resize((270, 330), Image.Resampling.NEAREST)
    out.paste(zoom, (x + 5, y + 35), zoom)
    draw.text((x + 10, y + 8), f'Frame {i:02}', font=font, fill='white')
out.save(HERE / 'Before_ContactSheet.png')
info = []
for i, im in enumerate(frames):
    info.append(dict(frame=i, bbox=im.getbbox(), sha256=hashlib.sha256(im.tobytes()).hexdigest()))
(HERE / 'Before_FrameInfo.json').write_text(json.dumps(info, indent=2), encoding='utf-8')
print(json.dumps(info, indent=2))
