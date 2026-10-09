from pathlib import Path
import json, math, re, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from collections import deque

PROJECT=Path('D:/UnityProject/miniGame/MyTestGame')
HERE=Path(__file__).parent
MAP=PROJECT/'Assets/GameMain/Res/Map'
DEST=MAP/'StreetBlock01/Polished'
DEST.mkdir(exist_ok=True)
BACK=HERE/'backup'; BACK.mkdir(exist_ok=True)
scene=PROJECT/'Assets/GameMain/Scenes/Level/ZombieLevel01.unity'
if not (BACK/scene.name).exists(): shutil.copy2(scene,BACK/scene.name)

def cut(atlas,c,r,name,ppu,polygon=None):
    rgb=np.array(Image.open(MAP/atlas).convert('RGB').crop((c*64,r*64,(c+1)*64,(r+1)*64)))
    possible=rgb.max(axis=2)<43
    visited=np.zeros((64,64),bool); q=deque()
    for i in range(64):
        for x,y in ((i,0),(i,63),(0,i),(63,i)):
            if possible[y,x] and not visited[y,x]: visited[y,x]=True;q.append((x,y))
    while q:
        x,y=q.popleft()
        for xx,yy in ((x+1,y),(x-1,y),(x,y+1),(x,y-1)):
            if 0<=xx<64 and 0<=yy<64 and possible[yy,xx] and not visited[yy,xx]:visited[yy,xx]=True;q.append((xx,yy))
    mask=~visited
    if polygon:
        pm=Image.new('1',(64,64));ImageDraw.Draw(pm).polygon(polygon,fill=1);mask&=np.array(pm)
    seen=np.zeros_like(mask)
    for y in range(64):
        for x in range(64):
            if not mask[y,x] or seen[y,x]:continue
            todo=[(x,y)];part=[];seen[y,x]=True
            while todo:
                xx,yy=todo.pop();part.append((xx,yy))
                for nx,ny in ((xx+1,yy),(xx-1,yy),(xx,yy+1),(xx,yy-1)):
                    if 0<=nx<64 and 0<=ny<64 and mask[ny,nx] and not seen[ny,nx]:seen[ny,nx]=True;todo.append((nx,ny))
            if len(part)<9:
                for xx,yy in part:mask[yy,xx]=False
    rgba=np.dstack((rgb,np.uint8(mask)*255));rgba[~mask,:3]=0
    im=Image.fromarray(rgba); bbox=im.getbbox()
    if bbox:im=im.crop((max(bbox[0]-1,0),max(bbox[1]-1,0),min(bbox[2]+1,64),min(bbox[3]+1,64)))
    im.save(DEST/(name+'.png'))
    return {'name':name,'ppu':ppu,'pixels':im.size,'worldSize':[round(im.width/ppu,2),round(im.height/ppu,2)]}

props=[]
for c,r,name,ppu in [(0,1,'DeadTree',26),(2,1,'TreeStump',55),(3,1,'FallenTrunk',48),
                      (0,0,'RockLarge',56),(1,0,'RockPile',60),(2,0,'RockTall',53),
                      (0,3,'DryGrass',88),(1,3,'DryGrassTall',84),(2,3,'Weeds',85),(3,3,'Scrub',84)]:
    props.append(cut('03_RocksTrees_4x4.png',c,r,name,ppu))
for c,r,name,ppu,poly in [(0,0,'AbandonedSedan',25,[(9,1),(48,1),(50,62),(7,62)]),
                          (1,0,'WreckedCar',27,None),
                          (0,1,'ChainFence',64,None),(1,1,'WoodFence',64,None),
                          (0,3,'RustBarrel',80,None),(1,3,'WasteBin',84,None),
                          (0,2,'RoadBarricade',52,None),(1,2,'DamagedBarricade',52,None),
                          (2,2,'Sandbags',55,None),(2,3,'StartSafetyDoor',48,None),
                          (3,3,'ExitSafetyDoor',48,None)]:
    props.append(cut('04_CarsFencesDoors_4x4.png',c,r,name,ppu,poly))

def periodic_noise(seed,base,amplitude):
    rng=np.random.default_rng(seed);a=np.empty((64,64,3),float);a[:]=base
    a+=rng.integers(-amplitude,amplitude+1,(64,64,1))
    a[:,0]=a[:,-1]=(a[:,0]+a[:,-1])/2;a[0]=a[-1]=(a[0]+a[-1])/2
    return np.uint8(np.clip(a,0,255))

asphalt=[]
for i in range(3):
    a=Image.fromarray(periodic_noise(860+i,[43,47,48],3));d=ImageDraw.Draw(a)
    if i==1:d.line([(12,16),(21,25),(19,31),(31,41)],fill=(28,31,31),width=1);d.line([(20,25),(29,22)],fill=(31,34,34),width=1)
    if i==2:d.polygon([(24,15),(43,12),(49,31),(44,43),(23,46),(18,32)],fill=(46,49,50));d.line([(18,32),(23,46),(44,43)],fill=(36,39,40))
    asphalt.append(a)
asheet=Image.new('RGB',(192,64))
for i,a in enumerate(asphalt):asheet.paste(a,(i*64,0))
asheet.save(DEST/'WornAsphalt_3x1.png')
for i,a in enumerate(asphalt): a.save(DEST/f'WornAsphalt_{i}.png')

concrete=periodic_noise(931,[78,80,75],3)
sheet=Image.new('RGB',(256,256));pavements=[]
for m in range(16):
    im=Image.fromarray(concrete.copy());d=ImageDraw.Draw(im)
    # Narrow cast-concrete curb: restrained highlight, dark gutter and worn chips.
    if m&1:d.rectangle((0,0,63,2),fill=(37,40,40));d.line((0,3,63,3),fill=(122,120,107));d.line((0,4,63,4),fill=(91,93,85))
    if m&2:d.rectangle((61,0,63,63),fill=(37,40,40));d.line((60,0,60,63),fill=(122,120,107));d.line((59,0,59,63),fill=(91,93,85))
    if m&4:d.rectangle((0,61,63,63),fill=(37,40,40));d.line((0,60,63,60),fill=(113,112,101));d.line((0,59,63,59),fill=(88,90,82))
    if m&8:d.rectangle((0,0,2,63),fill=(37,40,40));d.line((3,0,3,63),fill=(113,112,101));d.line((4,0,4,63),fill=(88,90,82))
    # A faint expansion joint, much quieter than the previous small brick pattern.
    d.line([(0,31),(63,31)],fill=(69,72,67))
    if m&1:d.line([(18,3),(23,3)],fill=(77,80,73));d.line([(45,3),(47,3)],fill=(56,59,55))
    pavements.append(im);sheet.paste(im,((m%4)*64,(m//4)*64))
sheet.save(DEST/'ConcreteCurbs_4x4.png')
for m,im in enumerate(pavements): im.save(DEST/f'ConcreteCurb_{m:02d}.png')
lane=Image.new('RGBA',(64,64));d=ImageDraw.Draw(lane);d.rectangle((8,30,40,32),fill=(171,161,119,150));d.rectangle((19,30,20,31),fill=(0,0,0,0));lane.save(DEST/'FadedLaneDash.png')

# Compact preview includes transparent cutouts against the actual ground colours.
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
preview=Image.new('RGB',(1000,650),(32,36,31));d=ImageDraw.Draw(preview)
d.text((18,12),'透明物件：保留原图像素，修整轮廓并按角色比例缩放',font=font,fill=(224,224,210))
for i,p in enumerate(props[:16]):
    x=(i%8)*125;y=50+(i//8)*185
    im=Image.open(DEST/(p['name']+'.png'));im.thumbnail((110,140),Image.Resampling.NEAREST)
    preview.paste(im,(x+(125-im.width)//2,y+140-im.height),im)
    d.text((x+6,y+148),p['name'],font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',11),fill=(220,220,210))
d.text((18,438),'路面预览：低对比混凝土步道 + 细路沿 + 旧柏油',font=font,fill=(224,224,210))
for x in range(15):
    for y in range(2):preview.paste(asphalt[(x+y)%3],(20+x*64,500+y*64))
    preview.paste(pavements[4],(20+x*64,466))
preview.save(HERE/'Assets_Preview.png')
(HERE/'props.json').write_text(json.dumps(props,indent=2),encoding='utf-8')
print(json.dumps(props))
