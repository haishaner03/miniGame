"""Retouch generated Hunter limb textures into a registered alternating eight-pose walk."""
import math
from PIL import Image
from continuous_walk import dim, knee, polygon, segment, shift_pixels


def front_walk(source, back=False):
    top = Image.new("RGBA", (80, 64))
    top.paste(source.crop((0, 0, 80, 43)), (0, 0))
    legs = [polygon([(28, 39), (38, 39), (39, 48), (38, 56), (28, 58)]),
            polygon([(42, 39), (53, 39), (54, 48), (53, 58), (42, 58)])]
    boots = [polygon([(26, 54), (38, 54), (40, 63), (25, 63)]),
             polygon([(42, 54), (54, 54), (57, 63), (41, 63)])]
    arms = [
        (polygon([(22, 24), (29, 24), (31, 32), (30, 45), (21, 46), (20, 35)]),
         (27, 27), (25, 35), (26, 43)),
        (polygon([(51, 24), (58, 24), (63, 34), (62, 46), (53, 46), (51, 35)]),
         (55, 27), (58, 35), (57, 43))]
    for mask, *_ in arms:
        for y in range(28, 64):
            for x in range(80):
                if mask.getpixel((x, y)):
                    top.putpixel((x, y), (0, 0, 0, 0))
    frames, joints = [], []
    for i in range(8):
        phase = i / 8
        bob = round(.65 * math.cos(4 * math.pi * phase))
        frame = Image.new("RGBA", (80, 64))
        layers = []
        for right, offset in ((False, 0), (True, .5)):
            index = int(right)
            p = (phase + offset) % 1
            swing = max(0, (p - .5) * 2)
            lift = math.sin(math.pi * swing)
            depth = 1 - p * 2 if p < .5 else swing
            if back:
                depth = 1 - depth
            hip = (47 if right else 33, 40 + bob)
            ankle = (hip[0] + (1 if right else -1) * lift, 54 + depth * 3 - lift * 3)
            joint = (hip[0] + (1 if right else -1) * lift,
                     hip[1] + (ankle[1]-hip[1]) * .52 - lift * 1.7)
            original = ((47, 40), (48, 49), (48, 57)) if right else ((33, 40), (33, 49), (32, 57))
            layer = Image.new("RGBA", (80, 64))
            layer.alpha_composite(segment(source, legs[index], original[0], original[1], hip, joint))
            layer.alpha_composite(segment(source, legs[index], original[1], original[2], joint, ankle))
            layer.alpha_composite(shift_pixels(source, boots[index], original[2], ankle))
            layers.append((ankle[1], layer))
            joints.append((i, right, ankle))
        for _, layer in sorted(layers, key=lambda pair: pair[0]):
            frame.alpha_composite(layer)
        frame.alpha_composite(top, (0, bob))
        for index, (mask, shoulder, elbow, wrist) in enumerate(arms):
            swing = math.cos(2 * math.pi * (phase + index * .5))
            a = (shoulder[0], shoulder[1] + bob)
            b = (elbow[0], elbow[1] + bob + swing * .8)
            c = (wrist[0], wrist[1] + bob + swing * 2)
            frame.alpha_composite(segment(source, mask, shoulder, elbow, a, b))
            frame.alpha_composite(segment(source, mask, elbow, wrist, b, c))
        frames.append(frame)
    return frames, joints


def side_walk(source):
    top = Image.new("RGBA", (80, 64))
    top.paste(source.crop((0, 0, 80, 43)), (0, 0))
    leg = polygon([(37, 38), (44, 38), (49, 44), (50, 53), (47, 58), (39, 58), (37, 49)])
    shoe = polygon([(40, 54), (53, 54), (56, 60), (55, 63), (39, 63)])
    arm = polygon([(25, 24), (34, 24), (36, 33), (35, 44), (25, 47), (22, 38)])
    for y in range(28, 64):
        for x in range(80):
            if arm.getpixel((x, y)):
                top.putpixel((x, y), (0, 0, 0, 0))
    frames, joints = [], []
    for i in range(8):
        phase = i / 8
        bob = round(.65 * math.cos(4 * math.pi * phase))
        frame = Image.new("RGBA", (80, 64))
        for far, offset in ((True, .5), (False, 0)):
            p = (phase + offset) % 1
            hip = (38 if far else 41, 41 + bob)
            if p < .5:
                ankle, lift = (hip[0] + 6 - p * 24, 57), 0
            else:
                t = (p - .5) * 2
                lift = math.sin(math.pi * t)
                ankle = (hip[0] - 6 * math.cos(math.pi * t), 57 - 5 * lift)
            joint = knee(hip, ankle)
            layer = Image.new("RGBA", (80, 64))
            layer.alpha_composite(segment(source, leg, (41, 41), (45, 49), hip, joint))
            layer.alpha_composite(segment(source, leg, (45, 49), (45, 57), joint, ankle))
            layer.alpha_composite(shift_pixels(source, shoe, (45, 57), ankle, -.4 * lift))
            frame.alpha_composite(dim(layer) if far else layer)
            joints.append((i, far, ankle))
        frame.alpha_composite(top, (0, bob))
        swing = -math.cos(2 * math.pi * phase)
        shoulder, elbow, wrist = (31, 27+bob), (28+swing, 35+bob), (29+swing*2.5, 43+bob)
        frame.alpha_composite(segment(source, arm, (31, 27), (28, 35), shoulder, elbow))
        frame.alpha_composite(segment(source, arm, (28, 35), (29, 43), elbow, wrist))
        frames.append(frame)
    return frames, joints
