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


GOLD = (226, 190, 92, 255)
ACCENT = (110, 170, 214, 255)
FLAG = (196, 92, 80, 255)
WOOD = (168, 128, 84, 255)


def canvas():
    s = SCALE
    image = Image.new("RGBA", (SIZE * s, SIZE * s), (0, 0, 0, 0))
    return image, ImageDraw.Draw(image), s


def done(image):
    return image.resize((SIZE, SIZE), Image.LANCZOS)


def method_trader():
    # A person handing over a coin: asking a trader to spread the word.
    image, draw, s = canvas()
    person(draw, 50 * s, 74 * s, s, FILL)
    for i, cy in enumerate((96, 84, 72)):
        box = [76 * s, (cy - 9) * s, 116 * s, (cy + 9) * s]
        draw.ellipse(box, fill=GOLD, outline=OUTLINE, width=3 * s)
    return done(image)


def method_envoy():
    # A person with a banner on a pole: an envoy sent to a settlement.
    image, draw, s = canvas()
    person(draw, 48 * s, 76 * s, s, FILL)
    draw.rectangle([86 * s, 12 * s, 97 * s, 118 * s], fill=OUTLINE)
    draw.rectangle([89 * s, 15 * s, 94 * s, 115 * s], fill=WOOD)
    draw.polygon([(95 * s, 16 * s), (124 * s, 28 * s), (95 * s, 42 * s)], fill=FLAG, outline=OUTLINE, width=3 * s)
    return done(image)


def method_radio():
    # A mast sending waves: a broadcast from the comms console.
    image, draw, s = canvas()
    draw.polygon([(64 * s, 34 * s), (40 * s, 118 * s), (88 * s, 118 * s)], fill=FILL, outline=OUTLINE, width=3 * s)
    draw.ellipse([56 * s, 22 * s, 72 * s, 38 * s], fill=ACCENT, outline=OUTLINE, width=3 * s)
    for r in (22, 36):
        box = [(64 - r) * s, (30 - r) * s, (64 + r) * s, (30 + r) * s]
        draw.arc(box, start=200, end=250, fill=ACCENT, width=6 * s)
        draw.arc(box, start=290, end=340, fill=ACCENT, width=6 * s)
    return done(image)


def method_orbital():
    # A satellite with two panels: orbital comms.
    image, draw, s = canvas()
    draw.rectangle([10 * s, 48 * s, 44 * s, 80 * s], fill=ACCENT, outline=OUTLINE, width=3 * s)
    draw.rectangle([84 * s, 48 * s, 118 * s, 80 * s], fill=ACCENT, outline=OUTLINE, width=3 * s)
    draw.rectangle([44 * s, 60 * s, 84 * s, 68 * s], fill=OUTLINE)
    draw.rounded_rectangle([50 * s, 44 * s, 78 * s, 84 * s], radius=6 * s, fill=FILL, outline=OUTLINE, width=3 * s)
    draw.line([64 * s, 44 * s, 64 * s, 22 * s], fill=OUTLINE, width=4 * s)
    draw.ellipse([58 * s, 14 * s, 70 * s, 26 * s], fill=GOLD, outline=OUTLINE, width=2 * s)
    return done(image)


ICONS = {
    "UI/Commands/Recruitment_Request": recruitment_request,
    "UI/Icons/Recruitment_Method_Trader": method_trader,
    "UI/Icons/Recruitment_Method_Envoy": method_envoy,
    "UI/Icons/Recruitment_Method_Radio": method_radio,
    "UI/Icons/Recruitment_Method_Orbital": method_orbital,
}


def main():
    for path, make in ICONS.items():
        out = ROOT / "Textures" / (path + ".png")
        out.parent.mkdir(parents=True, exist_ok=True)
        make().save(out)
        print(f"wrote {out.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
