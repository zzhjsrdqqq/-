from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

from prepare_assets import _make_blink_frames


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "assets"
SHEET = ASSETS / "expression-sheet-ai.png"
PET_HAPPY = ASSETS / "pet-happy.png"
PET_SLEEPY = ASSETS / "pet-sleepy.png"
PET_WRONGED = ASSETS / "pet-wronged.png"
PET_SURPRISED = ASSETS / "pet-surprised.png"
REVIEW = ASSETS / "expression-frames-review.png"
REVIEW_DARK = ASSETS / "expression-frames-review-dark.png"
TARGET_HEIGHT = 1080

EXPRESSIONS = ("neutral", "happy", "sleepy", "wronged", "surprised")
ROWS = 3
COLS = 5
REPRESENTATIVE_ROWS = {
    "happy": 1,
    "sleepy": 1,
    "wronged": 1,
    "surprised": 1,
}
HEAD_CUT_RATIO = 0.65
HEAD_FADE_START_RATIO = 0.65
ROW_HEAD_SCALE = {
    2: 0.90,
}
ROW_HEAD_Y_OFFSET = {
    2: 25,
}


def green_alpha(pixel: tuple[int, int, int, int]) -> int:
    r, g, b, a = pixel
    if a == 0:
        return 0
    green_strength = g - max(r, b)
    if g > 120 and green_strength > 52:
        return 0
    if g > 95 and green_strength > 34:
        return int(a * 0.22)
    return a


def remove_green(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            alpha = green_alpha((r, g, b, a))
            if alpha == 0:
                pixels[x, y] = (r, g, b, 0)
            elif alpha != a:
                pixels[x, y] = (r, min(g, max(r, b) + 6), b, alpha)
            elif g > max(r, b) + 18:
                pixels[x, y] = (r, min(g, max(r, b) + 8), b, a)
            r, g, b, a = pixels[x, y]
            if a and g > max(r, b) + 8:
                pixels[x, y] = (r, min(g, (r + b) // 2 + 8), b, a)
    return image


def keep_largest_component(image: Image.Image) -> Image.Image:
    alpha = image.getchannel("A")
    pixels = alpha.load()
    width, height = alpha.size
    visited = bytearray(width * height)
    best: list[tuple[int, int]] = []

    for start_y in range(height):
        for start_x in range(width):
            idx = start_y * width + start_x
            if visited[idx] or pixels[start_x, start_y] < 24:
                continue

            queue = [(start_x, start_y)]
            visited[idx] = 1
            component: list[tuple[int, int]] = []

            while queue:
                x, y = queue.pop()
                component.append((x, y))
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if not (0 <= nx < width and 0 <= ny < height):
                        continue
                    nidx = ny * width + nx
                    if visited[nidx] or pixels[nx, ny] < 24:
                        continue
                    visited[nidx] = 1
                    queue.append((nx, ny))

            if len(component) > len(best):
                best = component

    if not best:
        return image

    keep = Image.new("L", image.size, 0)
    keep_pixels = keep.load()
    for x, y in best:
        keep_pixels[x, y] = 255
    keep = keep.filter(ImageFilter.MaxFilter(5))
    original_alpha = image.getchannel("A")
    cleaned_alpha = Image.new("L", image.size, 0)
    cleaned_alpha.paste(original_alpha, mask=keep)
    output = image.copy()
    output.putalpha(cleaned_alpha)
    return output


def component_boxes(image: Image.Image) -> list[tuple[int, tuple[int, int, int, int]]]:
    alpha = image.getchannel("A")
    pixels = alpha.load()
    width, height = alpha.size
    visited = bytearray(width * height)
    components: list[tuple[int, tuple[int, int, int, int]]] = []

    for start_y in range(height):
        for start_x in range(width):
            idx = start_y * width + start_x
            if visited[idx] or pixels[start_x, start_y] < 24:
                continue

            queue = [(start_x, start_y)]
            visited[idx] = 1
            count = 0
            min_x = max_x = start_x
            min_y = max_y = start_y

            while queue:
                x, y = queue.pop()
                count += 1
                min_x = min(min_x, x)
                max_x = max(max_x, x)
                min_y = min(min_y, y)
                max_y = max(max_y, y)
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if not (0 <= nx < width and 0 <= ny < height):
                        continue
                    nidx = ny * width + nx
                    if visited[nidx] or pixels[nx, ny] < 24:
                        continue
                    visited[nidx] = 1
                    queue.append((nx, ny))

            components.append((count, (min_x, min_y, max_x + 1, max_y + 1)))

    return components


def extract_cells(sheet: Image.Image) -> list[tuple[str, int, Image.Image]]:
    clean = remove_green(sheet)
    components = sorted(component_boxes(clean), reverse=True)[: ROWS * COLS]
    if len(components) != ROWS * COLS:
        raise RuntimeError(f"Expected {ROWS * COLS} sprites, found {len(components)}")

    boxes = [box for _count, box in components]
    boxes.sort(key=lambda box: ((box[1] + box[3]) / 2, (box[0] + box[2]) / 2))

    rows = [boxes[index * COLS : (index + 1) * COLS] for index in range(ROWS)]
    cells: list[tuple[str, int, Image.Image]] = []
    for row_index, row_boxes in enumerate(rows):
        row_boxes.sort(key=lambda box: (box[0] + box[2]) / 2)
        for col, box in enumerate(row_boxes):
            expression = EXPRESSIONS[col]
            pad = 10
            x1 = max(0, box[0] - pad)
            y1 = max(0, box[1] - pad)
            x2 = min(clean.width, box[2] + pad)
            y2 = min(clean.height, box[3] + pad)
            cell = keep_largest_component(clean.crop((x1, y1, x2, y2)))
            bbox = cell.getchannel("A").getbbox()
            if bbox is None:
                raise RuntimeError(f"Empty sprite: {expression} frame {row_index}")
            cells.append((expression, row_index, cell.crop(bbox)))
    return cells


def normalize(cells: list[tuple[str, int, Image.Image]]) -> list[tuple[str, int, Image.Image]]:
    resized: list[tuple[str, int, Image.Image]] = []
    max_width = 1
    for expression, row, cell in cells:
        ratio = TARGET_HEIGHT / cell.height
        width = max(1, int(cell.width * ratio))
        sprite = cell.resize((width, TARGET_HEIGHT), Image.Resampling.LANCZOS)
        resized.append((expression, row, sprite))
        max_width = max(max_width, width)

    max_width += 36
    normalized: list[tuple[str, int, Image.Image]] = []
    for expression, row, sprite in resized:
        canvas = Image.new("RGBA", (max_width, TARGET_HEIGHT), (0, 0, 0, 0))
        x = (max_width - sprite.width) // 2
        canvas.alpha_composite(sprite, (x, 0))
        normalized.append((expression, row, canvas))
    return normalized


def _apply_alpha(image: Image.Image, alpha: Image.Image) -> Image.Image:
    output = image.copy()
    output.putalpha(alpha)
    return output


def lock_to_static_body(frames: list[tuple[str, int, Image.Image]]) -> list[tuple[str, int, Image.Image]]:
    frame_map = {(expression, row): frame for expression, row, frame in frames}
    static = frame_map[("neutral", 0)]
    static_bbox = static.getchannel("A").getbbox()
    if static_bbox is None:
        raise RuntimeError("Neutral static frame has no visible alpha")

    head_cut_y = int(static.height * HEAD_CUT_RATIO)
    fade_start_y = int(static.height * HEAD_FADE_START_RATIO)
    static_head_bbox = static.crop((0, 0, static.width, head_cut_y)).getchannel("A").getbbox()
    if static_head_bbox is None:
        raise RuntimeError("Neutral static head has no visible alpha")
    target_head_box = (static_head_bbox[0], 0, static_head_bbox[2], head_cut_y)
    target_head_size = (target_head_box[2] - target_head_box[0], target_head_box[3] - target_head_box[1])

    locked: list[tuple[str, int, Image.Image]] = []
    for expression, row, frame in frames:
        if expression == "neutral" and row == 0:
            locked.append((expression, row, static))
            continue

        source_bbox = frame.getchannel("A").getbbox()
        if source_bbox is None:
            raise RuntimeError(f"Empty expression frame: {expression}-{row + 1}")

        source_head_bbox = frame.crop((0, 0, frame.width, min(head_cut_y, frame.height))).getchannel("A").getbbox()
        if source_head_bbox is None:
            raise RuntimeError(f"Empty expression head: {expression}-{row + 1}")
        source_head_box = (source_head_bbox[0], 0, source_head_bbox[2], min(head_cut_y, frame.height))
        source_head = frame.crop(source_head_box)
        source_head = source_head.resize(target_head_size, Image.Resampling.LANCZOS)

        head_scale = ROW_HEAD_SCALE.get(row, 1.0)
        if head_scale != 1.0:
            scaled_width = max(1, int(source_head.width * head_scale))
            scaled_height = max(1, int(source_head.height * head_scale))
            scaled_head = source_head.resize((scaled_width, scaled_height), Image.Resampling.LANCZOS)
            head_canvas = Image.new("RGBA", source_head.size, (0, 0, 0, 0))
            x = (source_head.width - scaled_width) // 2
            y = ROW_HEAD_Y_OFFSET.get(row, 0)
            head_canvas.alpha_composite(scaled_head, (x, y))
            source_head = head_canvas

        source_alpha = source_head.getchannel("A")
        fade = Image.new("L", source_head.size, 255)
        fade_pixels = fade.load()
        fade_start = max(0, fade_start_y - target_head_box[1])
        fade_end = max(fade_start + 1, target_head_size[1])
        for y in range(source_head.height):
            if y < fade_start:
                multiplier = 255
            else:
                multiplier = max(0, int(255 * (1 - (y - fade_start) / (fade_end - fade_start))))
            if multiplier == 255:
                continue
            for x in range(source_head.width):
                fade_pixels[x, y] = multiplier
        source_alpha = Image.composite(source_alpha, Image.new("L", source_alpha.size, 0), fade)
        source_head = _apply_alpha(source_head, source_alpha)

        output = static.copy()
        output.alpha_composite(source_head, (target_head_box[0], target_head_box[1]))
        locked.append((expression, row, output))

    return locked


def save_review_sheet(frames: list[tuple[str, int, Image.Image]], path: Path, background: tuple[int, int, int]) -> None:
    thumb_height = 245
    padding_x = 28
    padding_y = 20
    gap_x = 34
    gap_y = 54
    label_height = 24
    columns = COLS

    thumbs: list[tuple[str, Image.Image]] = []
    for expression, row, frame in frames:
        bbox = frame.getchannel("A").getbbox()
        if bbox is None:
            continue
        crop = frame.crop(bbox)
        width = max(1, int(crop.width * thumb_height / crop.height))
        thumbs.append((f"{expression}-{row + 1}", crop.resize((width, thumb_height), Image.Resampling.LANCZOS)))

    column_width = max((thumb.width for _label, thumb in thumbs), default=160) + gap_x
    rows = max(1, (len(thumbs) + columns - 1) // columns)
    sheet_width = padding_x * 2 + column_width * columns
    sheet_height = padding_y * 2 + rows * (thumb_height + label_height + gap_y)
    sheet = Image.new("RGB", (sheet_width, sheet_height), background)
    draw = ImageDraw.Draw(sheet)
    label_fill = (38, 34, 31) if sum(background) > 380 else (232, 224, 216)

    for index, (label, thumb) in enumerate(thumbs):
        col = index % columns
        row = index // columns
        x = padding_x + col * column_width
        y = padding_y + row * (thumb_height + label_height + gap_y)
        sheet.paste(thumb, (x, y), thumb)
        draw.text((x, y + thumb_height + 8), label, fill=label_fill)

    sheet.save(path)
    print(f"Wrote {path}")


def main() -> None:
    if not SHEET.exists():
        raise FileNotFoundError(f"Missing expression sheet: {SHEET}")

    sheet = Image.open(SHEET).convert("RGBA")
    frames = lock_to_static_body(normalize(extract_cells(sheet)))
    frame_map: dict[tuple[str, int], Image.Image] = {}

    for expression, row, frame in frames:
        path = ASSETS / f"pet-{expression}-{row + 1}.png"
        frame.save(path)
        frame_map[(expression, row)] = frame
        print(f"Wrote {path}")

    neutral = frame_map[("neutral", 0)]
    neutral.save(ASSETS / "pet.png")
    print(f"Wrote {ASSETS / 'pet.png'}")

    representative_paths = {
        "happy": PET_HAPPY,
        "sleepy": PET_SLEEPY,
        "wronged": PET_WRONGED,
        "surprised": PET_SURPRISED,
    }
    for expression, path in representative_paths.items():
        frame_map[(expression, REPRESENTATIVE_ROWS[expression])].save(path)
        print(f"Wrote {path}")

    blink_25, half_blink, blink_75, blink = _make_blink_frames(neutral)
    blink_25.save(ASSETS / "pet-blink-25.png")
    half_blink.save(ASSETS / "pet-blink-half.png")
    blink_75.save(ASSETS / "pet-blink-75.png")
    blink.save(ASSETS / "pet-blink.png")
    print(f"Wrote {ASSETS / 'pet-blink-25.png'}")
    print(f"Wrote {ASSETS / 'pet-blink-half.png'}")
    print(f"Wrote {ASSETS / 'pet-blink-75.png'}")
    print(f"Wrote {ASSETS / 'pet-blink.png'}")
    save_review_sheet(frames, REVIEW, (238, 235, 230))
    save_review_sheet(frames, REVIEW_DARK, (42, 38, 34))


if __name__ == "__main__":
    main()
