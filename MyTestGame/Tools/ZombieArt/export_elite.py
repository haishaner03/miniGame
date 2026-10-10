"""Register image-generated elite poses and export Unity frames plus an animation preview."""
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps
from export_frames import cutout, hip_x, normalize
from elite_walk import front_walk, side_walk

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "output/imagegen/EliteHunter"
ART = ROOT / "Assets/GameMain/Res/Characters/Zombie/EliteHunter"
VIEWS = ("Down", "Up", "Right", "Left")


def sheet_cells(path):
    # Generated drawings can straddle an invisible cell boundary. Extract connected
    # figures before registration, so a neighbouring head/hand never leaks into a frame.
    sheet = cutout(Image.open(path))
    if sheet.size != (1024, 1024):
        raise ValueError(f"Unexpected source size: {sheet.size}")
    width, height = sheet.size
    alpha = bytearray(sheet.getchannel("A").tobytes())
    groups = []
    for start in range(len(alpha)):
        if not alpha[start]:
            continue
        alpha[start] = 0
        pending, points = [start], []
        while pending:
            index = pending.pop()
            points.append(index)
            x, y = index % width, index // width
            for nx, ny in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
                if 0 <= nx < width and 0 <= ny < height:
                    neighbor = ny * width + nx
                    if alpha[neighbor]:
                        alpha[neighbor] = 0
                        pending.append(neighbor)
        xs, ys = [p % width for p in points], [p // width for p in points]
        groups.append((points, (min(xs), min(ys), max(xs)+1, max(ys)+1)))
    groups.sort(key=lambda entry: len(entry[0]), reverse=True)
    primary = groups[:16]
    if len(primary) != 16 or min(len(points) for points, _ in primary) < 500:
        raise ValueError("Source does not contain sixteen complete figures")
    assignment = {}
    for points, bounds in primary:
        x, y = (bounds[0]+bounds[2])/2, (bounds[1]+bounds[3])/2
        slot = min(3, int(y // 256))*4 + min(3, int(x // 256))
        if slot in assignment:
            raise ValueError(f"Overlapping generated figures in slot {slot}")
        assignment[slot] = [points[:], bounds]
    for points, bounds in groups[16:]:
        x, y = (bounds[0]+bounds[2])/2, (bounds[1]+bounds[3])/2
        slot = min(assignment, key=lambda key: ((assignment[key][1][0]+assignment[key][1][2])/2-x)**2
                   + ((assignment[key][1][1]+assignment[key][1][3])/2-y)**2)
        assignment[slot][0].extend(points)
    cells = []
    for slot in range(16):
        isolated = Image.new("RGBA", sheet.size)
        for index in assignment[slot][0]:
            x, y = index % width, index // width
            isolated.putpixel((x, y), sheet.getpixel((x, y)))
        figure = isolated.crop(isolated.getbbox())
        if figure.width > 250 or figure.height > 230:
            raise ValueError(f"Figure too large for cell: {slot}: {figure.size}")
        cell = Image.new("RGBA", (256, 256))
        cell.alpha_composite(figure, (128-figure.width//2, 226-figure.height))
        cells.append(cell)
    return cells


def main():
    supplemental = sheet_cells(SOURCE / "DeathIdle_Source.png")
    frames = {}
    for row, view in enumerate(VIEWS[:3]):
        cells = sheet_cells(SOURCE / f"WalkAttack{view}_Source.png")
        bounds = cells[0].getbbox()
        scale = 54 / (bounds[3] - bounds[1])
        for cell in cells:
            box = cell.getbbox()
            anchor = hip_x(cell, box)
            scale = min(scale, 38 / max(1, anchor-box[0]),
                        38 / max(1, box[2]-anchor), 59 / (box[3]-box[1]))
        # Use the neutral pose from this direction's attack sheet for exact identity.
        frames["Idle", view] = [normalize(cells[8], scale)]
        neutral = supplemental[12 + row]
        bounds = neutral.getbbox()
        idle_scale = 54 / (bounds[3] - bounds[1])
        for motion, section in (("Walk", cells[:8]), ("Attack", cells[8:])):
            frames[motion, view] = [normalize(cell, scale) for cell in section]
        frames["Death", view] = [normalize(cell, idle_scale, (128, 226))
                                for cell in supplemental[row * 4:row * 4 + 4]]
        # Close the wind-up/recovery on the same registered idle so state changes don't pop.
        frames["Attack", view][0] = frames["Idle", view][0].copy()
        frames["Attack", view][7] = frames["Idle", view][0].copy()
        walk, joints = side_walk(frames["Idle", view][0]) if view == "Right" else front_walk(frames["Idle", view][0], view == "Up")
        frames["Walk", view] = walk
        for leg in (False, True):
            ankles = [entry[2] for entry in joints if entry[1] == leg]
            max_step = max(((ankles[(i+1)%8][0]-a[0])**2 + (ankles[(i+1)%8][1]-a[1])**2)**.5
                           for i, a in enumerate(ankles))
            if max_step > 5.8:
                raise ValueError(f"Discontinuous foot trajectory: {view}: {max_step}")
    for motion in ("Walk", "Attack", "Death", "Idle"):
        frames[motion, "Left"] = [ImageOps.mirror(frame) for frame in frames[motion, "Right"]]

    for (motion, view), images in frames.items():
        folder = ART / (motion + view)
        folder.mkdir(parents=True, exist_ok=True)
        for index, frame in enumerate(images):
            if not frame.getbbox() or frame.getextrema()[3] != (0, 255):
                raise ValueError(f"Missing transparent background in {motion}{view}{index}")
            frame.save(folder / f"EliteHunter_{motion}{view}_{index:02d}.png")
        unique = len({hashlib.sha256(image.tobytes()).hexdigest() for image in images})
        if motion == "Walk" and unique != 8:
            raise ValueError(f"Repeated walk poses: {view}: {unique}")
        if motion == "Attack" and unique < 7:
            raise ValueError(f"Repeated attack poses: {view}: {unique}")

    sheets = ART / "Sheets"
    sheets.mkdir(exist_ok=True)
    for motion in ("Walk", "Attack", "Death", "Idle"):
        count = len(frames[motion, "Down"])
        sheet = Image.new("RGBA", (80 * count, 64 * 4))
        for row, view in enumerate(VIEWS):
            for col, frame in enumerate(frames[motion, view]):
                sheet.alpha_composite(frame, (col * 80, row * 64))
        sheet.save(sheets / f"EliteHunter_{motion}_FourDirections.png")

    font = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 17)
    preview = []
    for index in range(8):
        canvas = Image.new("RGB", (800, 430), "#242d2c")
        draw = ImageDraw.Draw(canvas)
        for col, view in enumerate(VIEWS):
            draw.text((col * 200 + 16, 8), view, font=font, fill="#e8b57a")
            for row, motion in enumerate(("Walk", "Attack")):
                sprite = frames[motion, view][index].resize((200, 160), Image.Resampling.NEAREST)
                canvas.paste(sprite, (col * 200, row * 190 + 36), sprite)
                draw.text((col * 200 + 16, row * 190 + 186), motion, font=font, fill="#bec6c0")
        preview.append(canvas)
    preview[0].save(SOURCE / "EliteHunterAnimationPreview.gif", save_all=True,
                    append_images=preview[1:], duration=83, loop=0, disposal=2)
    contact = Image.new("RGB", (640, 512), "#242d2c")
    for row, view in enumerate(VIEWS):
        for col, motion in enumerate(("Idle", "Walk", "Attack", "Death")):
            index = 0 if motion == "Idle" else 3
            sprite = frames[motion, view][index].resize((160, 128), Image.Resampling.NEAREST)
            contact.paste(sprite, (col * 160, row * 128), sprite)
    contact.save(SOURCE / "EliteHunterPosePreview.png")
    comparison = Image.new("RGB", (480, 220), "#242d2c")
    draw = ImageDraw.Draw(comparison)
    for index, (name, sprite) in enumerate((
        ("Ordinary", Image.open(ROOT / "Assets/GameMain/Res/Characters/Zombie/Directional/IdleDown/Zombie_IdleDown_00.png")),
        ("Elite Hunter", frames["Idle", "Down"][0]),
    )):
        sprite = sprite.resize((240, 192), Image.Resampling.NEAREST)
        comparison.paste(sprite, (index * 240, 25), sprite)
        draw.text((index * 240 + 16, 7), name, font=font, fill="#e8b57a")
    comparison.save(SOURCE / "EliteHunterComparison.png")
    print(json.dumps({"frames": sum(map(len, frames.values())), "canvas": "80x64",
                      "art": str(ART), "preview": str(SOURCE / "EliteHunterAnimationPreview.gif")}))


if __name__ == "__main__":
    main()
