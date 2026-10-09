from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont

PROJECT = Path('D:/UnityProject/miniGame/MyTestGame')
HERE = Path(__file__).parent
SOURCE = PROJECT / 'Assets/GameMain/Res/Map/StreetBlock01/Polished'
DEST = PROJECT / 'Assets/GameMain/Res/Map/StreetBlock01/StreetRedesign'
DEST.mkdir(parents=True, exist_ok=True)
assets = []


def save(im, name, ppu=64, bottom=False, tile=False):
    im.save(DEST / (name + '.png'))
    assets.append(dict(name=name, ppu=ppu, bottom=bottom, tile=tile,
                       width=im.width, height=im.height))


# Reuse the generated concrete/curb material, with no transparent tile gutters.
atlas = Image.open(SOURCE / 'GptImage2/Curb_StreetEdge_4x4_GptImage2.png').convert('RGB')
patch = atlas.crop((30, 6, 51, 20)).resize((64, 64), Image.Resampling.NEAREST)
material = np.asarray(patch).astype(float)
rng = np.random.default_rng(106064)
material = material * .65 + rng.integers(-3, 4, (64, 64, 1))
base = Image.fromarray(np.uint8(np.clip(material, 0, 255)))
edge = atlas.crop((6, 23, 56, 35)).resize((64, 7), Image.Resampling.NEAREST)
edge = Image.fromarray(np.uint8(np.asarray(edge).astype(float) * .78))
for mask in range(16):
    im = base.copy()
    d = ImageDraw.Draw(im)
    # A quiet expansion joint rather than a heavy border around every cell.
    d.line((0, 63, 63, 63), fill=(97, 98, 90))
    if mask & 1:
        im.paste(edge.transpose(Image.Transpose.FLIP_TOP_BOTTOM), (0, 0))
    if mask & 2:
        im.paste(edge.transpose(Image.Transpose.ROTATE_90), (57, 0))
    if mask & 4:
        im.paste(edge, (0, 57))
    if mask & 8:
        im.paste(edge.transpose(Image.Transpose.ROTATE_270), (0, 0))
    save(im, 'Sidewalk_' + str(mask).zfill(2), tile=True)
save(base, 'CourtyardConcrete', tile=True)

# Small convex corner patches connect the end of two straight curb strips.
for name, corner in [('NE', (57, 0)), ('SE', (57, 57)), ('SW', (0, 57)), ('NW', (0, 0))]:
    im = base.copy()
    d = ImageDraw.Draw(im)
    x, y = corner
    d.rectangle((x, y, x + 6, y + 6), fill=(116, 108, 72))
    d.rectangle((x + 2, y + 2, x + 5, y + 5), fill=(70, 72, 67))
    save(im, 'Sidewalk_Corner_' + name, tile=True)


# Rectify the existing painted roof, reducing the side-facing perspective.
store = Image.open(PROJECT / 'Assets/GameMain/Res/Map/StreetBlock01/Buildings3D/Building_ConvenienceStore_GptImage2.png').convert('RGBA')
roof = store.transform((640, 220), Image.Transform.QUAD,
                       (149, 248, 410, 478, 1382, 331, 1118, 53),
                       Image.Resampling.BICUBIC)
shop = Image.open(SOURCE / 'GptImage2/Building_RowShop_GptImage2.png').convert('RGBA')
apartment = Image.open(SOURCE / 'GptImage2/Building_LowApartment_GptImage2.png').convert('RGBA')
safe = Image.open(SOURCE / 'GptImage2/Building_SafehouseFront_GptImage2.png').convert('RGBA')


def building(source, rect, name, width, roof_height, parapet_rect=None):
    front = source.crop(rect)
    front = front.resize((width, round(front.height * width / front.width)), Image.Resampling.LANCZOS)
    cap = roof.resize((width, roof_height), Image.Resampling.LANCZOS)
    out = Image.new('RGBA', (width, front.height + roof_height - 5))
    out.alpha_composite(cap, (0, 0))
    out.alpha_composite(front, (0, roof_height - 5))
    if parapet_rect:
        band = source.crop(parapet_rect).resize((width, 12), Image.Resampling.LANCZOS)
        out.alpha_composite(band, (0, roof_height - 10))
    # Align opaque ground contact with the bottom pivot; no cast shadow is added.
    out = out.crop(out.getbbox())
    save(out, name, bottom=True)


building(shop, (36, 466, 890, 807), 'Shop_TwoUnits_A', 384, 104, (40, 190, 890, 220))
building(shop, (880, 466, 1735, 807), 'Shop_TwoUnits_B', 384, 104, (880, 190, 1735, 220))
building(apartment, (31, 180, 930, 802), 'Apartment_TwoUnits_A', 384, 94)
building(apartment, (921, 180, 1741, 802), 'Apartment_TwoUnits_B', 352, 94)
building(safe, (488, 274, 1294, 770), 'Safehouse_CentralGate', 384, 102)

# Useful ground wear comes from the existing art rather than a new art style.
trash = store.crop((1110, 831, 1409, 992))
trash = trash.resize((92, 50), Image.Resampling.LANCZOS)
save(trash, 'Debris_Pile', bottom=True)
planks = store.crop((205, 654, 314, 780)).resize((34, 40), Image.Resampling.LANCZOS)
save(planks, 'Discarded_Planks', bottom=True)

manifest = dict(folder=str(DEST.relative_to(PROJECT)).replace('\\', '/'), assets=assets)
(HERE / 'asset_manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')

preview = Image.new('RGB', (1000, 870), '#20251e')
d = ImageDraw.Draw(preview)
font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 17)
names = ['Shop_TwoUnits_A', 'Shop_TwoUnits_B', 'Apartment_TwoUnits_A',
         'Apartment_TwoUnits_B', 'Safehouse_CentralGate']
for i, name in enumerate(names):
    im = Image.open(DEST / (name + '.png'))
    im.thumbnail((440, 280))
    x, y = 20 + (i % 2) * 490, 20 + (i // 2) * 285
    preview.paste(im, (x, y + 24), im)
    d.text((x, y), name, fill='white', font=font)
preview.save(HERE / 'Building_Modules_Preview.png')
print(json.dumps(manifest, indent=2))
