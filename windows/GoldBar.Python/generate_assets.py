from PIL import Image, ImageDraw, ImageFont, ImageFilter
from pathlib import Path
import os

ROOT = Path(__file__).resolve().parent


def font(size: int, bold: bool = False):
    names = [
        r"C:\Windows\Fonts\segoeuib.ttf" if bold else r"C:\Windows\Fonts\segoeui.ttf",
        r"C:\Windows\Fonts\arialbd.ttf" if bold else r"C:\Windows\Fonts\arial.ttf",
    ]
    for p in names:
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def icon():
    s = 512
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((18, 18, s-18, s-18), radius=106, fill=(8, 10, 14, 255), outline=(244, 185, 42, 255), width=18)
    glow = Image.new("RGBA", img.size, (0,0,0,0)); gd = ImageDraw.Draw(glow)
    gd.ellipse((80, 70, 432, 420), fill=(247, 179, 31, 72)); glow = glow.filter(ImageFilter.GaussianBlur(36)); img.alpha_composite(glow)
    d = ImageDraw.Draw(img)
    top = [(158,110),(354,110),(402,184),(110,184)]
    body = [(110,184),(402,184),(354,384),(158,384)]
    d.polygon(top, fill=(255,219,99,255), outline=(255,240,176,255))
    d.polygon(body, fill=(238,166,22,255), outline=(255,218,104,255))
    d.line((350,114,391,183,350,376), fill=(255,244,196,220), width=9)
    f = font(135, True)
    box = d.textbbox((0,0), "Au", font=f); x=(s-(box[2]-box[0]))//2
    d.text((x+4,212+5), "Au", font=f, fill=(99,55,1,150)); d.text((x,212), "Au", font=f, fill=(255,239,164,255))
    img.save(ROOT / "AppIcon.ico", format="ICO", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(96,96),(128,128),(256,256)])


def splash():
    w,h=620,320
    img=Image.new("RGB",(w,h),(7,9,13)); d=ImageDraw.Draw(img)
    for i in range(9):
        d.rounded_rectangle((i,i,w-1-i,h-1-i),radius=26,outline=(88+10*i,67+8*i,20,255),width=1)
    glow=Image.new("RGBA",(w,h),(0,0,0,0)); gd=ImageDraw.Draw(glow); gd.ellipse((160,40,460,260),fill=(247,181,35,65)); glow=glow.filter(ImageFilter.GaussianBlur(40)); img=Image.alpha_composite(img.convert("RGBA"),glow);d=ImageDraw.Draw(img)
    f1=font(45,True);f2=font(22,False)
    b=d.textbbox((0,0),"GOLD BAR",font=f1);d.text(((w-(b[2]-b[0]))/2,93),"GOLD BAR",font=f1,fill=(248,201,83,255))
    text="در حال بارگذاری...";b=d.textbbox((0,0),text,font=f2);d.text(((w-(b[2]-b[0]))/2,176),text,font=f2,fill=(232,232,226,255))
    d.rounded_rectangle((150,235,470,243),radius=4,fill=(30,34,41,255));d.rounded_rectangle((150,235,385,243),radius=4,fill=(243,186,45,255))
    (ROOT/"web").mkdir(exist_ok=True); img.convert("RGB").save(ROOT/"web"/"splash.png",quality=95)


if __name__ == "__main__":
    icon(); splash(); print("assets generated")
