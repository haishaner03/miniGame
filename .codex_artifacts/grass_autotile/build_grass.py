from pathlib import Path
import re, json, math, shutil, hashlib
import numpy as np
from PIL import Image, ImageDraw, ImageFont

PROJECT = Path('D:/UnityProject/miniGame/MyTestGame')
OUT = PROJECT.parent / '.codex_artifacts/grass_autotile'
MAP = PROJECT / 'Assets/GameMain/Res/Map'
OUT.mkdir(parents=True, exist_ok=True)
source = Image.open(MAP / '01_Grass_4x4.png').convert('RGB')
S = 64
# Clockwise bits: N,E,S,W,NE,SE,SW,NW. Unity y increases upwards.
def normalize(m):
    for d, a, b in ((16,1,2),(32,2,4),(64,4,8),(128,8,1)):
        if not (m & a and m & b): m &= ~d
    return m
MASKS = sorted(set(normalize(m) for m in range(256)))
assert len(MASKS) == 47

def seamless(crop):
    a = np.array(crop, dtype=float)
    # Periodic-plus-smooth decomposition removes seam discontinuities without
    # creating wide blurry bands around every cell.
    h,w=a.shape[:2]
    v=np.zeros_like(a)
    v[0]=a[-1]-a[0]; v[-1]=a[0]-a[-1]
    v[:,0]+=a[:,-1]-a[:,0]; v[:,-1]+=a[:,0]-a[:,-1]
    yy,xx=np.mgrid[:h,:w]
    den=2*np.cos(2*np.pi*xx/w)+2*np.cos(2*np.pi*yy/h)-4
    den[0,0]=1
    correction=np.fft.fft2(v,axes=(0,1))/den[...,None]
    correction[0,0]=0
    a-=np.fft.ifft2(correction,axes=(0,1)).real
    a[:,0]=a[:,-1]=(a[:,0]+a[:,-1])/2
    a[0]=a[-1]=(a[0]+a[-1])/2
    return np.uint8(np.clip(a, 0, 255))

def retouch_grass(seed):
    # Reuse original painted blade pixels as stamps over a periodic soil-green bed.
    # This removes the dark top/bottom band in the original crop.
    src=np.array(source.crop((0,0,64,64)),dtype=float)
    rng=np.random.default_rng(seed)
    a=np.empty((S,S,3),dtype=float)
    a[:]=[42,47,25]
    noise=rng.integers(-4,5,(S,S,1))
    a+=noise
    for _ in range(62):
        sx=int(rng.integers(2,54)); sy=int(rng.integers(3,50))
        patch=src[sy:sy+12,sx:sx+8]
        py,px=np.mgrid[:12,:8]
        # Keep the original stalk shapes and small dark gaps.
        green=(patch[:,:,1]-patch[:,:,0]>1)&(patch[:,:,1]-patch[:,:,2]>8)
        feather=np.minimum.reduce([px+1,8-px,py+1,12-py])/3
        alpha=np.clip(feather,0,1)*green*.92
        ox=int(rng.integers(0,S)); oy=int(rng.integers(0,S))
        for yy in range(12):
            for xx in range(8):
                tx=(ox+xx)%S; ty=(oy+yy)%S; w=alpha[yy,xx]
                a[ty,tx]=a[ty,tx]*(1-w)+patch[yy,xx]*w
    a[:,0]=a[:,-1]=(a[:,0]+a[:,-1])/2
    a[0]=a[-1]=(a[0]+a[-1])/2
    return np.uint8(np.clip(a,0,255))
grass = retouch_grass(318)
dirt = seamless(source.crop((128,0,192,64)))
# Keep the original palette, with a small reduction in contrast for tiled use.
grass = np.uint8(grass.astype(float)*.91 + np.array([7,8,3]))
dirt = np.uint8(dirt.astype(float)*.96 + np.array([2,2,1]))

def shape(m):
    y,x = np.mgrid[:S,:S]
    area = np.zeros((S,S),dtype=bool)
    for left,top,a,b,d in ((True,True,8,1,128),(False,True,2,1,16),
                          (False,False,2,4,32),(True,False,8,4,64)):
        # Reflect local quadrant coordinates. Identical edge profiles guarantee joins.
        u = x if left else S-1-x
        v = y if top else S-1-y
        q = (u < S//2) & (v < S//2)
        jitter_u = np.round(1.5*np.sin((u+1)*math.pi/16)).astype(int)
        jitter_v = np.round(1.5*np.sin((v+1)*math.pi/16)).astype(int)
        side_a,side_b = bool(m&a), bool(m&b)
        if side_a and side_b:
            fill = np.ones_like(q) if m&d else u*u+v*v >= 10*10
        elif side_a:
            fill = v >= 10+jitter_u
        elif side_b:
            fill = u >= 10+jitter_v
        else:
            fill = np.maximum(22-u,0)**2 + np.maximum(22-v,0)**2 <= 12*12
        area[q] = fill[q]
    return area

def render(m):
    mask = shape(m)
    pixels = np.where(mask[...,None],grass,dirt).copy()
    # One-pixel rim inside grass and one-pixel soil shadow; no grid lines.
    near = np.zeros_like(mask)
    for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
        shifted = np.roll(mask,(dy,dx),(0,1))
        # Treat edges as continued. Do not accidentally outline the tile square.
        if dx == 1: shifted[:,0] = mask[:,0]
        if dx == -1: shifted[:,-1] = mask[:,-1]
        if dy == 1: shifted[0] = mask[0]
        if dy == -1: shifted[-1] = mask[-1]
        near |= shifted != mask
    rim = near & mask
    shadow = near & ~mask
    pixels[rim] = np.uint8(np.clip(pixels[rim].astype(float)*1.09 + [4,5,0],0,255))
    pixels[shadow] = np.uint8(pixels[shadow].astype(float)*.72)
    return Image.fromarray(pixels)

tiles = {m:render(m) for m in MASKS}
atlas = Image.new('RGB',(8*S,6*S),tuple(dirt[0,0]))
for i,m in enumerate(MASKS): atlas.paste(tiles[m],((i%8)*S,(i//8)*S))
atlas.save(OUT/'GrassEdges_47.png')
variants=[grass]
for seed in (319,320):
    other=retouch_grass(seed)
    other=np.uint8(other.astype(float)*.91 + np.array([7,8,3]))
    y,x=np.mgrid[:S,:S]
    alpha=np.clip((np.minimum.reduce([x,y,S-1-x,S-1-y])-6)/8,0,1)[...,None]
    variants.append(np.uint8(grass*(1-alpha)+other*alpha))
center_sheet=Image.new('RGB',(3*S,S))
for i,v in enumerate(variants): center_sheet.paste(Image.fromarray(v),(i*S,0))
center_sheet.save(OUT/'GrassCenters_3x1.png')
Image.fromarray(dirt).save(OUT/'SoilSeamless.png')
# Preserve a little of the existing cracked soil, only inside the tile.
cracked = np.array(source.crop((128,128,192,192)))
y,x = np.mgrid[:S,:S]
fade = np.minimum.reduce([x,y,S-1-x,S-1-y])/12
fade = np.clip(fade,0,1)
dark = np.clip((36-np.mean(cracked,axis=2))/26,0,.8)*fade
crack_dirt = np.uint8(dirt.astype(float)*(1-dark[...,None]*.52))
Image.fromarray(crack_dirt).save(OUT/'SoilCrackedSeamless.png')
(OUT/'masks.json').write_text(json.dumps(MASKS),encoding='utf-8')

# Resolve the original scene's actual tile sprites for a faithful before/after preview.
meta_by_guid = {}
for p in (PROJECT/'Assets/GameMain/Res/Map').rglob('*.meta'):
    match = re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M)
    if match: meta_by_guid[match[1]] = Path(str(p)[:-5])
def sprite_image(guid,fid):
    asset = meta_by_guid[guid]
    if asset.suffix == '.asset':
        t=asset.read_text(encoding='utf-8-sig')
        r=re.search(r'm_Sprite: \{fileID: (-?\d+), guid: (\w+)',t)
        if r: return sprite_image(r[2],int(r[1]))
        raw=max(re.findall(r'  _typelessdata: ([0-9a-f]+)',t),key=len,default='')
        width=re.search(r'  m_Width: (\d+)',t)
        height=re.search(r'  m_Height: (\d+)',t)
        if raw and width and height:
            im=Image.frombytes('RGBA',(int(width[1]),int(height[1])),bytes.fromhex(raw))
            return im.transpose(Image.Transpose.FLIP_TOP_BOTTOM).convert('RGB').resize((S,S),Image.Resampling.NEAREST)
        raise ValueError(str(asset))
    t=Path(str(asset)+'.meta').read_text(encoding='utf-8-sig')
    for chunk in re.split(r'    - serializedVersion: 2\n',t.replace('\r\n','\n'))[1:]:
        match=re.search(r'      internalID: (-?\d+)',chunk)
        if match and int(match[1]) == fid:
            rect={key:int(re.search(r'^        '+key+r': (\d+)',chunk,re.M)[1]) for key in ('x','y','width','height')}
            im=Image.open(asset).convert('RGB'); yy=im.height-rect['y']-rect['height']
            return im.crop((rect['x'],yy,rect['x']+rect['width'],yy+rect['height']))
    raise ValueError('sprite not found '+str(asset)+' '+str(fid))

scene=(PROJECT/'Assets/GameMain/Scenes/Level/ZombieLevel01.unity').read_text(encoding='utf-8-sig')
# GroundTilemap component is determined from the GameObject, never from guessed IDs.
objects=re.split(r'(?=--- !u!)',scene)
go=next(b for b in objects if '\n  m_Name: GroundTilemap\n' in b)
go_id=re.search(r'--- !u!1 &(-?\d+)',go)[1]
block=next(b for b in objects if b.startswith('--- !u!1839735485 ') and f'm_GameObject: {{fileID: {go_id}}}' in b)
tile_section=block.split('  m_Tiles:\n')[1].split('  m_AnimatedTiles:')[0]
cells={}
for match in re.finditer(r'- first: \{x: (-?\d+), y: (-?\d+), z: (-?\d+)\}\n    second:\n(.*?)(?=  - first:|\Z)',tile_section,re.S):
    cells[(int(match[1]),int(match[2]))]=int(re.search(r'm_TileIndex: (\d+)',match[4])[1])
ref_block=block.split('  m_TileAssetArray:\n')[1].split('  m_TileSpriteArray:')[0]
refs=[re.search(r'guid: (\w+)',b)[1] if 'guid:' in b else None for b in ref_block.split('  - m_RefCount:')[1:]]
images={i:sprite_image(g,11400000) for i,g in enumerate(refs) if g}
grass_names={'01_Grass_4x4_00_03','01_Grass_4x4_01_03','01_Grass_4x4_03_03'}
soil_names={'01_Grass_4x4_02_03','01_Grass_4x4_02_01'}
grass_ids={i for i,g in enumerate(refs) if g and meta_by_guid[g].stem in grass_names}
soil_ids={i for i,g in enumerate(refs) if g and meta_by_guid[g].stem in soil_names}
offsets=((0,1,1),(1,0,2),(0,-1,4),(-1,0,8),(1,1,16),(1,-1,32),(-1,-1,64),(-1,1,128))
def cell_mask(pos,terrain):
    xx,yy=pos
    return normalize(sum(bit for dx,dy,bit in offsets if (xx+dx,yy+dy) in terrain))
grass_cells={pos for pos,i in cells.items() if i in grass_ids}
W=max(x for x,y in cells)+1; H=max(y for x,y in cells)+1
before=Image.new('RGB',(W*S,H*S)); after=before.copy()
for (cx,cy),i in cells.items():
    loc=(cx*S,(H-1-cy)*S)
    before.paste(images[i],loc)
    if i in grass_ids:
        mask=cell_mask((cx,cy),grass_cells)
        pic=Image.fromarray(variants[((cx*73856093)^(cy*19349663))%3]) if mask==255 else tiles[mask]
    elif i in soil_ids: pic=Image.fromarray(crack_dirt if meta_by_guid[refs[i]].stem.endswith('02_01') else dirt)
    else: pic=images[i]
    after.paste(pic,loc)
before.save(OUT/'Ground_Before.png'); after.save(OUT/'Ground_After.png')

# A compact side-by-side crop and full coverage demo with concave corners and paths.
view=(4*S,9*S,22*S,25*S)
side=Image.new('RGB',(1152,570),(24,27,22))
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
draw=ImageDraw.Draw(side); draw.text((18,10),'修改前：独立纹理块',font=font,fill=(225,225,210)); draw.text((594,10),'修改后：草地边界自动衔接',font=font,fill=(225,225,210))
side.paste(before.crop(view).resize((576,512),Image.Resampling.NEAREST),(0,44))
side.paste(after.crop(view).resize((576,512),Image.Resampling.NEAREST),(576,44))
side.save(OUT/'Grass_BeforeAfter.png')
terrain={(xx,yy) for yy in range(1,11) for xx in range(1,17)}
terrain-={(xx,yy) for yy in range(1,5) for xx in range(1,6)}
terrain-={(xx,yy) for yy in range(7,11) for xx in range(11,17)}
terrain-={(xx,yy) for yy in range(4,7) for xx in range(8,11)}
terrain|={(xx,yy) for yy in range(2,8) for xx in range(18,19)}
terrain|={(xx,yy) for yy in range(8,10) for xx in range(17,20)}
demo=Image.new('RGB',(21*S,12*S))
for yy in range(12):
    for xx in range(21):
        mask=cell_mask((xx,yy),terrain)
        pic=Image.fromarray(variants[((xx*73856093)^(yy*19349663))%3]) if mask==255 else tiles[mask]
        demo.paste(pic if (xx,yy) in terrain else Image.fromarray(dirt),(xx*S,(11-yy)*S))
demo.save(OUT/'Grass_JoinDemo.png')

# Check every valid local 3x3 occupancy pattern. Shared edge classifications must match.
for bits in range(512):
    terrain={(x,y) for y in range(3) for x in range(3) if bits & (1<<(y*3+x))}
    if (1,1) not in terrain: continue
    m=cell_mask((1,1),terrain)
    assert m in MASKS
assert np.array_equal(grass[:,0],grass[:,-1])
assert np.array_equal(dirt[:,0],dirt[:,-1])
assert np.array_equal(grass[0],grass[-1])
assert np.array_equal(dirt[0],dirt[-1])
for vertical in (False,True):
    width,height=(3,4) if vertical else (4,3)
    p=(1,1); q=(1,2) if vertical else (2,1)
    for bits in range(1<<(width*height)):
        terrain={(xx,yy) for yy in range(height) for xx in range(width) if bits&(1<<(yy*width+xx))}
        if p not in terrain or q not in terrain: continue
        a=shape(cell_mask(p,terrain)); b=shape(cell_mask(q,terrain))
        if vertical: assert np.array_equal(a[0],b[-1]),(cell_mask(p,terrain),cell_mask(q,terrain))
        else: assert np.array_equal(a[:,-1],b[:,0]),(cell_mask(p,terrain),cell_mask(q,terrain))
report={'tileSize':64,'pixelsPerUnit':64,'shapes':47,'sceneCells':len(cells),'grassCells':len(grass_cells),
        'soilCells':sum(i in soil_ids for i in cells.values()),'grassNames':sorted(grass_names),'soilNames':sorted(soil_names),
        'sourceHash':hashlib.sha256((MAP/'01_Grass_4x4.png').read_bytes()).hexdigest(),
        'sceneHash':hashlib.sha256((PROJECT/'Assets/GameMain/Scenes/Level/ZombieLevel01.unity').read_bytes()).hexdigest()}
(OUT/'preview_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
