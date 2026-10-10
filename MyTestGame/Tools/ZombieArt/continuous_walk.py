"""Register generated art, then retouch leg/arm poses into one complete alternating walk."""
import math
from PIL import Image, ImageDraw

W, H = 80, 64


def polygon(points):
    mask = Image.new("L", (W, H))
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask


def segment(source, mask, a, b, target_a, target_b):
    layer = Image.new("RGBA", (W, H))
    sx, sy = b[0] - a[0], b[1] - a[1]
    dx, dy = target_b[0] - target_a[0], target_b[1] - target_a[1]
    sl, dl = math.hypot(sx, sy), math.hypot(dx, dy)
    for y in range(H):
        for x in range(W):
            rx, ry = x - target_a[0], y - target_a[1]
            t = (rx * dx + ry * dy) / (dl * dl)
            if not -.15 <= t <= 1.15:
                continue
            across = (rx * -dy + ry * dx) / dl
            px = round(a[0] + t * sx - across * sy / sl)
            py = round(a[1] + t * sy + across * sx / sl)
            if 0 <= px < W and 0 <= py < H and mask.getpixel((px, py)):
                layer.putpixel((x, y), source.getpixel((px, py)))
    return layer


def shift_pixels(source, mask, origin, target, angle=0):
    layer = Image.new("RGBA", (W, H))
    c, s = math.cos(angle), math.sin(angle)
    for y in range(H):
        for x in range(W):
            dx, dy = x - target[0], y - target[1]
            px = round(origin[0] + c * dx + s * dy)
            py = round(origin[1] - s * dx + c * dy)
            if 0 <= px < W and 0 <= py < H and mask.getpixel((px, py)):
                layer.putpixel((x, y), source.getpixel((px, py)))
    return layer


def dim(layer, factor=.82):
    result = layer.copy()
    result.putdata([(round(r * factor), round(g * factor), round(b * factor), a)
                    for r, g, b, a in layer.get_flattened_data()])
    return result


def front_walk(source, back=False):
    # Masks are authored on the generated, ground-registered idle sprites.
    top = Image.new("RGBA", (W, H))
    top.paste(source.crop((0, 0, W, 44)), (0, 0))
    left_leg = polygon([(32, 41), (39, 41), (39, 48), (38, 56), (32, 57)])
    right_leg = polygon([(40, 41), (47, 41), (47, 48), (48, 54), (40, 55)])
    left_boot = polygon([(32, 55), (39, 55), (39, 62), (31, 62)])
    right_boot = polygon([(40, 52), (47, 52), (48, 58), (40, 58)])
    arms = [
        (polygon([(27, 30), (32, 30), (33, 36), (33, 41), (31, 46), (26, 45), (26, 35)]), (30, 31), (29, 38), (29, 43)),
        (polygon([(47, 30), (51, 30), (53, 35), (53, 43), (50, 46), (47, 43)]), (49, 31), (51, 38), (50, 43))
    ]
    for mask, *_ in arms:
        for y in range(31, H):
            for x in range(W):
                if mask.getpixel((x, y)):
                    top.putpixel((x, y), (0, 0, 0, 0))
    frames, positions = [], []
    for i in range(8):
        phase = i / 8
        bob = round(.65 * math.cos(4 * math.pi * phase))
        legs = []
        frame = Image.new("RGBA", (W, H))
        for right, offset in ((False, 0), (True, .5)):
            p = (phase + offset) % 1
            # Plant -> travel backwards -> lift -> pass -> next planted contact.
            swing = max(0, (p - .5) * 2)
            lift = math.sin(math.pi * swing)
            depth = (1 - p * 2) if p < .5 else swing
            if back:
                depth = 1 - depth
            ankle_y = 55 + depth * 4 - lift * 3
            hip = (43 if right else 36, 42 + bob)
            ankle = (hip[0] + (.75 if right else -.75) * lift, ankle_y)
            knee = (hip[0] + (1 if right else -1) * lift,
                    hip[1] + (ankle[1] - hip[1]) * .52 - lift * 1.7)
            mask = right_leg if right else left_leg
            original = ((43, 42), (44, 48), (44, 53)) if right else ((36, 42), (36, 50), (35, 57))
            layer = Image.new("RGBA", (W, H))
            layer.alpha_composite(segment(source, mask, original[0], original[1], hip, knee))
            layer.alpha_composite(segment(source, mask, original[1], original[2], knee, ankle))
            boot = shift_pixels(source, right_boot if right else left_boot,
                                original[2], ankle)
            layer.alpha_composite(boot)
            legs.append((ankle_y, layer))
            positions.append((i, right, ankle))
        for _, layer in sorted(legs, key=lambda entry: entry[0]):
            frame.alpha_composite(layer)
        for index, (mask, shoulder, elbow, wrist) in enumerate(arms):
            stride = math.cos(2 * math.pi * (phase + index * .5))
            target_shoulder = (shoulder[0], shoulder[1] + bob)
            target_elbow = (elbow[0], elbow[1] + bob + stride * .7)
            target_wrist = (wrist[0], wrist[1] + bob + stride * 1.7)
            frame.alpha_composite(segment(source, mask, shoulder, elbow, target_shoulder, target_elbow))
            frame.alpha_composite(segment(source, mask, elbow, wrist, target_elbow, target_wrist))
        frame.alpha_composite(top, (0, bob))
        frames.append(frame)
    return frames, positions


def knee(hip, ankle):
    # Side view uses two rigid segments with the knee always bending toward screen right.
    thigh, shin = 8.5, 9.5
    dx, dy = ankle[0] - hip[0], ankle[1] - hip[1]
    distance = math.hypot(dx, dy)
    along = (thigh * thigh - shin * shin + distance * distance) / (2 * distance)
    out = math.sqrt(max(0, thigh * thigh - along * along))
    return (hip[0] + dx * along / distance + dy * out / distance,
            hip[1] + dy * along / distance - dx * out / distance)


def side_walk(source):
    top = Image.new("RGBA", (W, H))
    top.paste(source.crop((0, 0, W, 44)), (0, 0))
    leg_mask = polygon([(38, 41), (44, 41), (43, 49), (43, 56), (35, 57), (34, 52)])
    shoe_mask = polygon([(34, 55), (41, 55), (44, 58), (44, 62), (32, 62), (32, 57)])
    arm_mask = polygon([(40, 30), (46, 30), (46, 42), (44, 46), (41, 46), (40, 39)])
    for y in range(33, H):
        for x in range(W):
            if arm_mask.getpixel((x, y)):
                top.putpixel((x, y), (0, 0, 0, 0))
    frames, positions = [], []
    for i in range(8):
        phase = i / 8
        bob = round(.65 * math.cos(4 * math.pi * phase))
        frame = Image.new("RGBA", (W, H))
        for far, offset in ((True, .5), (False, 0)):
            p = (phase + offset) % 1
            hip = (39 if far else 41, 42 + bob)
            if p < .5:
                ankle = (hip[0] + 6 - p * 24, 58)
                lift = 0
            else:
                t = (p - .5) * 2
                lift = math.sin(math.pi * t)
                ankle = (hip[0] - 6 * math.cos(math.pi * t), 58 - 5 * lift)
            joint = knee(hip, ankle)
            layer = Image.new("RGBA", (W, H))
            layer.alpha_composite(segment(source, leg_mask, (41, 42), (39, 50), hip, joint))
            layer.alpha_composite(segment(source, leg_mask, (39, 50), (37, 57), joint, ankle))
            layer.alpha_composite(shift_pixels(source, shoe_mask, (37, 57), ankle, -.45 * lift))
            if far:
                layer = dim(layer)
            frame.alpha_composite(layer)
            positions.append((i, far, ankle))
        frame.alpha_composite(top, (0, bob))
        swing = -math.cos(2 * math.pi * phase)
        shoulder = (43, 31 + bob)
        elbow = (43 + swing * 1.5, 38 + bob)
        wrist = (43 + swing * 3, 43 + bob)
        frame.alpha_composite(segment(source, arm_mask, (43, 31), (43, 38), shoulder, elbow))
        frame.alpha_composite(segment(source, arm_mask, (43, 38), (43, 44), elbow, wrist))
        frames.append(frame)
    return frames, positions
