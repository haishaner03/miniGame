"""Split generated art, keep feet/scale stable, export hand anchors and mirror left attacks."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageOps
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/imagegen/Weapons'
ART = ROOT / 'Assets/GameMain/Res/Characters/Survivor/Combat'
WEAPONS = ROOT / 'Assets/GameMain/Res/Weapons/Melee'


def cutout(im):
    rgba = np.array(im.convert('RGBA')).astype(np.int16)
    r, g, b = rgba[:, :, 0], rgba[:, :, 1], rgba[:, :, 2]
    pink = (r - g > 35) & (b - g > 35)
    rgba[pink] = 0
    rgba[rgba[:, :, 3] < 128] = 0
    rgba[rgba[:, :, 3] >= 128, 3] = 255
    return Image.fromarray(rgba.astype(np.uint8))


def skin_groups(im):
    a = np.array(im)
    r, g, b = [a[:, :, i].astype(int) for i in range(3)]
    mask = (a[:, :, 3] > 0) & (r > 100) & (r - g > 8) & (g - b > 5) & (r - b > 30)
    # Remove isolated warm stitching; the closed fist should form a connected patch.
    visited = set()
    groups = []
    for y, x in zip(*np.where(mask)):
        if (x, y) in visited:
            continue
        queue = [(int(x), int(y))]
        visited.add((x, y))
        group = []
        while queue:
            px, py = queue.pop()
            group.append((px, py))
            for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
                nx, ny = px + dx, py + dy
                if 0 <= nx < im.width and 0 <= ny < im.height and mask[ny, nx] and (nx, ny) not in visited:
                    visited.add((nx, ny))
                    queue.append((nx, ny))
        if len(group) >= 3:
            groups.append(group)
    return groups


def choose_hand(im, view, index, attack=False):
    candidates = []
    for group in skin_groups(im):
        x, y = np.mean(group, axis=0)
        # The face is central and above shoulders; exclude it.
        if 23 < x < 39 and y < 27:
            continue
        if view == 'Right' and x < 36:
            continue
        if view == 'Left' and x > 28:
            continue
        candidates.append((x, y, len(group)))
    if not candidates:
        return [43, 38] if view != 'Left' else [20, 38]
    if view == 'Down':
        if attack and index in (2, 3):
            chosen = max(candidates, key=lambda p: p[0] - p[1] * .25)
        elif attack and index == 4:
            chosen = min(candidates, key=lambda p: abs(p[0] - 29) + abs(p[1] - 37))
        else:
            chosen = min(candidates, key=lambda p: p[0])
    elif view == 'Up':
        chosen = min(candidates, key=lambda p: p[1]) if attack and index in (1, 2, 3) else max(candidates, key=lambda p: p[0])
    elif view == 'Right':
        chosen = max(candidates, key=lambda p: p[0])
    else:
        chosen = min(candidates, key=lambda p: p[0])
    return [round(float(chosen[0]) + .5, 2), round(float(chosen[1]) + .5, 2)]


def normalize_cells(sheet):
    cells = []
    for i in range(6):
        c, r = i % 3, i // 3
        cells.append(cutout(sheet.crop((round(c * sheet.width / 3), r * sheet.height // 2, round((c + 1) * sheet.width / 3), (r + 1) * sheet.height // 2))))
    neutral = cells[0].getbbox()
    scale = 55 / (neutral[3] - neutral[1])
    output = []
    for cell in cells:
        bbox = cell.getbbox()
        alpha = np.array(cell)[:, :, 3]
        ys, xs = np.where(alpha[bbox[3] - 12:bbox[3]] > 0)
        center_x = (xs.min() + xs.max()) / 2
        # Sample whole cell at a fixed scale; align boots, not a changing arm bounding box.
        resized = cell.resize((round(cell.width * scale), round(cell.height * scale)), Image.Resampling.NEAREST)
        x = round(32 - center_x * scale)
        y = round(62 - bbox[3] * scale)
        frame = Image.new('RGBA', (64, 64))
        frame.alpha_composite(resized, (x, y))
        output.append(frame)
    return output


def save_character():
    manifest = {'poses': [], 'attackDown': [], 'attackUp': [], 'attackRight': [], 'attackLeft': []}
    previews = []
    angles = {'Down': [165, 45, -65, 125, 170, 165], 'Up': [-20, -65, -20, 35, 70, -20], 'Right': [-25, 65, 20, -90, -130, -25]}
    frames_by_view = {}
    for view in ['Down', 'Up', 'Right']:
        corrected = OUT / f'Attack{view}_Corrected.png'
        source = corrected if corrected.exists() else OUT / f'Attack{view}_Source.png'
        frames = normalize_cells(Image.open(source))
        frames_by_view[view] = frames
    # Mirror the whole pose to correct the active shoulder without introducing seams.
    down = frames_by_view['Down']
    down[3] = ImageOps.mirror(down[3])
    for view in ['Down', 'Up', 'Right']:
        for index, frame in enumerate(frames_by_view[view]):
            folder = ART / f'Attack{view}'
            folder.mkdir(parents=True, exist_ok=True)
            path = folder / f'Survivor_Attack{view}_{index:02d}.png'
            frame.save(path)
            grip = choose_hand(frame, view, index, True)
            # Raised fists can overlap the face exclusion used for walk-cycle anchors.
            if view == 'Down' and index == 1:
                grip = [24.4, 27.0]
            if view == 'Right' and index == 1:
                grip = [37.5, 19.5]
            if view == 'Down' and index == 3:
                grip = [8.5, 27.8]
            if view == 'Up' and index == 3:
                grip = [38.7, 6.1]
            pose = {'path': path.relative_to(ROOT).as_posix(), 'view': view, 'gripPixel': grip, 'weaponAngle': angles[view][index]}
            manifest['poses'].append(pose)
            manifest[f'attack{view}'].append(pose)
            previews.append((view, index, frame, grip))
    for index, frame in enumerate(frames_by_view['Right']):
        left = ImageOps.mirror(frame)
        path = ART / 'AttackLeft' / f'Survivor_AttackLeft_{index:02d}.png'
        path.parent.mkdir(parents=True, exist_ok=True)
        left.save(path)
        source_pose = manifest['attackRight'][index]
        pose = {'path': path.relative_to(ROOT).as_posix(), 'view': 'Left', 'gripPixel': [64 - source_pose['gripPixel'][0], source_pose['gripPixel'][1]], 'weaponAngle': -source_pose['weaponAngle']}
        manifest['poses'].append(pose)
        manifest['attackLeft'].append(pose)
        previews.append(('Left', index, left, pose['gripPixel']))
    # Existing approved walk cycles remain intact. Attach to their actual hand pixels.
    generated = ROOT / 'Assets/GameMain/Res/Characters/Survivor/Generated'
    for folder, view in [('Normalized/IdleDown', 'Down'), ('Normalized/RunDown', 'Down'), ('Normalized/RunUp', 'Up'), ('Continuous/RunRight', 'Right'), ('Continuous/RunLeft', 'Left')]:
        for path in sorted((generated / folder).glob('*.png')):
            if Image.open(path).size != (64, 64):
                continue
            grip = choose_hand(Image.open(path).convert('RGBA'), view, 0)
            manifest['poses'].append({'path': path.relative_to(ROOT).as_posix(), 'view': view, 'gripPixel': grip, 'weaponAngle': 20 if view == 'Left' else -20 if view in ['Right', 'Up'] else 165})
    (OUT / 'PoseManifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    sheet = Image.new('RGB', (6 * 256, 4 * 272), '#353e37')
    draw = ImageDraw.Draw(sheet)
    for row, view in enumerate(['Down', 'Up', 'Right', 'Left']):
        for v, index, frame, grip in previews:
            if v != view:
                continue
            thumb = frame.resize((256, 256), Image.Resampling.NEAREST)
            sheet.paste(thumb, (index * 256, row * 272 + 16), thumb)
            draw.text((index * 256 + 4, row * 272 + 2), f'{view} {index}', fill='white')
            px, py = index * 256 + grip[0] * 4, row * 272 + 16 + grip[1] * 4
            draw.ellipse((px - 3, py - 3, px + 3, py + 3), outline='#00ffff', width=1)
    sheet.save(OUT / 'AttackFrames_Preview.png')
    print('24 transparent attack frames and pose manifest exported')


def save_weapons():
    im = Image.open(OUT / 'Weapons_Source.png')
    WEAPONS.mkdir(parents=True, exist_ok=True)
    for index, name in enumerate(['Machete', 'IronBar']):
        cell = cutout(im.crop((index * im.width // 2, 0, (index + 1) * im.width // 2, im.height)))
        crop = cell.crop(cell.getbbox())
        # Preserve a broader blade silhouette so it reads differently from the rod
        # at gameplay resolution. Keep the same canvas, grip pivot and length.
        width = max(3, round(crop.width * 48 / crop.height * (1.5 if name == 'Machete' else 1)))
        crop = crop.resize((width, 48), Image.Resampling.NEAREST)
        sprite = Image.new('RGBA', (16, 64))
        sprite.alpha_composite(crop, ((16 - crop.width) // 2, 8))
        sprite.save(WEAPONS / f'{name}.png')
    print('2 transparent 16x64 weapon sprites exported')


if __name__ == '__main__':
    save_weapons()
    save_character()
