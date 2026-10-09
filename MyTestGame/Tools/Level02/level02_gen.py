# Generates ZombieLevel02 layout (ASCII layers + object lists) and writes the C# data file.
import random, sys
W, H = 64, 40
G = [['#'] * W for _ in range(H)]   # ground
P = [['.'] * W for _ in range(H)]   # props
rng = random.Random(20261008)

def rect(layer, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            layer[y][x] = c

def prop(x, y, c):
    P[y][x] = c

def wreck(x, y):  # 2x2
    for dx in (0, 1):
        for dy in (0, 1):
            P[y + dy][x + dx] = 'w'
    P[y][x] = 'W'

def sedan(x, y):  # 1x2 vertical
    P[y][x] = 'N'; P[y + 1][x] = 'n'

buildings = []  # (name, spriteKey, x0, y0, w, h)
def building(name, key, x0, y0, w, h):
    rect(G, x0, y0, x0 + w - 1, y0 + h - 1, 'B')
    buildings.append((name, key, x0, y0, w, h))

# ---------------- Zone A : lower street ----------------
rect(G, 2, 6, 40, 7, 'S')             # south sidewalk
rect(G, 2, 8, 40, 12, 'R')            # lower road
rect(G, 2, 13, 40, 14, 'S')           # north sidewalk
rect(G, 28, 8, 32, 33, 'R')           # cross street (A -> B -> upper)
for x in range(3, 41):
    if not 28 <= x <= 32: G[10][x] = 'y'
for y in range(13, 29): G[y][30] = 'v'
# west dead end: barricade wall + wreck
for y in range(6, 15): prop(2, y, 'X' if y % 2 else 'D')
wreck(3, 10); prop(3, 8, 'O')
# east dead end: damaged barricade with a gap, spawn A3 behind it
for y in range(6, 15):
    if y != 11: prop(38, y, 'D')
# parking lot south
rect(G, 12, 1, 25, 5, 'd')
for x in range(12, 26):
    if x not in (15, 16, 22, 23): prop(x, 5, 'F')
wreck(13, 2); sedan(18, 1); sedan(20, 2); wreck(23, 1); prop(17, 4, 'O'); prop(25, 1, 'O')
# start safehouse + alley + shops (north row)
building('Start_Safehouse', 'Safehouse_CentralGate', 3, 15, 6, 6)
rect(G, 9, 15, 10, 19, 'd'); prop(9, 19, 'J')
building('Shop_A_West', 'Shop_TwoUnits_A', 11, 15, 6, 4)
building('Shop_B_West', 'Shop_TwoUnits_B', 17, 15, 6, 4)
building('Apt_A_East', 'Apartment_TwoUnits_B', 35, 15, 6, 6)
prop(3, 13, 'Q'); prop(8, 13, 'Q'); prop(12, 13, 'J'); prop(24, 13, 'O')
sedan(16, 8); wreck(21, 11); prop(25, 7, 'X')
# cross street sidewalks
rect(G, 26, 15, 27, 26, 'S'); rect(G, 33, 15, 34, 26, 'S')

# ---------------- Zone B : market plaza + cross street ----------------
rect(G, 11, 21, 25, 26, 'C')
rect(G, 14, 23, 16, 24, 'g'); rect(G, 20, 23, 22, 24, 'g')
prop(15, 24, 'T'); prop(21, 23, 'K'); prop(22, 24, 'k'); prop(12, 25, 'O'); prop(13, 25, 'O')
wreck(18, 21); prop(24, 26, 'J'); prop(11, 21, 'U')
rect(G, 8, 22, 10, 24, 'd')            # west alley (spawn B2)
building('Apt_Plaza_West', 'Apartment_TwoUnits_A', 11, 27, 6, 6)
rect(G, 17, 27, 18, 30, 'd')           # north alley (spawn B3)
building('Apt_Plaza_East', 'Apartment_TwoUnits_B', 19, 27, 6, 6)
rect(G, 35, 22, 38, 24, 'd'); prop(38, 24, 'O')   # east alley (spawn B1)
# pileup in the cross street: forces a lane choice
wreck(28, 21); prop(31, 22, 'X'); prop(32, 22, 'X'); sedan(32, 18); prop(29, 25, 'D')

# ---------------- Upper street (B west part, C east part) ----------------
rect(G, 26, 27, 27, 28, 'S'); rect(G, 33, 27, 60, 28, 'S')
rect(G, 26, 29, 62, 33, 'R')
for x in range(26, 63):
    if not 28 <= x <= 32: G[31][x] = 'h'
rect(G, 26, 34, 62, 35, 'S')
for y in range(29, 34): prop(26, y, 'X' if y % 2 else 'D')
building('Shop_Row_North', 'Building_RowShop_GptImage2', 27, 36, 7, 4)
rect(G, 34, 36, 35, 38, 'd')           # alley (spawn B4)
building('Shop_C_North', 'Shop_TwoUnits_B', 41, 36, 6, 4)
rect(G, 47, 36, 48, 38, 'd')           # alley (spawn C1)
building('Apt_Low_North', 'Building_LowApartment_GptImage2', 49, 36, 7, 4)
building('Evac_Safehouse', 'Building_SafehouseFront_GptImage2', 56, 36, 7, 4)
# zone C: sandbag chicane + overrun east barricade
for y in range(27, 32): prop(45, y, 'Q')
for y in range(31, 36): prop(52, y, 'Q')
for y in range(27, 34):
    if y != 30: prop(61, y, 'D')
sedan(49, 29); prop(56, 30, 'O'); prop(57, 32, 'K')
# military yard south of zone C
rect(G, 42, 19, 58, 25, 'd'); rect(G, 42, 26, 58, 26, 'd')
for x in range(42, 59):
    if x not in (48, 49, 50): prop(x, 26, 'F')
wreck(44, 21); sedan(55, 21); prop(50, 21, 'Q'); prop(51, 21, 'Q'); prop(52, 21, 'Q')
prop(46, 24, 'O'); prop(53, 24, 'O'); prop(58, 19, 'O')

# gates (prop digits, opened at runtime)
rect(P, 26, 15, 34, 16, '1')
rect(P, 37, 27, 38, 35, '2')

# ---------------- dressing ----------------
WALK = set('RhvySCdg')
BLOCKP = set('WwNnFfXDQOTKkUJ12')

# Fill the leftover grass with city-block roofs so the streets read as enclosed,
# instead of an endless field. Largest sprites first, aligned to a coarse grid.
ROOFS = [
    ('Apartment_TwoUnits_A', 6, 6), ('Apartment_TwoUnits_B', 6, 6),
    ('Safehouse_CentralGate', 6, 6),
    ('Building_RowShop_GptImage2', 7, 4), ('Building_LowApartment_GptImage2', 7, 4),
    ('Building_SafehouseFront_GptImage2', 7, 4),
    ('Shop_TwoUnits_A', 6, 4), ('Shop_TwoUnits_B', 6, 4),
    ('Building_Apartment', 6, 4),
]
def can_roof(x0, y0, w, h):
    if x0 < 0 or y0 < 0 or x0 + w > W or y0 + h > H: return False
    for y in range(y0, y0 + h):
        for x in range(x0, x0 + w):
            if G[y][x] != '#' or P[y][x] != '.': return False
    return True

# Try sizes big->small, but rotate sprite choice so neighbours differ.
SIZES = [(6, 6, ['Apartment_TwoUnits_A', 'Apartment_TwoUnits_B']),
         (7, 4, ['Building_RowShop_GptImage2', 'Building_LowApartment_GptImage2']),
         (6, 4, ['Shop_TwoUnits_A', 'Shop_TwoUnits_B', 'Building_Apartment'])]
counter = 0
for (w, h, keys) in SIZES:
    for y0 in range(1, H - h, 1):
        for x0 in range(1, W - w, 1):
            if can_roof(x0, y0, w, h):
                key = keys[counter % len(keys)]; counter += 1
                rect(G, x0, y0, x0 + w - 1, y0 + h - 1, 'B')
                buildings.append((f'Block_{len(buildings):02d}_{key}', key, x0, y0, w, h))

def walk_g(x, y): return 0 <= x < W and 0 <= y < H and G[y][x] in WALK
for y in range(H):
    for x in range(W):
        if G[y][x] == '#' and P[y][x] == '.':
            near = any(walk_g(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1))
            r = rng.random()
            if near and r < 0.18: P[y][x] = rng.choice('TTKkUJ^;')
            elif not near and r < 0.05: P[y][x] = rng.choice('T^;')
        elif G[y][x] in 'Rh' and P[y][x] == '.':
            r = rng.random()
            if r < 0.035: P[y][x] = 'o'
            elif r < 0.06: P[y][x] = '*'
        elif G[y][x] in 'SC' and P[y][x] == '.' and rng.random() < 0.05:
            P[y][x] = rng.choice(',;')
        elif G[y][x] == 'd' and P[y][x] == '.' and rng.random() < 0.08:
            P[y][x] = rng.choice('^,;*')
# extra blood around the wave trigger zones
for (cx, cy) in [(30, 11), (30, 20), (44, 31), (59, 33)]:
    for _ in range(5):
        x, y = cx + rng.randint(-2, 2), cy + rng.randint(-2, 2)
        if G[y][x] in 'Rhv' and P[y][x] == '.': P[y][x] = '*'

# ---------------- objects ----------------
spawns = {
    'A1': (10.0, 17.5), 'A2': (19.5, 3.5), 'A3': (39.5, 10.5),
    'B1': (37.0, 23.5), 'B2': (8.5, 23.5), 'B3': (18.0, 29.5), 'B4': (35.0, 37.5),
    'C1': (48.0, 37.5), 'C2': (43.5, 19.5), 'C3': (62.5, 30.5), 'C4': (57.5, 24.5),
}
triggers = [  # id, x0,y0,x1,y1 (cells inclusive), count, spawn ids
    ('Wave_A', 24, 6, 36, 14, 7, ['A1', 'A2', 'A3']),
    ('Wave_B', 26, 18, 34, 22, 10, ['B1', 'B2', 'B3', 'B4']),
    ('Wave_C', 40, 27, 46, 35, 14, ['C1', 'C2', 'C3', 'C4']),
]
gates = [('Gate_1', '1', 'Wave_A'), ('Gate_2', '2', 'Wave_B')]
start_door = (6.0, 14.3); player_spawn = (6.0, 13.5)
exit_door = (59.5, 35.4)
ambient = ['A2', 'A3']


def emit_cs(path):
    def v2(p): return f'new Vector2({p[0]:.2f}f, {p[1]:.2f}f)'
    L = []
    L.append('// <auto-generated> ZombieLevel02 layout data. Regenerate with level02_gen.py, or hand-edit carefully.')
    L.append('// Ground / Props rows are listed top (y = Height-1) to bottom (y = 0). One char = one 1x1 cell.')
    L.append('#if UNITY_EDITOR')
    L.append('using UnityEngine;')
    L.append('')
    L.append('public static class ZombieLevel02LayoutData')
    L.append('{')
    L.append(f'    public const int Width = {W};')
    L.append(f'    public const int Height = {H};')
    L.append('')
    L.append('    // # blocked grass, B building footprint, R road, y double-yellow (E-W), h white dash (E-W),')
    L.append('    // v white dash (N-S), S sidewalk, C plaza concrete, d dirt/alley/lot, g lawn')
    L.append('    public static readonly string[] Ground =')
    L.append('    {')
    for y in range(H - 1, -1, -1): L.append(f'        "{"".join(G[y])}", // {y}')
    L.append('    };')
    L.append('')
    L.append('    // Blocking: W wreck(2x2 anchor) w wreck-fill N sedan(1x2 anchor) n sedan-fill F chain fence X road barricade')
    L.append('    // D damaged barricade Q sandbags O barrel T dead tree K rock k rock pile U tall rock J waste bin')
    L.append('    // Walkable decor: o puddle * blood , dry grass ; tall dry grass ^ scrub. Digits = zone gate cells.')
    L.append('    public static readonly string[] Props =')
    L.append('    {')
    for y in range(H - 1, -1, -1): L.append(f'        "{"".join(P[y])}", // {y}')
    L.append('    };')
    L.append('')
    L.append('    public struct BuildingDef { public string Name, Sprite; public int X, Y, W, H;')
    L.append('        public BuildingDef(string n, string s, int x, int y, int w, int h) { Name = n; Sprite = s; X = x; Y = y; W = w; H = h; } }')
    L.append('    public struct WaveDef { public string Id; public int X0, Y0, X1, Y1, Count; public string[] Spawns;')
    L.append('        public WaveDef(string id, int x0, int y0, int x1, int y1, int c, string[] s) { Id = id; X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; Count = c; Spawns = s; } }')
    L.append('    public struct GateDef { public string Name; public char Mark; public string WaveId;')
    L.append('        public GateDef(string n, char m, string w) { Name = n; Mark = m; WaveId = w; } }')
    L.append('    public struct SpawnDef { public string Id; public Vector2 Position;')
    L.append('        public SpawnDef(string id, Vector2 p) { Id = id; Position = p; } }')
    L.append('')
    L.append('    public static readonly BuildingDef[] Buildings =')
    L.append('    {')
    for (n, k, x0, y0, w, h) in buildings: L.append(f'        new BuildingDef("{n}", "{k}", {x0}, {y0}, {w}, {h}),')
    L.append('    };')
    L.append('')
    L.append('    public static readonly SpawnDef[] Spawns =')
    L.append('    {')
    for k, p in spawns.items(): L.append(f'        new SpawnDef("{k}", {v2(p)}),')
    L.append('    };')
    L.append('')
    L.append('    public static readonly WaveDef[] Waves =')
    L.append('    {')
    for (i, x0, y0, x1, y1, c, sp) in triggers:
        L.append(f'        new WaveDef("{i}", {x0}, {y0}, {x1}, {y1}, {c}, new[] {{ ' + ', '.join(f'"{s}"' for s in sp) + ' }),')
    L.append('    };')
    L.append('')
    L.append('    public static readonly GateDef[] Gates =')
    L.append('    {')
    for (n, m, w) in gates: L.append(f"        new GateDef(\"{n}\", '{m}', \"{w}\"),")
    L.append('    };')
    L.append('')
    L.append('    public static readonly string[] AmbientSpawns = { ' + ', '.join(f'"{a}"' for a in ambient) + ' };')
    L.append(f'    public static readonly Vector2 StartDoor = {v2(start_door)};')
    L.append(f'    public static readonly Vector2 PlayerSpawn = {v2(player_spawn)};')
    L.append(f'    public static readonly Vector2 ExitDoor = {v2(exit_door)};')
    L.append('}')
    L.append('#endif')
    open(path, 'w', newline='\n').write('\n'.join(L) + '\n')

if __name__ == '__main__':
    emit_cs(sys.argv[1] if len(sys.argv) > 1 else 'ZombieLevel02LayoutData.cs')

