"""Normalize generated shooting frames to the existing 64px / 64 PPU body convention."""
from pathlib import Path
from PIL import Image, ImageOps
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/imagegen/BowAndMeta'
ART = ROOT / 'Assets/GameMain/Res/Characters/Survivor/BowCombat'
sheet = Image.open(OUT / 'BowAttackSheetFixed.png').convert('RGBA')
cells = []
for row in range(3):
    line = []
    for col in range(6):
        im = sheet.crop((col*256, round(row*1024/3), (col+1)*256, round((row+1)*1024/3)))
        pixels = np.array(im)
        r, g, b = [pixels[:,:,i].astype(int) for i in range(3)]
        pixels[(r-g > 45) & (b-g > 45)] = 0
        line.append(Image.fromarray(pixels))
    cells.append(line)

reference = cells[0][0].getbbox()
scale = 55 / (reference[3]-reference[1])
frames = {}
for row, view in enumerate(['Down', 'Up', 'Right']):
    frames[view] = []
    order = [0,2,1,3,4,5] if view=='Down' else list(range(6))
    for index, source in enumerate(order):
        im = cells[row][source]
        bounds = im.getbbox()
        alpha = np.array(im)[:,:,3]
        ys,xs = np.where(alpha[max(0,bounds[3]-10):bounds[3]]>0)
        foot_x = (xs.min()+xs.max())/2
        resized = im.resize((round(im.width*scale),round(im.height*scale)), Image.Resampling.NEAREST)
        frame = Image.new('RGBA',(64,64))
        frame.alpha_composite(resized,(round(32-foot_x*scale),round(62-bounds[3]*scale)))
        frames[view].append(frame)
frames['Left'] = [ImageOps.mirror(frame) for frame in frames['Right']]
preview = Image.new('RGBA',(6*128,4*128),(40,45,39,255))
for row,view in enumerate(['Down','Up','Right','Left']):
    folder=ART/view
    folder.mkdir(parents=True,exist_ok=True)
    for index,frame in enumerate(frames[view]):
        frame.save(folder/f'Survivor_Bow{view}_{index:02d}.png')
        preview.alpha_composite(frame.resize((128,128),Image.Resampling.NEAREST),(index*128,row*128))
preview.save(OUT/'BowFramesPreview.png')
print('Exported 24 transparent shooting frames, planted feet, mirrored left direction.')
