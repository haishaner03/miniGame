from PIL import Image, ImageDraw
from pathlib import Path
import random, math
OUT=Path(r'D:\UnityProject\miniGame\MyTestGame\Assets\GameMain\Res\Map\StreetBlock01\Buildings')
PRE=Path(r'D:\UnityProject\miniGame\.codex_artifacts\building_rework')
OUT.mkdir(parents=True,exist_ok=True)
rng=random.Random(20260930)

def pxrect(d, box, fill, outline=None, width=1):
    x0,y0,x1,y1=box
    d.rectangle((x0,y0,x1,y1), fill=fill, outline=outline, width=width)

def add_noise(d, box, colors, count, seed):
    rr=random.Random(seed); x0,y0,x1,y1=box
    for _ in range(count):
        x=rr.randrange(x0, max(x0+1,x1-3)); y=rr.randrange(y0, max(y0+1,y1-3))
        w=rr.choice([2,3,4,6]); h=rr.choice([2,3,4])
        d.rectangle((x,y,min(x+w,x1),min(y+h,y1)), fill=rr.choice(colors))

def base_canvas(w,h):
    im=Image.new('RGBA',(w,h),(0,0,0,0)); d=ImageDraw.Draw(im)
    # ground shadow, hard-edged pixels
    d.polygon([(12,h-31),(w-20,h-31),(w-8,h-17),(w-26,h-7),(27,h-7),(7,h-18)], fill=(8,10,14,145))
    # debris shadow pixels
    d.rectangle((18,h-22,36,h-16),fill=(8,10,12,120)); d.rectangle((w-53,h-24,w-28,h-18),fill=(8,10,12,120))
    return im,d

def roof(d,x0,y0,x1,y1,edge=(34,38,44),top=(59,64,70),accent=(108,62,45)):
    # roof overhang and stepped corners
    d.polygon([(x0+10,y0),(x1-12,y0),(x1-12,y0+5),(x1,y0+5),(x1,y1-10),(x1-6,y1-10),(x1-6,y1),(x0+6,y1),(x0+6,y1-6),(x0,y1-6),(x0,y0+10),(x0+10,y0+10)], fill=edge)
    d.rectangle((x0+10,y0+6,x1-13,y1-12),fill=top)
    for x in range(x0+13,x1-14,12): d.rectangle((x,y0+8,x+5,y1-14),fill=accent)
    for y in range(y0+10,y1-14,12): d.rectangle((x0+11,y,x1-14,y+3),fill=(28,31,36))
    d.rectangle((x0+10,y0+5,x1-13,y0+8),fill=(125,75,53))

def window(d,x,y,w=30,h=22,boarded=False,glow=False):
    d.rectangle((x-3,y-3,x+w+3,y+h+3),fill=(20,23,28),outline=(8,10,12))
    if boarded:
        d.rectangle((x,y,x+w,y+h),fill=(84,58,45))
        for i in range(-4,w+8,10): d.rectangle((x+i,y,x+i+4,y+h),fill=(49,38,34))
        d.line((x+2,y+h-2,x+w-2,y+2),fill=(121,76,48),width=3)
    else:
        d.rectangle((x,y,x+w,y+h),fill=(28,59,67) if not glow else (111,74,41))
        d.rectangle((x+3,y+3,x+w-5,y+6),fill=(74,104,104) if not glow else (216,145,63))
        d.rectangle((x+w//2-2,y,x+w//2+2,y+h),fill=(12,21,27))
        d.rectangle((x,y+h//2-2,x+w,y+h//2+2),fill=(12,21,27))
        d.point((x+6,y+7),fill=(147,169,157))

def trim(d, x0,y0,x1,y1, color=(107,89,72)):
    d.rectangle((x0,y0,x1,y0+4),fill=color)
    d.rectangle((x0,y1-4,x1,y1),fill=(41,43,43))
    d.rectangle((x0,y0,x0+4,y1),fill=(54,52,48)); d.rectangle((x1-4,y0,x1,y1),fill=(31,33,35))

def convenience():
    w,h=320,256; im,d=base_canvas(w,h)
    # main footprint
    trim(d,27,37,293,215,(116,92,64)); d.rectangle((33,43,287,208),fill=(71,73,68))
    # roof, red emergency lip
    roof(d,20,17,300,67,edge=(28,31,35),top=(48,55,60),accent=(101,48,39))
    d.rectangle((34,58,286,68),fill=(128,49,40)); d.rectangle((45,64,276,69),fill=(45,35,34))
    # facade panels
    for x in range(42,285,32): d.rectangle((x,74,x+22,195),fill=(86,86,76))
    for x in [44,108,172,236]: window(d,x,88,43,28,boarded=(x in [44,236]),glow=(x==108))
    for x in [58,126,194,250]: window(d,x,137,36,24,boarded=(x in [58,194]))
    # boarded doorway and cracked entrance
    d.rectangle((135,166,191,210),fill=(24,27,28)); d.rectangle((141,171,185,205),fill=(76,53,39))
    d.rectangle((134,163,193,169),fill=(141,110,73)); d.rectangle((146,180,184,185),fill=(49,38,33)); d.rectangle((140,194,183,199),fill=(49,38,33))
    # sign shape without text
    d.rectangle((82,28,239,51),fill=(69,30,29),outline=(150,75,49)); d.rectangle((90,33,231,46),fill=(104,44,35))
    for x in range(96,227,18): d.rectangle((x,36,x+9,42),fill=(166,95,52))
    # dumpsters / debris
    d.rectangle((20,189,44,214),fill=(42,64,59)); d.rectangle((24,184,47,191),fill=(31,43,42)); d.rectangle((273,188,301,216),fill=(42,48,46)); d.rectangle((269,183,298,190),fill=(31,37,37))
    for _ in range(18):
        x=rng.randrange(28,293); y=rng.randrange(205,226); d.rectangle((x,y,x+4,y+3),fill=rng.choice([(136,96,51),(74,74,63),(105,57,45)]))
    add_noise(d,(37,74,286,203),[(62,64,60),(101,82,63),(48,51,49)],44,101)
    return im

def apartment():
    w,h=384,256; im,d=base_canvas(w,h)
    trim(d,29,31,354,216,(95,83,68)); d.rectangle((36,38,347,207),fill=(64,68,69))
    # roof plane
    roof(d,22,15,362,66,edge=(29,33,38),top=(52,60,64),accent=(53,77,78))
    d.rectangle((43,60,340,72),fill=(44,48,49)); d.rectangle((53,66,330,72),fill=(111,61,48))
    # facade blocks
    for y in [83,130,177]:
        d.rectangle((42,y,341,y+4),fill=(31,35,37))
    for y in [91,138,185]:
        for x in [55,112,169,226,283]:
            boarded=((x+y)%3==0)
            window(d,x,y,38,28,boarded=boarded,glow=(x==169 and y==138))
    # central fire escape and entry
    d.rectangle((164,82,216,207),fill=(51,52,51)); d.rectangle((168,88,212,203),fill=(42,44,44))
    d.rectangle((173,90,207,124),fill=(29,62,67)); d.rectangle((174,92,206,97),fill=(101,118,106))
    d.rectangle((180,155,200,205),fill=(64,47,39)); d.rectangle((172,151,208,157),fill=(128,94,61))
    # ladders
    d.rectangle((218,78,223,190),fill=(26,29,30)); d.rectangle((246,79,251,190),fill=(26,29,30))
    for y in range(85,190,13): d.rectangle((218,y,251,y+3),fill=(112,100,81))
    # graffiti-like stripes no letters
    d.rectangle((71,48,161,55),fill=(128,48,42)); d.rectangle((171,48,294,55),fill=(87,54,49))
    # debris and HVAC
    for x,y in [(34,197),(332,198),(309,211),(61,214)]: d.rectangle((x,y,x+18,y+9),fill=(45,48,44)); d.rectangle((x+3,y-4,x+12,y),fill=(99,75,56))
    d.rectangle((281,22,328,37),fill=(69,72,69)); d.rectangle((286,17,323,23),fill=(28,32,35));
    add_noise(d,(38,75,346,205),[(56,61,63),(96,80,62),(45,51,51)],55,202)
    return im

def warehouse():
    w,h=448,320; im,d=base_canvas(w,h)
    trim(d,23,39,424,279,(88,76,61)); d.rectangle((30,47,417,270),fill=(63,67,66))
    roof(d,16,17,432,78,edge=(28,30,33),top=(46,51,52),accent=(91,58,45))
    # broad corrugated facade
    for x in range(39,412,23): d.rectangle((x,84,x+10,260),fill=(74,76,70))
    for y in range(87,260,33): d.rectangle((33,y,414,y+5),fill=(40,44,44))
    # loading doors
    for x in [58,159,260,342]:
        d.rectangle((x,111,x+68,224),fill=(28,32,34),outline=(113,83,58),width=4)
        for y in range(120,216,16): d.rectangle((x+6,y,x+62,y+4),fill=(82,83,77))
        d.rectangle((x+22,219,x+45,255),fill=(47,41,36)); d.rectangle((x+16,216,x+52,222),fill=(129,94,59))
    # office windows
    for x in [45,120,195,270,345]: window(d,x,88,41,20,boarded=(x in [45,270]))
    # sign bars and flood lights
    d.rectangle((120,34,323,60),fill=(81,42,35),outline=(143,76,47)); d.rectangle((130,40,313,51),fill=(126,58,42))
    for x in [106,332]: d.rectangle((x,68,x+9,88),fill=(28,31,33)); d.rectangle((x-9,82,x+18,88),fill=(124,105,70))
    # pallets, barrels, scrap
    for x,y in [(37,243),(115,242),(300,242),(399,242)]:
        d.rectangle((x,y,x+44,y+13),fill=(101,67,43)); d.rectangle((x,y+5,x+44,y+8),fill=(54,44,36))
    for x,y in [(81,235),(83,255),(376,224)]: d.rectangle((x,y,x+23,y+23),fill=(62,67,60)); d.rectangle((x+4,y-4,x+18,y),fill=(33,38,37))
    add_noise(d,(31,79,416,269),[(55,60,58),(101,77,55),(44,48,47)],80,303)
    return im

def safehouse():
    w,h=256,192; im,d=base_canvas(w,h)
    trim(d,25,31,231,158,(101,84,64)); d.rectangle((32,37,224,151),fill=(73,74,68))
    roof(d,19,15,237,55,edge=(27,30,33),top=(49,57,62),accent=(121,55,43))
    d.rectangle((38,50,219,60),fill=(123,48,39)); d.rectangle((47,72,91,102),fill=(27,52,58)); d.rectangle((165,72,210,102),fill=(27,52,58))
    window(d,42,74,41,25,boarded=True); window(d,169,74,36,25,boarded=True)
    d.rectangle((104,81,151,153),fill=(24,28,29)); d.rectangle((109,87,146,151),fill=(63,54,44)); d.rectangle((101,77,154,86),fill=(153,113,60)); d.rectangle((115,104,140,109),fill=(41,34,31))
    # red emergency marker, no lettering
    d.rectangle((103,61,153,70),fill=(106,35,34)); d.rectangle((111,64,145,67),fill=(204,83,54))
    for x,y in [(22,139),(219,142),(57,151),(184,149)]: d.rectangle((x,y,x+17,y+9),fill=(44,54,48)); d.rectangle((x+3,y-4,x+13,y),fill=(107,72,46))
    add_noise(d,(33,61,222,153),[(64,66,60),(99,77,54),(48,52,50)],26,404)
    return im

assets={
 'Building_ConvenienceStore.png':convenience(),
 'Building_Apartment.png':apartment(),
 'Building_Warehouse.png':warehouse(),
 'Building_Safehouse.png':safehouse(),
}
for name,im in assets.items():
    im.save(OUT/name)
# checkerboard preview
thumbs=[]
for name,im in assets.items():
    scale=1
    pad=24
    bg=Image.new('RGBA',(im.width+pad*2,im.height+pad*2),(26,29,36,255)); bd=ImageDraw.Draw(bg)
    # light checker behind transparent sprite
    for y in range(pad,pad+im.height,16):
      for x in range(pad,pad+im.width,16):
        bd.rectangle((x,y,min(x+15,pad+im.width-1),min(y+15,pad+im.height-1)),fill=(58,62,69) if ((x//16+y//16)%2==0) else (45,49,56))
    bg.alpha_composite(im,(pad,pad)); thumbs.append((name,bg))
W=max(t.width for _,t in thumbs); H=sum(t.height for _,t in thumbs)+30*(len(thumbs)+1)
sheet=Image.new('RGBA',(W,H),(17,19,24,255)); sd=ImageDraw.Draw(sheet); y=30
for name,t in thumbs:
    sd.text((10,y-22),name,fill=(232,224,192,255)); sheet.alpha_composite(t,(0,y)); y+=t.height+30
sheet.convert('RGB').save(PRE/'Buildings_Preview.png')
print('wrote',*[str(OUT/n) for n in assets],sep='\n'); print('preview',PRE/'Buildings_Preview.png')
