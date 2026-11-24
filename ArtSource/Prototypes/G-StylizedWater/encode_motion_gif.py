"""Encode Unity's 100 real render frames as a five-second, 20 fps GIF.
Run with the bundled DSH Python (Pillow installed):
  python ArtSource/Prototypes/G-StylizedWater/encode_motion_gif.py
No resizing is done; frames are 960x540, two stills are 1920x1080.
"""
from pathlib import Path
from PIL import Image

project = Path(__file__).resolve().parents[3]
folder = project / "Assets" / "Scenes" / "Archive" / "BounceBack" / "Art" / "G-StylizedWater" / "Preview"
frames = sorted((folder / "Motion Frames").glob("Water_*.png"))
if len(frames) != 100:
    raise RuntimeError(f"Expected 100 Unity capture frames, found {len(frames)}")
first = Image.open(frames[0]).convert("RGB")
palette = first.quantize(colors=256)
images = [palette]
for path in frames[1:]:
    with Image.open(path) as frame:
        images.append(frame.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE))
output = folder / "G-StylizedWater_5s.gif"
images[0].save(output, save_all=True, append_images=images[1:], duration=50, loop=0, disposal=2, optimize=False)
print(f"G_GIF_COMPLETE {output} {len(images)} frames 5000 ms")
