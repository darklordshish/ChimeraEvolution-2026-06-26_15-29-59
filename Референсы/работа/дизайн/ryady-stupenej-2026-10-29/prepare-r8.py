from PIL import Image,ImageDraw
from pathlib import Path
p=Path('Референсы/работа/дизайн/ryady-stupenej-2026-10-29'); base=Path('../ChimeraEvolution/Референсы/витрина/человек-лось')
def sheet(name,src,boxes,labels):
 im=Image.open(src).convert('RGB'); rows=(len(boxes)+1)//2; h=820//rows
 out=Image.new('RGB',(1200,900),'white'); d=ImageDraw.Draw(out)
 for i,(box,label) in enumerate(zip(boxes,labels)):
  c=im.crop(box); s=min(560/c.width,(h-65)/c.height); c=c.resize((round(c.width*s),round(c.height*s)),Image.Resampling.LANCZOS)
  x=(i%2)*600; y=(i//2)*h
  out.paste(c,(x+(600-c.width)//2,y+65+(h-65-c.height)//2)); d.text((x+12,y+15),label,fill='black',font_size=17)
 out.save(p/name)
sheet('r8-input-pose.png',base/'тело.png',[(440,375,770,662),(780,375,1145,662)],['EDIT TARGET: STAGE 1 - POSE ONLY','EDIT TARGET: STAGE 2 - POSE ONLY'])
sheet('r8-input-organs.png',base/'ступени.png',[(480,38,655,140),(820,30,1076,147),(545,650,667,699),(916,650,1032,699)],['INSERT: STAGE 1 HEAD / EARS / ANTLERS','INSERT: STAGE 2 HEAD / EARS / ANTLERS','INSERT: STAGE 1 CLOVEN HOOVES','INSERT: STAGE 2 CLOVEN HOOVES'])
sheet('r8-input-upper.png',p/'chelovek-los-r5.png',[(420,45,770,391),(780,40,1130,391)],['SURFACE: STAGE 1 - MOOSE UPPER / DRY LIMBS','SURFACE: STAGE 2 - MOOSE UPPER / DRY LIMBS'])
sheet('r8-input-balance.png',p/'chelovek-los-r6-proba1.png',[(430,430,580,950),(1140,430,1300,950)],['CHECK ONLY: PELVIS OVER HOOVES','CHECK ONLY: PELVIS OVER HOOVES'])
