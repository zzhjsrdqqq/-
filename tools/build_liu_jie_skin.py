from __future__ import annotations

import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SPRITESHEET = ROOT.parent / "hatch-pet-liu-jie" / "final" / "spritesheet.webp"
SKIN_DIR = ROOT / "skins" / "liu-jie"
ASSETS_DIR = SKIN_DIR / "assets"

CELL_W = 192
CELL_H = 208
SCALE = 5


def frame(sheet: Image.Image, row: int, col: int) -> Image.Image:
    left = col * CELL_W
    top = row * CELL_H
    tile = sheet.crop((left, top, left + CELL_W, top + CELL_H)).convert("RGBA")
    return tile.resize((CELL_W * SCALE, CELL_H * SCALE), Image.Resampling.NEAREST)


def save(image: Image.Image, name: str) -> None:
    image.save(ASSETS_DIR / name)


def main() -> None:
    if not SPRITESHEET.exists():
        raise SystemExit(f"spritesheet not found: {SPRITESHEET}")

    ASSETS_DIR.mkdir(parents=True, exist_ok=True)
    sheet = Image.open(SPRITESHEET).convert("RGBA")

    frames = {
        "idle": [frame(sheet, 0, index) for index in range(6)],
        "run_right": [frame(sheet, 1, index) for index in range(8)],
        "run_left": [frame(sheet, 2, index) for index in range(8)],
        "waving": [frame(sheet, 3, index) for index in range(4)],
        "jumping": [frame(sheet, 4, index) for index in range(5)],
        "failed": [frame(sheet, 5, index) for index in range(8)],
        "waiting": [frame(sheet, 6, index) for index in range(6)],
        "running": [frame(sheet, 7, index) for index in range(6)],
        "review": [frame(sheet, 8, index) for index in range(6)],
    }

    save(frames["idle"][0], "pet.png")
    save(frames["idle"][1], "pet-blink-25.png")
    save(frames["idle"][2], "pet-blink-half.png")
    save(frames["idle"][2], "pet-blink-75.png")
    save(frames["idle"][2], "pet-blink.png")

    save(frames["waving"][1], "pet-happy.png")
    save(frames["failed"][1], "pet-sleepy.png")
    save(frames["failed"][0], "pet-wronged.png")
    save(frames["waiting"][0], "pet-surprised.png")

    for out_index, source_index in enumerate((0, 3, 6), start=1):
        save(frames["run_right"][source_index], f"pet-run-right-{out_index}.png")
        save(frames["run_left"][source_index], f"pet-run-left-{out_index}.png")

    expression_map = {
        "neutral": (frames["idle"][0], frames["idle"][1], frames["idle"][3]),
        "happy": (frames["waving"][0], frames["waving"][1], frames["waving"][2]),
        "sleepy": (frames["idle"][2], frames["failed"][1], frames["failed"][3]),
        "wronged": (frames["failed"][0], frames["failed"][2], frames["failed"][6]),
        "surprised": (frames["waiting"][0], frames["waiting"][1], frames["waiting"][3]),
    }
    for expression, images in expression_map.items():
        for index, image in enumerate(images, start=1):
            save(image, f"pet-{expression}-{index}.png")

    manifest = {
        "id": "liu-jie",
        "name": "刘婕",
        "description": "MapleStory 风格长发绿裙抱小熊桌宠皮肤。",
        "version": 1,
        "preview": "pet.png",
        "tags": ["MapleStory", "像素", "长发", "小熊"],
        "accent": "#8bb95a",
    }
    (SKIN_DIR / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    print(f"wrote {SKIN_DIR}")


if __name__ == "__main__":
    main()
