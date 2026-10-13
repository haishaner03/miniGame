from pathlib import Path
from PIL import Image, ImageDraw
import random

root = Path('D:/UnityProject/miniGame')
out = root / 'MyTestGame/Assets/GameMain/Res/UI/Art/BattleHUD'
out.mkdir(parents=True, exist_ok=True)
names = ['Icon_Health','Icon_Melee','Icon_Dash','Icon_Time','Icon_Kills','Icon_Room',
         'Icon_Pause','Icon_Retry','Upgrade_Damage','Upgrade_Range','Upgrade_AttackSpeed','Upgrade_MoveSpeed',
         'Upgrade_MaxHealth','Upgrade_DashCooldown','Upgrade_DashDistance','Upgrade_LifeSteal','Upgrade_Frost','Upgrade_Fire',
         'Upgrade_Frostfire','Icon_Bow','Icon_IronBar','Icon_Hammer','Upgrade_FrostShield','Upgrade_Burst']
sheet = Image.open(root / 'output/imagegen/battle-hud-assets-sheet.png').convert('RGBA')
w,h = sheet.size
for i,name in enumerate(names):
    tile = sheet.crop((i%6*w//6, i//6*h//4, (i%6+1)*w//6, (i//6+1)*h//4))
    pixels = []
    for r,g,b,a in tile.getdata():
        # Remove the solid magenta field, including slight generation variations.
        if r > g+65 and b > g+65 and r > 140 and b > 130:
            pixels.append((0,0,0,0))
        else:
            pixels.append((r,g,b,255))
    tile.putdata(pixels)
    # Discard tiny fragments from icons that brush a generated cell boundary.
    alpha=tile.getchannel('A'); seen=set(); components=[]
    for y in range(tile.height):
        for x in range(tile.width):
            if (x,y) in seen or not alpha.getpixel((x,y)): continue
            stack=[(x,y)];seen.add((x,y));component=[]
            while stack:
                px,py=stack.pop();component.append((px,py))
                for nx,ny in [(px-1,py),(px+1,py),(px,py-1),(px,py+1)]:
                    if 0<=nx<tile.width and 0<=ny<tile.height and (nx,ny) not in seen and alpha.getpixel((nx,ny)):
                        seen.add((nx,ny));stack.append((nx,ny))
            components.append(component)
    minimum=max(len(c) for c in components)*.03
    for c in components:
        if len(c)<minimum:
            for xy in c: tile.putpixel(xy,(0,0,0,0))
    box = tile.getbbox()
    if box is None: raise ValueError(name)
    tile = tile.crop(box)
    scale = min(88/tile.width,88/tile.height)
    tile = tile.resize((round(tile.width*scale),round(tile.height*scale)),Image.Resampling.NEAREST)
    icon = Image.new('RGBA',(96,96))
    icon.alpha_composite(tile,((96-tile.width)//2,(96-tile.height)//2))
    icon.save(out / (name+'.png'))

# Reconstruct the reference's stepped brass frame as a clean nine-slice bitmap.
def frame(name, size, opaque):
    im = Image.new('RGBA',(size,size))
    d = ImageDraw.Draw(im)
    d.polygon([(8,0),(size-9,0),(size-1,8),(size-1,size-9),(size-9,size-1),(8,size-1),(0,size-9),(0,8)],fill=(8,13,13,245))
    d.polygon([(8,2),(size-9,2),(size-3,8),(size-3,size-9),(size-9,size-3),(8,size-3),(2,size-9),(2,8)],outline=(122,104,65,255),width=2)
    d.line((9,4,size-10,4),fill=(201,175,112,255),width=1)
    d.line((4,10,4,size-11),fill=(176,152,94,255),width=1)
    d.line((10,size-5,size-11,size-5),fill=(65,58,40,255),width=2)
    d.rectangle((7,7,size-8,size-8),fill=(14,22,21,opaque),outline=(40,48,40,255),width=1)
    rng = random.Random(351)
    for _ in range(size*2):
        x,y=rng.randrange(11,size-11),rng.randrange(11,size-11)
        d.point((x,y), fill=(23,30,27,opaque))
    for x,y in [(7,7),(size-10,7),(7,size-10),(size-10,size-10)]:
        d.rectangle((x,y,x+2,y+2),fill=(161,136,83,255))
        d.point((x,y),fill=(235,211,139,255))
    im.save(out/(name+'.png'))
frame('Panel_Brass',128,224)
frame('Slot_Brass',64,240)
ring=Image.new('RGBA',(96,96));d=ImageDraw.Draw(ring)
d.ellipse((3,3,92,92),outline=(241,222,180,255),width=4)
d.ellipse((8,8,87,87),outline=(111,94,61,255),width=1)
ring.save(out/'Cooldown_Ring.png')
fill=Image.new('RGBA',(16,16),(255,255,255,255));d=ImageDraw.Draw(fill)
d.line((0,1,15,1), fill=(255,255,255,255),width=2)
d.line((0,14,15,14), fill=(175,175,175,255),width=2)
fill.save(out/'Bar_Fill.png')

# Portrait uses the approved reference, cropped tightly inside its old frame.
ref=Image.open(root/'output/imagegen/hud-redesign-v2.png').convert('RGBA')
portrait=ref.crop((76,48,166,148)).resize((72,80),Image.Resampling.NEAREST)
portrait.save(out/'Portrait_Survivor.png')
preview=Image.new('RGBA',(768,600),(35,39,39,255))
for i,name in enumerate(names):
    tile=Image.open(out/(name+'.png'));preview.alpha_composite(tile,(i%8*96,i//8*112))
for i,name in enumerate(['Panel_Brass','Slot_Brass','Cooldown_Ring','Portrait_Survivor']):
    tile=Image.open(out/(name+'.png'));preview.alpha_composite(tile,(20+i*180,410))
preview.save(root/'output/imagegen/battle-hud-cutouts-preview.png')
print(f'Prepared {len(names)+5} RGBA PNGs. Source sheet: {w}x{h}')
