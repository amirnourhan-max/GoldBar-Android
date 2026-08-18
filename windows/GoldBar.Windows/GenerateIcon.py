from PIL import Image, ImageDraw, ImageFont
import os

S = 512
img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# Premium black badge with a strong gold rim; simple enough to remain legible at 16px.
d.rounded_rectangle((18, 18, S-18, S-18), radius=112, fill=(10, 12, 16, 255), outline=(247, 194, 55, 255), width=20)
d.rounded_rectangle((55, 55, S-55, S-55), radius=88, fill=(17, 21, 27, 255), outline=(92, 69, 18, 255), width=8)

# Gold ingot / assay tile.
d.rounded_rectangle((112, 105, 400, 385), radius=64, fill=(247, 194, 55, 255), outline=(255, 225, 125, 255), width=10)
d.polygon([(148, 113), (360, 113), (390, 165), (122, 165)], fill=(255, 218, 104, 255))

font_paths = [
    r'C:\Windows\Fonts\segoeuib.ttf',
    r'C:\Windows\Fonts\arialbd.ttf',
]
font_path = next((p for p in font_paths if os.path.exists(p)), None)
if font_path:
    f_au = ImageFont.truetype(font_path, 138)
    f_gb = ImageFont.truetype(font_path, 42)
else:
    f_au = ImageFont.load_default()
    f_gb = ImageFont.load_default()

# AU is the primary recognisable mark.
text = 'Au'
bbox = d.textbbox((0, 0), text, font=f_au)
x = (S - (bbox[2]-bbox[0])) // 2
y = 176
# subtle shadow
for ox, oy in [(4, 5), (2, 3)]:
    d.text((x+ox, y+oy), text, font=f_au, fill=(90, 57, 3, 150))
d.text((x, y), text, font=f_au, fill=(38, 26, 3, 255))

# Small GB monogram survives at medium/large icon sizes.
gb = 'GB'
bb = d.textbbox((0,0), gb, font=f_gb)
d.text(((S-(bb[2]-bb[0]))//2, 405), gb, font=f_gb, fill=(247, 211, 112, 255))

out = os.path.join(os.path.dirname(__file__), 'AppIcon.ico')
img.save(out, format='ICO', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(96,96),(128,128),(256,256)])
print(out)
