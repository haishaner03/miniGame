"""Build a continuous left run from the existing survivor's pixel textures."""

import json
import math
from pathlib import Path

from PIL import Image, ImageDraw


PROJECT = Path(__file__).resolve().parents[2]
SOURCE = PROJECT / "Assets/GameMain/Res/Characters/Survivor/Generated/Normalized/RunLeft"
OUTPUT = PROJECT / "Assets/GameMain/Res/Characters/Survivor/Generated/Continuous/RunLeft"
PREVIEW = PROJECT / "Assets/Screenshots/RunLeftContinuity"
FRAME_COUNT = 16
SIZE = 64
STANCE_FRACTION = 0.375


def polygon_mask(points):
    mask = Image.new("L", (SIZE, SIZE))
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask


def inverse_limb(source, mask, src_a, src_b, dst_a, dst_b):
    layer = Image.new("RGBA", source.size)
    sx, sy = src_b[0] - src_a[0], src_b[1] - src_a[1]
    dx, dy = dst_b[0] - dst_a[0], dst_b[1] - dst_a[1]
    src_length, dst_length = math.hypot(sx, sy), math.hypot(dx, dy)
    for y in range(SIZE):
        for x in range(SIZE):
            rx, ry = x - dst_a[0], y - dst_a[1]
            along = (rx * dx + ry * dy) / (dst_length * dst_length)
            if not -0.15 <= along <= 1.15:
                continue
            across = (rx * -dy + ry * dx) / dst_length
            px = round(src_a[0] + along * sx - across * sy / src_length)
            py = round(src_a[1] + along * sy + across * sx / src_length)
            if 0 <= px < SIZE and 0 <= py < SIZE and mask.getpixel((px, py)):
                layer.putpixel((x, y), source.getpixel((px, py)))
    return layer


def knee_position(hip, ankle, thigh=9.5, shin=10.0):
    dx, dy = ankle[0] - hip[0], ankle[1] - hip[1]
    distance = math.hypot(dx, dy)
    along = (thigh * thigh - shin * shin + distance * distance) / (2 * distance)
    offset = math.sqrt(max(0, thigh * thigh - along * along))
    return (hip[0] + dx * along / distance - dy * offset / distance,
            hip[1] + dy * along / distance + dx * offset / distance)


def foot_pose(phase, center):
    phase %= 1.0
    if phase < STANCE_FRACTION:
        return (center - 8 + 16 * phase / STANCE_FRACTION, 56.0), 0.0
    recovery = (phase - STANCE_FRACTION) / (1 - STANCE_FRACTION)
    lift = math.sin(math.pi * recovery)
    return (center + 8 * math.cos(math.pi * recovery), 56 - 5 * lift), -55 * lift * lift


def boot_layer(source, mask, ankle, angle):
    layer = Image.new("RGBA", source.size)
    cosine, sine = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = x - ankle[0], y - ankle[1]
            px = round(24 + (cosine * dx + sine * dy) / 0.85)
            py = round(56 + (-sine * dx + cosine * dy))
            if 0 <= px < SIZE and 0 <= py < SIZE and mask.getpixel((px, py)):
                layer.putpixel((x, y), source.getpixel((px, py)))
    return layer


def dim(layer, factor):
    result = Image.new("RGBA", layer.size)
    result.putdata([(round(r * factor), round(g * factor), round(b * factor), a)
                    for r, g, b, a in layer.get_flattened_data()])
    return result


def build_frames():
    template = Image.open(SOURCE / "Survivor_RunLeft_02.png").convert("RGBA")
    upper = Image.new("RGBA", template.size)
    upper.paste(template.crop((0, 0, SIZE, 41)), (0, 0))
    near_mask = polygon_mask([(26, 40), (32, 41), (31, 44), (29, 48), (28, 55),
                              (24, 57), (21, 55), (21, 48), (23, 43)])
    far_mask = polygon_mask([(32, 40), (36, 40), (36, 46), (39, 49), (44, 50),
                             (45, 54), (42, 56), (33, 54), (29, 49), (31, 45)])
    shoe_mask = polygon_mask([(19, 55), (23, 55), (23, 57), (26, 57), (27, 61),
                              (22, 62), (17, 59), (16, 57)])
    frames, poses = [], []
    for index in range(FRAME_COUNT):
        phase = index / FRAME_COUNT
        bob = round(0.7 * math.cos(4 * math.pi * phase))
        result = Image.new("RGBA", template.size)
        joints = []
        for far, shift, center in [(True, 0.5, 33), (False, 0.0, 31)]:
            ankle, angle = foot_pose(phase + shift, center)
            hip = (center, 40 + bob)
            knee = knee_position(hip, ankle)
            src_hip, src_knee, src_ankle = ((34, 41), (35, 49), (43, 53)) if far else ((29, 41), (25, 48), (24, 56))
            mask = far_mask if far else near_mask
            thigh = inverse_limb(template, mask, src_hip, src_knee, hip, knee)
            shin = inverse_limb(template, mask, src_knee, src_ankle, knee, ankle)
            shoe = boot_layer(template, shoe_mask, ankle, angle)
            if far:
                thigh, shin, shoe = [dim(part, 0.85) for part in (thigh, shin, shoe)]
            result.alpha_composite(thigh)
            result.alpha_composite(shin)
            result.alpha_composite(shoe)
            joints.append({"leg": "far" if far else "near", "hip": hip, "knee": knee, "ankle": ankle, "shoeAngle": angle})
        result.alpha_composite(upper, (0, bob))
        frames.append(result)
        poses.append(joints)
    return frames, poses


def contact_sheet(frames, scale=5):
    cell = SIZE * scale
    sheet = Image.new("RGB", (cell * 8, cell * 2 + 48), (37, 40, 42))
    draw = ImageDraw.Draw(sheet)
    for index, frame in enumerate(frames):
        x, y = index % 8 * cell, index // 8 * (cell + 24)
        sprite = frame.resize((cell, cell), Image.Resampling.NEAREST)
        sheet.paste(sprite, (x, y), sprite)
        draw.text((x + 8, y + cell + 4), str(index).zfill(2), fill=(230, 235, 230))
    return sheet


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    frames, poses = build_frames()
    # Sample the wrap seam as well as all internal frame transitions.
    deltas = []
    knee_steps = []
    for index in range(FRAME_COUNT):
        for leg in range(2):
            a, b = poses[index][leg]["ankle"], poses[(index + 1) % FRAME_COUNT][leg]["ankle"]
            deltas.append(math.dist(a, b))
            knee_steps.append(math.dist(poses[index][leg]["knee"], poses[(index + 1) % FRAME_COUNT][leg]["knee"]))
    assert max(deltas) < 3.5, max(deltas)
    assert all(frame.size == (SIZE, SIZE) and frame.getbbox()[3] <= SIZE for frame in frames)
    for index, frame in enumerate(frames):
        frame.save(OUTPUT / f"Survivor_RunLeft_Continuous_{index:02d}.png")
    contact_sheet(frames).save(PREVIEW / "RunLeft_16Frames.png")
    preview_frames = []
    for frame in frames:
        preview = Image.new("RGB", (384, 384), (37, 40, 42))
        resized = frame.resize(preview.size, Image.Resampling.NEAREST)
        preview.paste(resized, mask=resized)
        preview_frames.append(preview)
    preview_frames[0].save(PREVIEW / "RunLeft_Continuous.gif", save_all=True,
                           append_images=preview_frames[1:], duration=[30, 40, 30] * 5 + [30], loop=0)
    comparison = []
    for index, frame in enumerate(frames):
        old = Image.open(SOURCE / f"Survivor_RunLeft_{index // 2:02d}.png").convert("RGBA")
        pair = Image.new("RGB", (768, 412), (37, 40, 42))
        for sprite, x in [(old, 0), (frame, 384)]:
            resized = sprite.resize((384, 384), Image.Resampling.NEAREST)
            pair.paste(resized, (x, 28), resized)
        draw = ImageDraw.Draw(pair)
        draw.text((12, 8), "Before", fill=(230, 235, 230))
        draw.text((396, 8), "After", fill=(230, 235, 230))
        comparison.append(pair)
    comparison[0].save(PREVIEW / "RunLeft_BeforeAfter.gif", save_all=True,
                       append_images=comparison[1:], duration=[30, 40, 30] * 5 + [30], loop=0)
    (PREVIEW / "RunLeftPoseData.json").write_text(json.dumps({"frames": poses, "maximumAnkleStepPixels": max(deltas), "maximumKneeStepPixels": max(knee_steps)}, indent=2))
    print(f"Built {FRAME_COUNT} frames; maximum ankle step including loop seam: {max(deltas):.3f}px")


if __name__ == "__main__":
    main()
