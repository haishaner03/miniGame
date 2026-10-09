# Renders a preview PNG of the generated layout and checks connectivity per gate state.
import sys, os
from collections import deque
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import level02_gen as L

W, H = L.W, L.H
ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'GameMain', 'Res', 'Map')
PPU = 32  # preview pixels per world unit

def load(p, sub=None, size=None):
    im = Image.open(os.path.join(ART, p)).convert('RGBA')
    if sub:
        im = im.crop((sub[0] * 64, sub[1] * 64, sub[0] * 64 + 64, sub[1] * 64 + 64))
    if size:
        im = im.resize(size, Image.LANCZOS)
    return im

sheet_grass = os.path.join('01_Grass_4x4.png')
sheet_road = os.path.join('02_Road_4x4.png')
sheet_side = 'StreetBlock01/StreetRedesign'

def sheet_tile(sheet, col, row_vis, px):
    im = load(sheet, (col, 3 - row_vis))
    return im.resize((px, px), Image.NEAREST)

# ground char -> (kind, args)
def ground_img(ch, px, x, y):
    def gr(col, row_vis): return sheet_tile(sheet_grass, col, row_vis, px)
    def rd(col, row_vis): return sheet_tile(sheet_road, col, row_vis, px)
    if ch in 'Rh': return rd(3, 2)
    if ch == 'y': return sheet_tile(sheet_road, 0, 0, px).rotate(90)
    if ch == 'v': return rd(1, 0, px) if False else sheet_tile(sheet_road, 1, 0, px)
    if ch == 'h': return sheet_tile(sheet_road, 1, 0, px).rotate(90)
    if ch == 'S' or ch == 'C':
        RD = 'Rhvy'
        n = L.G[y + 1][x] in RD if y < H - 1 else False
        e = L.G[y][x + 1] in RD if x < W - 1 else False
        s = L.G[y - 1][x] in RD if y > 0 else False
        w = L.G[y][x - 1] in RD if x > 0 else False
        if not (n or e or s or w) and ch == 'C': return load(f'{sheet_side}/CourtyardConcrete.png').resize((px, px), Image.NEAREST)
        if not (n or e or s or w): return load(f'{sheet_side}/Sidewalk_00.png').resize((px, px), Image.NEAREST)
        mask = n * 1 + e * 2 + s * 4 + w * 8
        idx = {1: 1, 2: 2, 3: 3, 4: 4, 5: 5, 6: 6, 7: 7, 8: 8, 9: 9, 10: 10, 11: 11, 12: 12, 13: 13, 14: 14, 15: 15}[mask]
        name = f'Sidewalk_{idx:02d}.png'
        return load(f'{sheet_side}/{name}').resize((px, px), Image.NEAREST)
    if ch == 'd': return [gr(2, 3), gr(3, 0), gr(2, 1)][(x * 7 + y * 3) % 3]
    if ch == 'g': return gr(0, 3)
    if ch == '#': return [gr(0, 3), gr(3, 3), gr(0, 3), gr(3, 1)][(x * 5 + y * 3) % 4]
    if ch == 'B': return load(f'{sheet_side}/CourtyardConcrete.png').resize((px, px), Image.NEAREST)
    return gr(0, 0)

PROP_TILE = {
    'W': ('Polished/Tile_WreckedCar.asset', 'WreckedCar'), 'N': ('', 'AbandonedSedan'),
    'F': ('', 'ChainFence'), 'f': ('', 'WoodFence'), 'X': ('', 'RoadBarricade'),
    'D': ('', 'DamagedBarricade'), 'Q': ('', 'Sandbags'), 'O': ('', 'RustBarrel'),
    'T': ('', 'DeadTree'), 'K': ('', 'RockLarge'), 'k': ('', 'RockPile'),
    'U': ('', 'RockTall'), 'J': ('', 'WasteBin'), '^': ('', 'Scrub'),
    ',': ('', 'DryGrass'), ';': ('', 'DryGrassTall'), 'o': ('', 'WornAsphalt_puddle'),
    '*': ('', 'WornAsphalt_blood'),
}
PROP_PNG = {'WreckedCar': 2.0, 'AbandonedSedan': 1.5, 'ChainFence': 1.0, 'WoodFence': 1.0,
            'RoadBarricade': 1.5, 'DamagedBarricade': 1.5, 'Sandbags': 1.5, 'RustBarrel': 1.0,
            'DeadTree': 2.4, 'RockLarge': 1.0, 'RockPile': 1.0, 'RockTall': 1.2, 'WasteBin': 0.8,
            'Scrub': 0.8, 'DryGrass': 0.8, 'DryGrassTall': 0.8}

def draw_prop(im, ch, cx, cy, px):
    """cx, cy are world-space centres; multi-cell props are drawn at footprint centre."""
    if ch in 'WwNn':
        png = 'WreckedCar' if ch in 'Ww' else 'AbandonedSedan'
    else:
        png = PROP_TILE.get(ch, ('', 'Scrub'))[1]
        if png not in PROP_PNG:
            png = 'Scrub'
    s = int(PROP_PNG[png] * PPU)
    art = load(f'StreetBlock01/Polished/{png}.png', None, (s, s))
    im.alpha_composite(art, (int(cx * PPU - art.width / 2), int(cy * PPU - art.height / 2)))

def render(open_gates=()):
    px = PPU
    def cy(y): return (H - 1 - y) * px          # cell row -> image top
    def wy(v): return (H - v) * px               # world y -> image y
    im = Image.new('RGBA', (W * px, H * px), (26, 26, 30, 255))
    for y in range(H):
        for x in range(W):
            im.alpha_composite(ground_img(L.G[y][x], px, x, y), (x * px, cy(y)))
    for y in range(H):
        for x in range(W):
            c = L.P[y][x]
            if c == 'o': im.alpha_composite(sheet_tile(sheet_road, 0, 1, px), (x * px, cy(y)))
            elif c == '*': im.alpha_composite(sheet_tile(sheet_road, 2, 1, px), (x * px, cy(y)))
    def put(art, wx, wyc):
        im.alpha_composite(art, (int(wx * px - art.width / 2), int(wy(wyc) - art.height / 2)))
    def prop_art(c):
        png = 'WreckedCar' if c in 'Ww' else 'AbandonedSedan' if c in 'Nn' else PROP_TILE.get(c, ('', 'Scrub'))[1]
        if png not in PROP_PNG: png = 'Scrub'
        s_ = int(PROP_PNG[png] * PPU)
        return load(f'StreetBlock01/Polished/{png}.png', None, (s_, s_))
    order = sorted(((y, x) for y in range(H) for x in range(W)), key=lambda t: -t[0])
    for (y, x) in order:
        c = L.P[y][x]
        if c in PROP_TILE and c not in 'WwNno*':
            put(prop_art(c), x + 0.5, y + 0.5)
        elif c == 'W':
            put(prop_art(c), x + 1.0, y + 1.0)
        elif c == 'N':
            put(prop_art(c), x + 0.5, y + 1.0)
    for (name, key, x0, y0, w, h) in sorted(L.buildings, key=lambda b: -b[3]):
        path = f'StreetBlock01/Polished/GptImage2/{key}.png' if 'GptImage' in key else (
            f'StreetBlock01/Buildings/{key}.png' if key.startswith('Building_') else f'{sheet_side}/{key}.png')
        art = load(path)
        art = art.resize((w * px, int(art.height / art.width * w * px)), Image.LANCZOS)
        im.alpha_composite(art, (int(x0 * px), int(wy(y0) - art.height)))
    d = ImageDraw.Draw(im, 'RGBA')
    for (tid, x0, y0, x1, y1, n, sp) in L.triggers:
        d.rectangle([x0 * px, wy(y1 + 1), (x1 + 1) * px - 1, wy(y0) - 1], outline=(255, 90, 60, 230), width=3)
        d.text((x0 * px + 6, wy(y1 + 1) + 6), f'{tid} x{n}', fill=(255, 220, 120, 255))
    for name, pos in L.spawns.items():
        X, Y = pos[0] * px, wy(pos[1])
        d.ellipse([X - 9, Y - 9, X + 9, Y + 9], fill=(255, 40, 40, 210), outline=(255, 255, 255, 255))
        d.text((X + 12, Y - 8), name, fill=(255, 255, 255, 255))
    for (gid, mark, wave) in L.gates:
        for y in range(H):
            for x in range(W):
                if L.P[y][x] == mark:
                    a_ = 25 if mark in open_gates else 90
                    d.rectangle([x * px, cy(y), (x + 1) * px - 1, cy(y) + px - 1], fill=(255, 230, 0, a_), outline=(255, 230, 0, 160))
    for nm, (X_, Y_) in [('START', L.start_door), ('EXIT', L.exit_door)]:
        X, Y = X_ * px, wy(Y_)
        d.ellipse([X - 12, Y - 12, X + 12, Y + 12], outline=(0, 255, 120, 255), width=4)
        d.text((X + 14, Y - 6), nm, fill=(0, 255, 120, 255))
    return im

def walkable(x, y, open_gates):
    if not (0 <= x < W and 0 <= y < H): return False
    g = L.G[y][x]
    if g not in set('RhvySCdg'): return False
    p = L.P[y][x]
    if p in set('WwNnFfXDQOTKkUJ'):
        return False
    if p in ('1', '2') and p not in open_gates: return False
    return True

def reachable(open_gates):
    start = (int(L.player_spawn[0]), int(L.player_spawn[1]))
    seen = {start}; q = deque([start])
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n not in seen and walkable(n[0], n[1], open_gates):
                seen.add(n); q.append(n)
    return seen

if __name__ == '__main__':
    print('=== ground ===')
    for y in range(H - 1, -1, -1): print(f'{y:2d} ' + ''.join(L.G[y]))
    print('=== props ===')
    for y in range(H - 1, -1, -1): print(f'{y:2d} ' + ''.join(L.P[y]))

    closed = reachable(())
    print('\nzone A cells reachable (gates shut):', len(closed))
    for nm, pos in [('A1', L.spawns['A1']), ('exit', L.exit_door)] + [(k, v) for k, v in L.spawns.items() if k[1:] != '' and k[0] != 'A']:
        ok = (int(pos[0]), int(pos[1])) in closed
        print(f'  {nm:5s} reachable={ok}')
    g1 = reachable(('1',))
    print("\nafter gate1:", 'B1', (int(L.spawns['B1'][0]), int(L.spawns['B1'][1])) in g1,
          'B3', (int(L.spawns['B3'][0]), int(L.spawns['B3'][1])) in g1,
          'C1', (int(L.spawns['C1'][0]), int(L.spawns['C1'][1])) in g1)
    both = reachable(('1', '2'))
    print("after both gates:", 'exit cells', (int(L.exit_door[0]), int(L.exit_door[1])) in both,
          'C3', (int(L.spawns['C3'][0]), int(L.spawns['C3'][1])) in both,
          'C4', (int(L.spawns['C4'][0]), int(L.spawns['C4'][1])) in both,
          'total', len(both))
    for (nm, pos) in list(L.spawns.items()):
        if (int(pos[0]), int(pos[1])) not in both: print('  WARNING spawn walled off:', nm, pos)
    for (nm, pos) in [('start', L.start_door), ('exit', L.exit_door), ('player', L.player_spawn)]:
        if (int(pos[0]), int(pos[1])) not in both: print('  WARNING object walled off:', nm, pos)

    render(('1', '2')).save(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'level02_preview_all.png'))
    render(()).save(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'level02_preview_start.png'))
    print('previews written')
