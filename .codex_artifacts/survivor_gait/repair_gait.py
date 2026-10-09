from pathlib import Path
import hashlib
import json
import shutil
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path('D:/UnityProject/miniGame/MyTestGame/Assets/GameMain/Res/Characters/Survivor')
HERE = Path(__file__).parent
BACKUP = HERE / 'backup'
BACKUP.mkdir(parents=True, exist_ok=True)
OUT = HERE / 'candidate'
OUT.mkdir(exist_ok=True)
original = []
for i in range(8):
    path = ROOT / 'RunRight' / f'Survivor_RunRight_{i:02}.png'
    original.append(Image.open(path).convert('RGBA'))
for folder, pattern in [('RunRight', '*'), ('Ani', '*')]:
    destination = BACKUP / folder
    destination.mkdir(exist_ok=True)
    for path in (ROOT / folder).glob(pattern):
        if path.is_file() and not (destination / path.name).exists():
            shutil.copy2(path, destination / path.name)

base = original[0]
core = Image.new('RGBA', (64, 64))
# Retain the original head, backpack and jacket; exclude both old arm/leg silhouettes.
row_spans = {
    24: (0, 64), 25: (0, 64), 26: (0, 64), 27: (0, 64),
    28: (0, 64), 29: (0, 64), 30: (26, 35), 31: (27, 35),
    32: (28, 34), 33: (28, 34), 34: (28, 33), 35: (28, 33),
    36: (28, 33), 37: (28, 34), 38: (29, 34),
}
for y, (x0, x1) in row_spans.items():
    core.paste(base.crop((x0, y, x1, y + 1)), (x0, y))

# Eight phases: contact, compression, push-off, flight, then the opposite leg.
# The visible near leg changes from forward contact in 00 to rear swing in 04.
legs = [
    ((35, 42), (36, 46)),
    ((32, 43), (31, 46)),
    ((29, 42), (27, 45)),
    ((29, 41), (26, 42)),
    ((28, 42), (25, 44)),
    ((30, 43), (29, 46)),
    ((34, 40), (33, 43)),
    ((35, 40), (36, 43)),
]
arms = [
    ((26, 34), (25, 37)),
    ((27, 35), (28, 38)),
    ((32, 37), (34, 35)),
    ((35, 36), (38, 33)),
    ((36, 35), (39, 32)),
    ((34, 37), (36, 35)),
    ((29, 35), (29, 38)),
    ((26, 34), (25, 37)),
]
bobs = [0, 1, 0, -1, 0, 1, 0, -1]
outline = (21, 23, 21, 255)


def segment(draw, a, b, dark, light, width=3):
    draw.line([a, b], fill=outline, width=width + 1)
    draw.line([a, b], fill=dark, width=width)
    draw.line([(a[0] + 1, a[1]), (b[0] + 1, b[1])], fill=light, width=1)


def leg(im, pose, near, phase, bob):
    d = ImageDraw.Draw(im)
    knee, ankle = pose
    hip = (31 if near else 32, 38 + bob)
    # Ground contacts stay aligned, while the hips compress and lift.
    if phase in (3, 7):
        knee = (knee[0], knee[1] - 1)
        ankle = (ankle[0], ankle[1] - 1)
    dark = (43, 46, 43, 255) if near else (28, 32, 30, 255)
    light = (65, 68, 63, 255) if near else (40, 45, 41, 255)
    segment(d, hip, knee, dark, light, 3)
    segment(d, knee, ankle, dark, light, 2)
    d.point((knee[0], knee[1]), fill=(51, 54, 49, 255) if near else dark)
    x, y = ankle
    sole = (25, 21, 16, 255)
    leather = (79, 53, 34, 255) if near else (49, 35, 24, 255)
    shine = (111, 78, 51, 255) if near else (70, 49, 31, 255)
    # Both boots keep toes facing right, including the folded trailing foot.
    d.polygon([(x - 1, y - 2), (x + 1, y - 2), (x + 1, y - 1),
               (x + 2, y - 1), (x + 3, y), (x + 3, y + 1),
               (x - 1, y + 1)], fill=sole)
    d.rectangle((x - 1, y - 1, x + 1, y), fill=leather)
    d.line((x, y - 1, x + 1, y - 1), fill=shine)
    d.point((x + 2, y), fill=shine)
    d.point((x, y), fill=(67, 47, 31, 255) if near else leather)


def arm(im, pose, near, bob):
    d = ImageDraw.Draw(im)
    elbow, wrist = pose
    shoulder = (29 if near else 33, 31 + bob)
    elbow = (elbow[0], elbow[1] + bob)
    wrist = (wrist[0], wrist[1] + bob)
    dark = (73, 70, 48, 255) if near else (43, 45, 33, 255)
    light = (116, 111, 75, 255) if near else (76, 78, 53, 255)
    segment(d, shoulder, elbow, dark, light, 3)
    segment(d, elbow, wrist, dark, light, 2)
    x, y = wrist
    d.rectangle((x - 1, y - 1, x + 1, y + 1), fill=(55, 35, 25, 255))
    d.rectangle((x - 1, y - 1, x, y), fill=(196, 126, 88, 255) if near else (142, 86, 60, 255))
    d.point((x, y - 1), fill=(226, 157, 113, 255) if near else (173, 112, 78, 255))


frames = []
metadata = []
for i in range(8):
    im = Image.new('RGBA', (64, 64))
    bob = bobs[i]
    opposite = (i + 4) % 8
    leg(im, legs[opposite], False, opposite, bob)
    arm(im, arms[opposite], False, bob)
    leg(im, legs[i], True, i, bob)
    im.alpha_composite(core, (0, bob))
    arm(im, arms[i], True, bob)
    # The original shoulder and bag straps sit over the moving sleeve.
    shoulder = base.crop((28, 29, 32, 32))
    im.alpha_composite(shoulder, (28, 29 + bob))
    im.save(OUT / f'Survivor_RunRight_{i:02}.png')
    frames.append(im)
    metadata.append(dict(frame=i, near_leg=legs[i], far_leg=legs[opposite],
                         torso_bob=bob, bbox=im.getbbox(),
                         sha256=hashlib.sha256(im.tobytes()).hexdigest()))

sheet = Image.new('RGBA', (512, 64))
for i, im in enumerate(frames):
    sheet.alpha_composite(im, (i * 64, 0))
sheet.save(OUT / 'Survivor_RunRight_8x1.png')
font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 19)
contact = Image.new('RGB', (1120, 750), '#34383e')
d = ImageDraw.Draw(contact)
phases = ['near contact', 'near support', 'near push-off', 'flight',
          'far contact', 'far support', 'far push-off', 'flight']
for i, im in enumerate(frames):
    x, y = (i % 4) * 280, (i // 4) * 375
    zoom = im.crop((18, 18, 45, 51)).resize((270, 330), Image.Resampling.NEAREST)
    contact.paste(zoom, (x + 5, y + 35), zoom)
    d.text((x + 10, y + 8), f'{i:02} {phases[i]}', font=font, fill='white')
contact.save(HERE / 'After_ContactSheet.png')
animated = []
for i in range(8):
    canvas = Image.new('RGB', (960, 360), '#34383e')
    d = ImageDraw.Draw(canvas)
    for column, (label, im) in enumerate([('Before', original[i]), ('Right - fixed', frames[i]),
                                        ('Left - mirrored', ImageOps.mirror(frames[i]))]):
        tile = im.crop((16, 20, 48, 52)).resize((320, 320), Image.Resampling.NEAREST)
        canvas.paste(tile, (column * 320, 34), tile)
        d.text((column * 320 + 12, 8), label, font=font, fill='white')
    animated.append(canvas)
animated[0].save(HERE / 'Before_After_Run.gif', save_all=True, append_images=animated[1:],
                 duration=[80, 90, 80, 80, 90, 80, 90, 80], loop=0, optimize=False, disposal=2)
(HERE / 'Gait_Phases.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
if __name__ == '__main__':
    import sys
    if '--apply' in sys.argv:
        for path in OUT.glob('*.png'):
            shutil.copy2(path, ROOT / 'RunRight' / path.name)
        print('Applied 8 frames and updated the 8x1 sheet; existing Unity metadata retained.')
    else:
        print('Candidate prepared; original project textures are unchanged.')
