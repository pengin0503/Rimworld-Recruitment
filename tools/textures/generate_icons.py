#!/usr/bin/env python3
"""Generates the mod's UI icons. Run from the repository root: python3 tools/textures/generate_icons.py"""
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SIZE = 128
SCALE = 4  # draw large and downsample for smooth edges

FILL = (222, 222, 214, 255)
OUTLINE = (34, 34, 30, 255)
PLUS = (126, 196, 116, 255)


def person(draw, cx, cy, s, fill):
    # Head and shoulders, in the flat style of the game's command icons.
    head_r = 13 * s
    draw.ellipse([cx - head_r, cy - 38 * s - head_r, cx + head_r, cy - 38 * s + head_r], fill=fill, outline=OUTLINE, width=int(3 * s))
    draw.rounded_rectangle([cx - 27 * s, cy - 20 * s, cx + 27 * s, cy + 30 * s], radius=int(16 * s), fill=fill, outline=OUTLINE, width=int(3 * s))


def recruitment_request():
    s = SCALE
    image = Image.new("RGBA", (SIZE * s, SIZE * s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    person(draw, 54 * s, 70 * s, s, FILL)
    # A plus sign: "someone new joins".
    cx, cy, arm, half = 96 * s, 40 * s, 18 * s, 6 * s
    for box in ([cx - arm, cy - half, cx + arm, cy + half], [cx - half, cy - arm, cx + half, cy + arm]):
        draw.rectangle([box[0] - 3 * s, box[1] - 3 * s, box[2] + 3 * s, box[3] + 3 * s], fill=OUTLINE)
    for box in ([cx - arm, cy - half, cx + arm, cy + half], [cx - half, cy - arm, cx + half, cy + arm]):
        draw.rectangle(box, fill=PLUS)
    return image.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    out = ROOT / "Textures" / "UI" / "Commands" / "Recruitment_Request.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    recruitment_request().save(out)
    print(f"wrote {out.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
