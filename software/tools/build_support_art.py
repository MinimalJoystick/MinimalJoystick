"""Generate the code-native control reference used by simulator image maps."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parents[1]
out=ROOT/'simulator-support/X-Plane'
out.mkdir(parents=True,exist_ok=True)
im=Image.new('RGB',(640,540),(17,23,33));d=ImageDraw.Draw(im)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
title=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',32)
accent=(78,213,180)
d.text((30,18),'MinimalJoystick',font=title,fill='white')
d.text((30,66),'Control reference / software prototype',font=font,fill=(162,177,196))
d.rounded_rectangle((210,110,430,490),radius=30,outline=accent,width=3)
d.ellipse((255,125,385,255),outline=accent,width=3)
d.line((320,190,360,156),fill='white',width=5)
d.rounded_rectangle((245,300,395,320),radius=5,fill=(60,77,98))
d.rectangle((310,286,330,334),fill=accent)
d.ellipse((300,365,340,405),outline=accent,width=3)
for x in (265,320,375):d.rounded_rectangle((x-18,440,x+18,470),radius=6,outline=accent,width=2)
d.text((30,180),'Rudder',font=font,fill='white');d.text((450,300),'Throttle',font=font,fill='white');d.text((450,380),'Gear',font=font,fill='white')
d.text((30,495),'Schematic locations; brake face is BACK, USB is FRONT.',font=font,fill=(162,177,196))
im.save(out/'MinimalJoystick-controls.png')
print('Generated 640 x 540 PNG matching .joy label coordinates.')
