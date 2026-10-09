"""Mirror the approved continuous left-run frames into a matching right run."""

from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageOps

from fix_runleft_cycle import FRAME_COUNT, SIZE, contact_sheet


PROJECT = Path(__file__).resolve().parents[2]
CHARACTER = PROJECT / "Assets/GameMain/Res/Characters/Survivor/Generated"
SOURCE = CHARACTER / "Continuous/RunLeft"
OUTPUT = CHARACTER / "Continuous/RunRight"
PREVIEW = PROJECT / "Assets/Screenshots/RunRightContinuity"
DURATIONS = [30, 40, 30] * 5 + [30]


def main():
    sources = [SOURCE / f"Survivor_RunLeft_Continuous_{index:02d}.png"
               for index in range(FRAME_COUNT)]
    frames = []
    for source in sources:
        with Image.open(source) as image:
            left = image.convert("RGBA")
        if left.size != (SIZE, SIZE) or left.getbbox() is None:
            raise ValueError(f"Invalid source frame: {source}")
        right = ImageOps.mirror(left)
        assert ImageChops.difference(left, ImageOps.mirror(right)).getbbox() is None
        frames.append(right)

    OUTPUT.mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    for index, frame in enumerate(frames):
        frame.save(OUTPUT / f"Survivor_RunRight_Continuous_{index:02d}.png")
    contact_sheet(frames).save(PREVIEW / "RunRight_16Frames.png")

    previews, comparisons = [], []
    for index, frame in enumerate(frames):
        resized = frame.resize((384, 384), Image.Resampling.NEAREST)
        preview = Image.new("RGB", (384, 384), (37, 40, 42))
        preview.paste(resized, mask=resized)
        previews.append(preview)

        old_path = CHARACTER / "Normalized/RunRight" / f"Survivor_RunRight_{index // 2:02d}.png"
        with Image.open(old_path) as image:
            old = image.convert("RGBA").resize((384, 384), Image.Resampling.NEAREST)
        pair = Image.new("RGB", (768, 412), (37, 40, 42))
        pair.paste(old, (0, 28), old)
        pair.paste(resized, (384, 28), resized)
        draw = ImageDraw.Draw(pair)
        draw.text((12, 8), "Before", fill=(230, 235, 230))
        draw.text((396, 8), "After", fill=(230, 235, 230))
        comparisons.append(pair)

    for sequence, name in [(previews, "RunRight_Continuous.gif"),
                           (comparisons, "RunRight_BeforeAfter.gif")]:
        sequence[0].save(PREVIEW / name, save_all=True, append_images=sequence[1:],
                         duration=DURATIONS, loop=0)
    print(f"Built {len(frames)} right-run frames from the approved left run; left assets unchanged.")


if __name__ == "__main__":
    main()
