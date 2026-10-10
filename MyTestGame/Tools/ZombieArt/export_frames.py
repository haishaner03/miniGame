"""Export image-generated sheets with a fixed per-view scale and ground/hip registration."""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps
from continuous_walk import front_walk, side_walk

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "output/imagegen/ZombieDirectional"
ART = ROOT / "Assets/GameMain/Res/Characters/Zombie/Directional"
VIEWS = ("Down", "Up", "Right", "Left")
FRAME_W, FRAME_H = 80, 64


def cutout(image):
    image = image.convert("RGBA")
    # The prompt forbids pink in the costume. Preserve dark outlines and tiny fingers.
    pixels = []
    for r, g, b, a in image.get_flattened_data():
        if r - g > 35 and b - g > 35:
            pixels.append((0, 0, 0, 0))
        else:
            pixels.append((r, g, b, 255 if a >= 128 else 0))
    image.putdata(pixels)
    return image


def sheet_cells(path):
    sheet = Image.open(path)
    if sheet.size != (1024, 1024):
        raise ValueError(f"Unexpected source size: {path.name}: {sheet.size}")
    return [cutout(sheet.crop((i % 4 * 256, i // 4 * 256, i % 4 * 256 + 256, i // 4 * 256 + 256)))
            for i in range(16)]


def hip_x(image, bbox):
    # Use trousers at the waist, rather than the moving hands or forward foot.
    y0 = round(bbox[1] + (bbox[3] - bbox[1]) * .55)
    y1 = round(bbox[1] + (bbox[3] - bbox[1]) * .67)
    xs = []
    for y in range(y0, y1):
        row = [x for x in range(bbox[0], bbox[2]) if image.getpixel((x, y))[3] > 0]
        if row:
            xs.append((min(row) + max(row)) / 2)
    return sorted(xs)[len(xs) // 2] if xs else (bbox[0] + bbox[2]) / 2


def normalize(cell, scale, anchor=None, floor=62):
    bbox = cell.getbbox()
    if bbox is None:
        raise ValueError("Empty sprite cell")
    anchor = anchor or (hip_x(cell, bbox), bbox[3])
    resampled = cell.resize((round(cell.width * scale), round(cell.height * scale)), Image.Resampling.NEAREST)
    frame = Image.new("RGBA", (FRAME_W, FRAME_H))
    frame.alpha_composite(resampled, (round(FRAME_W / 2 - anchor[0] * scale), round(floor - anchor[1] * scale)))
    bounds = frame.getbbox()
    if bounds is None or bounds[0] == 0 or bounds[2] == FRAME_W or bounds[1] == 0:
        raise ValueError(f"Clipped sprite, adjust export registration: {bounds}")
    return frame


def export():
    ART.mkdir(parents=True, exist_ok=True)
    frames = {}
    scales = {}
    for view in VIEWS[:3]:
        cells = sheet_cells(SOURCE / f"WalkAttack{view}_Source.png")
        height = cells[0].getbbox()[3] - cells[0].getbbox()[1]
        # One scale for all sixteen poses, never resize an individual attack to fit its bbox.
        scale = 52 / height
        scales[view] = scale
        for motion, section in (("Walk", cells[:8]), ("Attack", cells[8:])):
            frames[(motion, view)] = [normalize(cell, scale) for cell in section]
    for motion in ("Walk", "Attack"):
        frames[(motion, "Left")] = [ImageOps.mirror(f) for f in frames[(motion, "Right")]]

    supplementary = sheet_cells(SOURCE / "DeathIdle_Source.png")
    for row, view in enumerate(VIEWS[:3]):
        neutral = supplementary[12 + row]
        bbox = neutral.getbbox()
        scale = 52 / (bbox[3] - bbox[1])
        frames[("Idle", view)] = [normalize(neutral, scale)]
        # Falling changes silhouette height; all death frames use the idle scale.
        # Keep the sheet cell's specified ground anchor, rather than stretching each corpse.
        frames[("Death", view)] = [normalize(c, scale, (128, 226)) for c in supplementary[row * 4:row * 4 + 4]]
    for motion in ("Idle", "Death"):
        frames[(motion, "Left")] = [ImageOps.mirror(f) for f in frames[(motion, "Right")]]

    # Attacks start/end with the registered idle pose, eliminating a one-frame pop.
    for view in VIEWS:
        frames[("Attack", view)][0] = frames[("Idle", view)][0].copy()
        frames[("Attack", view)][7] = frames[("Idle", view)][0].copy()

    # Image generation established identity, materials, poses and attack art.
    # Retouch registered generated limb pixels so walking has exact alternating contacts.
    trajectories = {}
    for view in VIEWS[:3]:
        idle = frames[("Idle", view)][0]
        result, joints = side_walk(idle) if view == "Right" else front_walk(idle, view == "Up")
        frames[("Walk", view)] = result
        trajectories[view] = joints
    frames[("Walk", "Left")] = [ImageOps.mirror(f) for f in frames[("Walk", "Right")]]
    for view, joints in trajectories.items():
        for leg in (False, True):
            ankles = [entry[2] for entry in joints if entry[1] == leg]
            seam_steps = [((ankles[(i + 1) % 8][0] - point[0]) ** 2 +
                           (ankles[(i + 1) % 8][1] - point[1]) ** 2) ** .5
                          for i, point in enumerate(ankles)]
            if max(seam_steps) > 5.8:
                raise ValueError(f"Discontinuous foot path: {view}: {max(seam_steps)}")

    for (motion, view), images in frames.items():
        folder = ART / (motion + view)
        folder.mkdir(parents=True, exist_ok=True)
        for index, frame in enumerate(images):
            frame.save(folder / f"Zombie_{motion}{view}_{index:02d}.png")
        expected = 7 if motion == "Attack" else 8
        if motion in ("Walk", "Attack") and len({hashlib.sha256(f.tobytes()).hexdigest() for f in images}) != expected:
            raise ValueError(f"Duplicate poses in {motion}{view}")

    for motion in ("Walk", "Attack", "Death", "Idle"):
        count = len(frames[(motion, "Down")])
        sheet = Image.new("RGBA", (FRAME_W * count, FRAME_H * 4))
        for row, view in enumerate(VIEWS):
            for col, frame in enumerate(frames[(motion, view)]):
                sheet.alpha_composite(frame, (col * FRAME_W, row * FRAME_H))
        folder = ART / "Sheets"
        folder.mkdir(parents=True, exist_ok=True)
        sheet.save(folder / f"Zombie_{motion}_FourDirections.png")

    # This preview is a final art deliverable; no diagnostic report or editor screenshot.
    preview = []
    font_path = Path("C:/Windows/Fonts/consola.ttf")
    from PIL import ImageFont
    font = ImageFont.truetype(str(font_path), 17)
    for index in range(8):
        canvas = Image.new("RGB", (640, 340), "#242d2c")
        draw = ImageDraw.Draw(canvas)
        for col, view in enumerate(VIEWS):
            draw.text((col * 160 + 12, 6), view, font=font, fill="#bcc6b6")
            for row, motion in enumerate(("Walk", "Attack")):
                sprite = frames[(motion, view)][index].resize((160, 128), Image.Resampling.NEAREST)
                canvas.paste(sprite, (col * 160, row * 150 + 32), sprite)
                draw.text((col * 160 + 12, row * 150 + 156), motion, font=font, fill="#95a39d")
        preview.append(canvas)
    preview[0].save(SOURCE / "ZombieAnimationPreview.gif", save_all=True, append_images=preview[1:],
                    duration=83, loop=0, disposal=2)
    contact = Image.new("RGB", (1280, 512), "#242d2c")
    for row, view in enumerate(VIEWS):
        for col, frame in enumerate(frames[("Walk", view)]):
            frame = frame.resize((160, 128), Image.Resampling.NEAREST)
            contact.paste(frame, (160 * col, 128 * row), frame)
    contact.save(SOURCE / "ZombieWalkPreview.png")
    print(json.dumps({"frames": sum(len(f) for f in frames.values()), "canvas": "80x64",
                      "motionFrames": {"Walk": 8, "Attack": 8, "Death": 4, "Idle": 1},
                      "target": str(ART), "preview": str(SOURCE / "ZombieAnimationPreview.gif")}))


if __name__ == "__main__":
    export()
