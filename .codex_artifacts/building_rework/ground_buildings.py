from PIL import Image, ImageDraw
from pathlib import Path
root=Path(r'D:\UnityProject\miniGame\MyTestGame\Assets\GameMain\Res\Map\StreetBlock01\Buildings3D')
prev=Path(r'D:\UnityProject\miniGame\.codex_artifacts\building_rework')
files=sorted(root.glob('Building_*_3D.png'))
for p in files:
    im=Image.open(p).convert('RGBA'); w,h=im.size; px=im.load()
    # remove the old detached bottom shadow only
    for y in range(max(0,h-44),h):
        for x in range(w):
            px[x,y]=(0,0,0,0)
    d=ImageDraw.Draw(im)
    # dark foundation that touches the facade bottom
    base_y=h-49
    left=int(w*0.20); right=int(w*0.83)
    d.rectangle((left,base_y,right,h-43),fill=(30,32,31,255))
    d.rectangle((left+8,base_y-3,right-9,base_y),fill=(85,68,50,255))
    d.rectangle((left+4,h-44,right-4,h-41),fill=(15,18,18,225))
    # compact contact shadow, kept close to the base
    d.polygon([(left-11,h-41),(right+12,h-41),(right+3,h-30),(right-20,h-25),(left+16,h-25),(left-18,h-31)],fill=(9,11,14,120))
    d.rectangle((left+12,h-39,right-14,h-35),fill=(12,14,15,150))
    # a few grounded debris pixels
    for x,y in [(left-6,h-36),(left+32,h-32),(right-28,h-33),(right+16,h-37)]:
        d.rectangle((x,y,x+4,y+2),fill=(115,75,45,220))
    im.save(p)
# preview
parts=[]
for p in files:
    im=Image.open(p).convert('RGBA'); pad=20
    bg=Image.new('RGBA',(im.width+pad*2,im.height+pad*2),(26,29,36,255)); bd=ImageDraw.Draw(bg)
    for y in range(pad,pad+im.height,16):
      for x in range(pad,pad+im.width,16): bd.rectangle((x,y,min(x+15,pad+im.width-1),min(y+15,pad+im.height-1)),fill=(58,62,69) if ((x//16+y//16)%2==0) else (45,49,56))
    bg.alpha_composite(im,(pad,pad)); parts.append((p.name,bg))
W=max(p.width for _,p in parts); H=sum(p.height for _,p in parts)+40*len(parts)+20
sheet=Image.new('RGBA',(W,H),(17,19,24,255)); sd=ImageDraw.Draw(sheet); yy=16
for n,p in parts:
    sd.text((10,yy),n,fill=(235,226,193,255)); yy+=25; sheet.alpha_composite(p,(0,yy)); yy+=p.height+15
sheet.convert('RGB').save(prev/'Buildings3D_Grounded_Preview.png')
print('patched',*[p.name for p in files],sep='\n'); print(prev/'Buildings3D_Grounded_Preview.png')
