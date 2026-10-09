import os, json, base64, urllib.request, pathlib

key = os.environ.get('XINDU_KEY')
if not key:
    raise SystemExit('XINDU_KEY is not set')

outdir = pathlib.Path(r'D:\UnityProject\miniGame\.codex_artifacts\building_rework\batch_gpt_image2')
outdir.mkdir(parents=True, exist_ok=True)

items = [
    ('Building_RowShop_GptImage2', '''Use case: stylized-concept. Asset type: Unity 2D zombie-survival neighborhood building sprite. Generate a wide abandoned row shop designed to sit directly beside other buildings. Front-biased orthographic top-down view: about 75 percent of the image is the road-facing front facade, with only a very narrow roof plane and minimal side depth visible. Rectangular footprint, left and right walls end in clean vertical edges so neighboring buildings can touch or nearly touch without a large perspective gap. Weathered gray-green concrete, rust-red roof trim, broken dark windows, boarded sections, cracked plaster, small entrance steps, weeds and rubble only along the bottom contact edge. High-quality detailed pixel art, crisp pixel edges, dark zombie-street mood, consistent lighting. Centered full silhouette, directly grounded, transparent background, no large cast shadow, no readable text, no characters, no watermark, no grid lines.'''),
    ('Building_LowApartment_GptImage2', '''Use case: stylized-concept. Asset type: Unity 2D zombie-survival neighborhood building sprite. Generate a broad low-rise abandoned apartment row for a dense street block. Front-facing top-down composition with the facade dominant, around 80 percent front and only a thin roof strip visible; keep side walls extremely narrow. Wide rectangular footprint with flush left and right boundaries for adjacent placement. Two-story facade, repeated broken windows, small balconies and awnings, faded gray concrete, rusted metal, vines and a few boarded openings. Keep the building body coherent and straight, with bottom edge touching the ground and only a small amount of rubble at the base. High-quality hand-pixel art, crisp readable details, dark but cohesive zombie neighborhood palette. Transparent background, no large shadow, no text, no watermark, no characters, no grid lines.'''),
    ('Building_SafehouseFront_GptImage2', '''Use case: stylized-concept. Asset type: Unity 2D zombie-survival start and exit safehouse sprite. Generate a wide reinforced safehouse viewed from a front-biased orthographic top-down angle. The front facade must dominate the image, with only a narrow roof plane and almost no side wall visible. Wide rectangular footprint that can be placed tightly beside other buildings. Centered reinforced double entrance, roll-up metal door, barricade panels, warning lamps, sandbags and a small sign shape with no readable text. Weathered concrete and rusted steel, subtle weeds and debris only on the bottom ground-contact strip. Detailed pixel art with clean silhouette, grounded directly on the map, transparent background, no large cast shadow, no readable text, no watermark, no characters, no grid lines.'''),
    ('Curb_StreetEdge_4x4_GptImage2', '''Use case: stylized-concept. Asset type: Unity 2D modular curb and sidewalk edge tile atlas for a zombie-survival street map. Create a strict 4 columns by 4 rows atlas containing exactly 16 equal square tiles, all tiles isolated and aligned to the same pixel grid with transparent gutters and no overlap. Top-down orthographic pixel art. Include several horizontal and vertical curb straights, inside corners, outside corners, T-junctions, four-way junction, end caps, and two damaged transition pieces where cracked concrete curb meets muddy soil and grass. Make the curb visibly richer than a plain gray strip: chipped concrete, exposed aggregate, broken paint, drainage grates, weeds, small cracks and rubble, but keep every tile modular and easy to tile. No grid lines, no labels, no text, no watermark, no characters, transparent background.'''),
]

results = []
for name, prompt in items:
    body = {'model': 'gpt-image-2', 'prompt': prompt, 'size': '1024x1024'}
    request = urllib.request.Request(
        'https://xindu.xyz/v1/images/generations',
        data=json.dumps(body).encode(),
        headers={'Authorization': 'Bearer ' + key, 'Content-Type': 'application/json', 'Accept': 'application/json'},
        method='POST',
    )
    try:
        with urllib.request.urlopen(request, timeout=240) as response:
            data = json.loads(response.read())
        (outdir / (name + '_response.json')).write_text(json.dumps(data, ensure_ascii=False), encoding='utf-8')
        entries = data.get('data') or []
        if not entries:
            raise RuntimeError('no data returned')
        entry = entries[0]
        if entry.get('b64_json'):
            image = base64.b64decode(entry['b64_json'])
        elif entry.get('url'):
            image = urllib.request.urlopen(entry['url'], timeout=240).read()
        else:
            raise RuntimeError('no b64_json or url')
        path = outdir / (name + '.png')
        path.write_bytes(image)
        result = {'name': name, 'path': str(path), 'bytes': len(image), 'size': entry.get('size'), 'model': entry.get('model')}
        results.append(result)
        print(json.dumps(result, ensure_ascii=False), flush=True)
    except Exception as exc:
        result = {'name': name, 'error': repr(exc)}
        results.append(result)
        print(json.dumps(result, ensure_ascii=False), flush=True)

print(json.dumps({'results': results}, ensure_ascii=False))
