from PIL import Image, ImageDraw
from pathlib import Path
import random
OUT=Path(r'D:\UnityProject\miniGame\MyTestGame\Assets\GameMain\Res\Map\StreetBlock01\Buildings3D')
PRE=Path(r'D:\UnityProject\miniGame\.codex_artifacts\building_rework')
OUT.mkdir(parents=True,exist_ok=True)

def poly(d,pts,fill,outline=None):
    d.polygon(pts,fill=fill)
    if outline: d.line(pts+[pts[0]],fill=outline,width=3)

def line(d,pts,fill,width=2): d.line(pts,fill=fill,width=width)

def shadow(im,d,w,h):
    d.polygon([(18,h-42),(w-18,h-38),(w-7,h-23),(w-25,h-10),(26,h-12),(8,h-25)],fill=(5,7,11,165))
    d.rectangle((31,h-20,69,h-14),fill=(5,7,11,120)); d.rectangle((w-72,h-22,w-30,h-16),fill=(5,7,11,120))

def roof_tiles(d,poly_pts,base,light,dark,seed=1,step=16):
    # roof poly plus striping within broad top plane approximation
    poly(d,poly_pts,base,(20,23,27))
    x0=min(p[0] for p in poly_pts); x1=max(p[0] for p in poly_pts); y0=min(p[1] for p in poly_pts); y1=max(p[1] for p in poly_pts)
    rr=random.Random(seed)
    for y in range(y0+10,y1-2,step):
        line(d,[(x0+8,y),(x1-8,y+1)],dark,3)
        for x in range(x0+12,x1-8,step):
            line(d,[(x,y-4),(x+7,y+6)],light,3)

def front_window(d,x,y,w,h,boarded=False,glow=False):
    d.rectangle((x-3,y-3,x+w+3,y+h+3),fill=(17,21,24),outline=(8,9,11),width=2)
    if boarded:
        d.rectangle((x,y,x+w,y+h),fill=(77,52,38))
        for xx in range(x-4,x+w+4,12): d.rectangle((xx,y,xx+4,y+h),fill=(47,35,31))
        line(d,[(x+3,y+h-2),(x+w-2,y+2)],(144,88,48),3)
    else:
        d.rectangle((x,y,x+w,y+h),fill=(98,73,44) if glow else (28,59,65))
        d.rectangle((x+3,y+3,x+w-5,y+6),fill=(213,142,59) if glow else (73,111,112))
        d.rectangle((x+w//2-2,y,x+w//2+2,y+h),fill=(13,21,26))
        d.rectangle((x,y+h//2-2,x+w,y+h//2+2),fill=(13,21,26))

def make(kind,w,h,seed):
    im=Image.new('RGBA',(w,h),(0,0,0,0)); d=ImageDraw.Draw(im); shadow(im,d,w,h)
    rr=random.Random(seed)
    # common 3/4 volume
    ox=24; oy=35; rx=w-28; by=h-47; depth=max(28,int(h*0.18))
    roof_pts=[(ox+6,oy+15),(rx-45,oy),(rx,oy+depth),(ox+60,oy+depth+17)]
    front_pts=[(ox+60,oy+depth+17),(rx,oy+depth),(rx,by-15),(ox+60,by)]
    side_pts=[(ox+6,oy+15),(ox+60,oy+depth+17),(ox+60,by),(ox+6,by-20)]
    poly(d,side_pts,(60,63,59),(24,27,29)); poly(d,front_pts,(74,75,69),(24,27,29))
    # side panel bands for depth
    for y in range(oy+42,by-12,26): line(d,[(ox+12,y),(ox+57,y+19)],(86,78,61),3)
    # roof with stepped eave
    roof_tiles(d,roof_pts,(50,57,61),(113,68,47),(29,34,38),seed,14)
    line(d,[(ox+9,oy+20),(ox+62,oy+depth+23),(rx+2,oy+depth+6)],(126,58,43),6)
    # facade design
    fw=rx-(ox+70); ybase=oy+depth+31
    if kind=='convenience':
        d.rectangle((ox+78,ybase-23,rx-7,ybase-13),fill=(118,48,38)); d.rectangle((ox+100,ybase-20,rx-30,ybase-16),fill=(197,93,50))
        front_window(d,ox+82,ybase+3,42,31,boarded=True)
        front_window(d,ox+135,ybase+3,42,31,boarded=False,glow=True)
        front_window(d,ox+188,ybase+3,42,31,boarded=True)
        d.rectangle((ox+127,ybase+40,ox+170,by-9),fill=(38,33,30),outline=(137,97,59),width=3); d.rectangle((ox+122,ybase+35,ox+175,ybase+42),fill=(155,107,60))
        d.rectangle((ox+71,by-14,ox+101,by-7),fill=(43,64,58)); d.rectangle((rx-36,by-15,rx-9,by-8),fill=(45,53,49))
    elif kind=='apartment':
        for y in [ybase+3,ybase+49,ybase+95]:
            for x in [ox+78,ox+130,ox+182,ox+234]:
                if x+33<rx-3: front_window(d,x,y,33,24,boarded=((x+y)%3==0),glow=(x==ox+130 and y==ybase+49))
        d.rectangle((ox+127,ybase+2,ox+177,by-7),fill=(45,47,45),outline=(25,27,28),width=3); d.rectangle((ox+134,ybase+15,ox+170,ybase+43),fill=(29,59,64)); d.rectangle((ox+134,ybase+65,ox+170,ybase+92),fill=(35,47,49))
        d.rectangle((ox+184,ybase-8,ox+190,by-4),fill=(27,29,29)); d.rectangle((ox+211,ybase-8,ox+217,by-4),fill=(27,29,29))
        for y in range(ybase+1,by-6,13): d.rectangle((ox+184,y,ox+217,y+3),fill=(115,96,69))
        d.rectangle((ox+75,by-13,ox+103,by-6),fill=(50,51,47)); d.rectangle((rx-37,by-13,rx-8,by-6),fill=(51,53,48))
    elif kind=='warehouse':
        for x in [ox+77,ox+151,ox+225]:
            d.rectangle((x,ybase+5,x+55,by-14),fill=(31,34,35),outline=(118,86,57),width=4)
            for y in range(ybase+16,by-16,15): d.rectangle((x+6,y,x+49,y+4),fill=(83,84,77))
            d.rectangle((x+19,by-18,x+38,by-8),fill=(49,42,37))
        d.rectangle((ox+112,ybase-21,rx-20,ybase-11),fill=(110,48,39)); d.rectangle((ox+133,ybase-18,rx-42,ybase-15),fill=(177,79,46))
        for x in [ox+70,rx-32]: d.rectangle((x,by-11,x+20,by-5),fill=(106,70,43))
    else: # safehouse
        front_window(d,ox+83,ybase+3,43,27,boarded=True); front_window(d,ox+175,ybase+3,38,27,boarded=True)
        d.rectangle((ox+130,ybase+2,ox+171,by-7),fill=(38,34,31),outline=(135,98,58),width=3); d.rectangle((ox+125,ybase-6,ox+176,ybase+3),fill=(157,110,59))
        d.rectangle((ox+124,ybase-18,ox+176,ybase-9),fill=(107,34,34)); d.rectangle((ox+134,ybase-16,ox+166,ybase-13),fill=(210,87,52))
    # side windows and AC units
    for i in range(2):
        yy=oy+61+i*44
        d.polygon([(ox+16,yy),(ox+49,yy+12),(ox+49,yy+32),(ox+16,yy+20)],fill=(26,55,61),outline=(16,20,22))
        line(d,[(ox+32,yy+5),(ox+32,yy+26)],(10,19,23),3)
    d.rectangle((ox+18,by-22,ox+47,by-10),fill=(49,54,50)); d.rectangle((ox+23,by-28,ox+41,by-21),fill=(29,33,34))
    # scattered pixels
    for _ in range(max(20,w//8)):
        x=rr.randrange(max(10,ox-4),min(w-10,rx+5)); y=rr.randrange(max(10,oy+35),min(h-12,by+6));
        d.rectangle((x,y,x+rr.choice([2,3,5]),y+rr.choice([2,3])),fill=rr.choice([(108,81,51),(54,59,55),(121,55,43)]))
    return im

assets={
 'Building_ConvenienceStore_3D.png':make('convenience',320,256,101),
 'Building_Apartment_3D.png':make('apartment',384,256,202),
 'Building_Warehouse_3D.png':make('warehouse',448,320,303),
 'Building_Safehouse_3D.png':make('safehouse',256,192,404),
}
for n,im in assets.items(): im.save(OUT/n)
# preview on checker
parts=[]
for n,im in assets.items():
    pad=20; bg=Image.new('RGBA',(im.width+pad*2,im.height+pad*2),(26,29,36,255)); bd=ImageDraw.Draw(bg)
    for y in range(pad,pad+im.height,16):
      for x in range(pad,pad+im.width,16): bd.rectangle((x,y,min(x+15,pad+im.width-1),min(y+15,pad+im.height-1)),fill=(58,62,69) if ((x//16+y//16)%2==0) else (45,49,56))
    bg.alpha_composite(im,(pad,pad)); parts.append((n,bg))
W=max(p.width for _,p in parts); H=sum(p.height for _,p in parts)+40*len(parts)+20
sheet=Image.new('RGBA',(W,H),(17,19,24,255)); sd=ImageDraw.Draw(sheet); y=18
for n,p in parts:
    sd.text((10,y),n,fill=(235,226,193,255)); y+=25; sheet.alpha_composite(p,(0,y)); y+=p.height+15
sheet.convert('RGB').save(PRE/'Buildings3D_Preview.png')
print('generated',*[str(OUT/n) for n in assets],sep='\n'); print(PRE/'Buildings3D_Preview.png')
