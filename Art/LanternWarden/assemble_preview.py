from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).parent
frames = []
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 22)
for i, path in enumerate(sorted((root / 'frames').glob('*.png'))):
    frame = Image.open(path).convert('RGB')
    label = 'IDLE' if i < 30 else 'MOVE / TURN' if i < 60 else 'CAUGHT' if i < 68 else 'DEATH / DISSOLVE'
    ImageDraw.Draw(frame).text((300, 24), label, font=font, anchor='mt', fill='#bbdcec')
    frames.append(frame)
frames[0].save(root / 'motion_preview.gif', save_all=True, append_images=frames[1:], duration=67, loop=0, disposal=2)
print('Saved motion_preview.gif: 96 Unity-rendered frames, 15 fps')
