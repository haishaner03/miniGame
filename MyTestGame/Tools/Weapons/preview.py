"""Preview the same pivot, scale, rotation and layering used by Unity."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageOps

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/imagegen/Weapons'
DATA = json.loads((OUT / 'PoseManifest.json').read_text(encoding='utf-8'))


def compose(pose, weapon='Machete'):
    body = Image.open(ROOT / pose['path']).convert('RGBA')
    item = Image.open(ROOT / f'Assets/GameMain/Res/Weapons/Melee/{weapon}.png').convert('RGBA')
    scale = .72 if weapon == 'Machete' else .78
    item = item.resize((round(16*scale), round(64*scale)), Image.Resampling.NEAREST)
    if pose['view'] == 'Left':
        item = ImageOps.mirror(item)
    rotated = Image.new('RGBA', (192, 192))
    rotated.alpha_composite(item, (round(96-8*scale), round(96-50*scale)))
    rotated = rotated.rotate(pose['weaponAngle'], resample=Image.Resampling.NEAREST)
    layer = Image.new('RGBA', (128, 128))
    hand = pose['gripPixel']
    layer.alpha_composite(rotated, (round(32+hand[0]-96), round(32+hand[1]-96)))
    result = Image.new('RGBA', (128, 128), '#353e37')
    if pose['view'] == 'Up': result.alpha_composite(layer)
    result.alpha_composite(body, (32, 32))
    if pose['view'] != 'Up': result.alpha_composite(layer)
    return result


def main():
    sheet = Image.new('RGB', (6*256, 4*256))
    for row, view in enumerate(['Down', 'Up', 'Right', 'Left']):
        poses = DATA[f'attack{view}']
        for i, pose in enumerate(poses):
            frame = compose(pose).resize((256,256), Image.Resampling.NEAREST)
            sheet.paste(frame, (i*256,row*256))
            ImageDraw.Draw(sheet).text((i*256+4,row*256+4), f'{view} {i}', fill='white')
        sequence = [0,1,2,3,4,5,0,0,0,0]
        frames = [compose(poses[i]).resize((512,512), Image.Resampling.NEAREST) for i in sequence]
        frames[0].save(OUT / f'Attack{view}_Preview.gif', save_all=True, append_images=frames[1:], duration=[70,40,32,40,55,55,200,70,70,70], loop=0, disposal=2)
    sheet.save(OUT / 'Weapons_AttackPreview.png')
    poses = [p for p in DATA['poses'] if '/Combat/' not in p['path']]
    sheet = Image.new('RGB', (10*160, ((len(poses)+9)//10)*160))
    for i, pose in enumerate(poses):
        sheet.paste(compose(pose).resize((160,160), Image.Resampling.NEAREST), ((i%10)*160,(i//10)*160))
        ImageDraw.Draw(sheet).text(((i%10)*160+2,(i//10)*160+2), f"{pose['view']} {i}", fill='white')
    sheet.save(OUT / 'Weapons_WalkPreview.png')

    # Show weapon silhouettes together and compare their actual base timings.
    frames = []
    for tick in range(42):
        canvas = Image.new('RGB', (768, 384), '#353e37')
        for column, (weapon, cooldown, duration, impact) in enumerate([
            ('Machete', .28, .2, .48), ('IronBar', .42, .32, .55)
        ]):
            elapsed = (tick / 50) % cooldown
            progress = min(1, elapsed / duration)
            adjusted = progress / impact * .48 if progress <= impact else .48 + (progress-impact)/(1-impact)*.52
            index = max(i for i, threshold in enumerate([0, .2, .36, .48, .68, .88]) if adjusted >= threshold)
            if elapsed >= duration:
                index = 0
            frame = compose(DATA['attackRight'][index], weapon).resize((384,384), Image.Resampling.NEAREST)
            canvas.paste(frame, (column*384, 0))
            ImageDraw.Draw(canvas).text((column*384+12,12), f'{weapon}  {duration:.2f}s / {cooldown:.2f}s', fill='white')
        frames.append(canvas)
    frames[0].save(OUT / 'Weapons_Comparison.gif', save_all=True, append_images=frames[1:], duration=20, loop=0)
    frames[12].save(OUT / 'Weapons_Comparison.png')


if __name__ == '__main__': main()
