import glob, os
from PIL import Image

SRC = r"D:\GameUnity\IronWasteland\flight\frames\f*.png"
OUT = r"D:\GameUnity\IronWasteland\airplane-flight.gif"

files = sorted(glob.glob(SRC))
frames = [Image.open(f).convert("RGB") for f in files]
print("frames:", len(frames), frames[0].size)

# build one shared 256-colour palette from downscaled copies of every frame,
# so the whole loop shares a palette and does not flicker
tw, th = 160, 90
sheet = Image.new("RGB", (tw, th * len(frames)))
for i, f in enumerate(frames):
    sheet.paste(f.resize((tw, th), Image.LANCZOS), (0, th * i))
pal = sheet.quantize(colors=256, method=Image.MEDIANCUT)

imgs = [f.quantize(palette=pal, dither=Image.FLOYDSTEINBERG) for f in frames]
imgs[0].save(OUT, save_all=True, append_images=imgs[1:],
             duration=150, loop=0, optimize=True, disposal=2)
print("out bytes:", os.path.getsize(OUT))
