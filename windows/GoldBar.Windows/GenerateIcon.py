from PIL import Image, ImageDraw, ImageFont
import os

S = 512
img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# Gold Bar desktop identity: high-contrast black/gold mark that stays readable
# in the title bar, taskbar, desktop shortcut and installer.
d.rounded_rectangle((18, 18, S-18, S-18), radius=112,
                    fill=(7, 9, 13, 255), outline=(247, 200, 65, 255), width=18)
d.rounded_rectangle((49, 49, S-49, S-49), radius=92,
                    fill=(16, 20, 27, 255), outline=(91, 69, 24, 255), width=6)

# Premium assay/ingot tile.
d.rounded_rectangle((104, 100, 408, 390), radius=68,
                    fill=(239, 179, 43, 255), outline=(255, 225, 128, 255), width=11)
d.polygon([(137, 111), (373, 111), (397, 163), (116, 163)],
          fill=(255, 214, 93, 255))

font_paths = [
    r'C:\Windows\Fonts\segoeuib.ttf',
    r'C:\Windows\Fonts\arialbd.ttf',
]
font_path = next((p for p in font_paths if os.path.exists(p)), None)
if font_path:
    f_au = ImageFont.truetype(font_path, 148)
    f_gold = ImageFont.truetype(font_path, 34)
else:
    f_au = ImageFont.load_default()
    f_gold = ImageFont.load_default()

# Large Au mark: deliberately simple for 16/24/32 px Windows icon sizes.
au = 'Au'
b = d.textbbox((0, 0), au, font=f_au)
x = (S - (b[2] - b[0])) // 2
y = 176
d.text((x + 4, y + 5), au, font=f_au, fill=(89, 54, 3, 145))
d.text((x, y), au, font=f_au, fill=(31, 22, 5, 255))

# Small wordmark only for larger icon sizes.
word = 'GOLD BAR'
bw = d.textbbox((0, 0), word, font=f_gold)
d.text(((S - (bw[2] - bw[0])) // 2, 420), word, font=f_gold,
       fill=(247, 211, 112, 255))

out = os.path.join(os.path.dirname(__file__), 'AppIcon.ico')
img.save(out, format='ICO', sizes=[
    (16, 16), (24, 24), (32, 32), (48, 48),
    (64, 64), (96, 96), (128, 128), (256, 256)
])
print(out)
