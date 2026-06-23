from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "assets"
SOURCE = Path(
    r"C:\Users\12108\Documents\xwechat_files\wxid_ci23vuoydmdb12_cd3d\temp\RWTemp\2026-06"
    r"\9e20f478899dc29eb19741386f9343c8\279ac351e97046e18f89a47fade4756a.jpg"
)
TARGET_HEIGHT = 1080
PET = ASSETS / "pet.png"
RUN_SOURCE = ASSETS / "run-reference-source.jpg"
RUN_RIGHT = tuple(ASSETS / f"pet-run-right-{index}.png" for index in range(1, 4))
RUN_LEFT = tuple(ASSETS / f"pet-run-left-{index}.png" for index in range(1, 4))
RUN_REVIEW = ASSETS / "run-frames-review.png"
RUN_REVIEW_DARK = ASSETS / "run-frames-review-dark.png"

SOURCE_UPPER_SPLIT_RATIO = 0.70
TARGET_UPPER_RATIO = 0.76
SEAM_OVERLAP_RATIO = 0.009


def connected_background_mask(image: Image.Image) -> Image.Image:
    rgb = image.convert("RGB")
    width, height = rgb.size
    pixels = rgb.load()
    corners = [pixels[0, 0], pixels[width - 1, 0], pixels[0, height - 1], pixels[width - 1, height - 1]]
    bg = tuple(sum(channel) // len(corners) for channel in zip(*corners))
    threshold = 44

    visited = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def enqueue(x: int, y: int) -> None:
        index = y * width + x
        if not visited[index]:
            visited[index] = 1
            queue.append((x, y))

    for x in range(width):
        enqueue(x, 0)
        enqueue(x, height - 1)
    for y in range(height):
        enqueue(0, y)
        enqueue(width - 1, y)

    while queue:
        x, y = queue.popleft()
        r, g, b = pixels[x, y]
        distance = abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2])
        if distance > threshold:
            continue
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < width and 0 <= ny < height:
                enqueue(nx, ny)

    mask = Image.new("L", (width, height), 0)
    mask.putdata(visited)
    return mask.point(lambda value: 255 if value else 0)


def subject_alpha(image: Image.Image) -> Image.Image:
    background = connected_background_mask(image)
    background = background.filter(ImageFilter.MaxFilter(5))
    alpha = ImageOps.invert(background)
    return alpha.point(lambda value: 255 if value > 24 else 0)


def component_boxes(alpha: Image.Image) -> list[tuple[int, tuple[int, int, int, int]]]:
    pixels = alpha.load()
    width, height = alpha.size
    visited = bytearray(width * height)
    boxes: list[tuple[int, tuple[int, int, int, int]]] = []

    for start_y in range(height):
        for start_x in range(width):
            idx = start_y * width + start_x
            if visited[idx] or pixels[start_x, start_y] < 16:
                continue
            queue: deque[tuple[int, int]] = deque([(start_x, start_y)])
            visited[idx] = 1
            count = 0
            min_x = max_x = start_x
            min_y = max_y = start_y

            while queue:
                x, y = queue.popleft()
                count += 1
                min_x = min(min_x, x)
                max_x = max(max_x, x)
                min_y = min(min_y, y)
                max_y = max(max_y, y)
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if not (0 <= nx < width and 0 <= ny < height):
                        continue
                    nidx = ny * width + nx
                    if visited[nidx] or pixels[nx, ny] < 16:
                        continue
                    visited[nidx] = 1
                    queue.append((nx, ny))

            if count > 900:
                boxes.append((count, (min_x, min_y, max_x + 1, max_y + 1)))

    return boxes


def keep_largest_component(image: Image.Image) -> Image.Image:
    alpha = image.getchannel("A")
    components = component_boxes(alpha)
    if not components:
        return image
    _count, box = max(components, key=lambda item: item[0])
    mask = Image.new("L", image.size, 0)
    x1, y1, x2, y2 = box
    mask.crop((x1, y1, x2, y2))

    pixels = alpha.load()
    mask_pixels = mask.load()
    width, height = alpha.size
    visited = bytearray(width * height)
    start: tuple[int, int] | None = None
    for y in range(y1, y2):
        for x in range(x1, x2):
            if pixels[x, y] >= 16:
                start = (x, y)
                break
        if start is not None:
            break
    if start is None:
        return image

    queue: deque[tuple[int, int]] = deque([start])
    visited[start[1] * width + start[0]] = 1
    while queue:
        x, y = queue.popleft()
        mask_pixels[x, y] = 255
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if not (0 <= nx < width and 0 <= ny < height):
                continue
            nidx = ny * width + nx
            if visited[nidx] or pixels[nx, ny] < 16:
                continue
            visited[nidx] = 1
            queue.append((nx, ny))

    output = image.copy()
    new_alpha = Image.new("L", image.size, 0)
    new_alpha.paste(alpha, mask=mask.filter(ImageFilter.MaxFilter(3)))
    output.putalpha(new_alpha)
    return output


def normalize(
    sprite: Image.Image,
    canvas_size: tuple[int, int],
    target_content_size: tuple[int, int],
    baseline: int,
) -> Image.Image:
    bbox = sprite.getchannel("A").getbbox()
    if bbox is None:
        raise RuntimeError("Empty run sprite")
    sprite = sprite.crop(bbox)
    target_width, target_height = target_content_size
    sprite = sprite.resize((target_width, target_height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    x = (canvas.width - target_width) // 2
    y = baseline - target_height
    canvas.alpha_composite(sprite, (x, y))
    return canvas


def remove_light_edge_halo(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    pixels = image.load()
    alpha = image.getchannel("A")
    alpha_pixels = alpha.load()
    width, height = image.size
    clear: list[tuple[int, int]] = []

    for y in range(1, height - 1):
        for x in range(1, width - 1):
            current_alpha = alpha_pixels[x, y]
            if current_alpha < 112:
                continue
            edge_pixel = any(alpha_pixels[nx, ny] < 112 for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
            if not edge_pixel:
                continue
            r, g, b, _a = pixels[x, y]
            bright = min(r, g, b) > 212
            nearly_white = min(r, g, b) > 235
            low_saturation = max(r, g, b) - min(r, g, b) < 42
            if nearly_white or (bright and low_saturation and current_alpha < 235):
                clear.append((x, y))

    for x, y in clear:
        r, g, b, _a = pixels[x, y]
        pixels[x, y] = (r, g, b, 0)
    return image


def add_outer_outline(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    alpha = image.getchannel("A").point(lambda value: 255 if value >= 96 else 0)
    expanded = alpha.filter(ImageFilter.MaxFilter(3))
    ring = ImageChops.subtract(expanded, alpha)
    outline = Image.new("RGBA", image.size, (54, 35, 28, 185))
    outline.putalpha(ring.point(lambda value: min(185, value)))
    output = Image.new("RGBA", image.size, (0, 0, 0, 0))
    output.alpha_composite(outline)
    output.alpha_composite(image)
    return output


def soften_bright_head_edges(image: Image.Image) -> Image.Image:
    image = image.convert("RGBA")
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        return image

    pixels = image.load()
    alpha = image.getchannel("A")
    alpha_pixels = alpha.load()
    head_bottom = bbox[1] + int((bbox[3] - bbox[1]) * 0.42)
    width, height = image.size
    recolor: list[tuple[int, int, int, int]] = []

    for y in range(max(1, bbox[1] - 1), min(height - 1, head_bottom)):
        for x in range(max(1, bbox[0] - 1), min(width - 1, bbox[2] + 1)):
            current_alpha = alpha_pixels[x, y]
            if current_alpha < 96:
                continue
            near_clear = any(
                alpha_pixels[nx, ny] < 96
                for nx, ny in (
                    (x + 1, y),
                    (x - 1, y),
                    (x, y + 1),
                    (x, y - 1),
                    (x + 2, y),
                    (x - 2, y),
                    (x, y + 2),
                    (x, y - 2),
                )
                if 0 <= nx < width and 0 <= ny < height
            )
            if not near_clear:
                continue
            r, g, b, a = pixels[x, y]
            low_saturation = max(r, g, b) - min(r, g, b) < 50
            if min(r, g, b) > 205 and low_saturation:
                recolor.append((x, y, a, current_alpha))

    for x, y, a, _current_alpha in recolor:
        pixels[x, y] = (78, 48, 35, min(210, a))
    return image


def rebalance_run_proportions(
    image: Image.Image,
    canvas_size: tuple[int, int],
    pet_bbox: tuple[int, int, int, int],
) -> Image.Image:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise RuntimeError("Empty normalized run sprite")

    crop = image.crop(bbox)
    crop_bbox = crop.getchannel("A").getbbox()
    if crop_bbox is None:
        raise RuntimeError("Empty cropped run sprite")
    crop = crop.crop(crop_bbox)

    pet_width = pet_bbox[2] - pet_bbox[0]
    pet_height = pet_bbox[3] - pet_bbox[1]
    baseline = pet_bbox[3]
    center_x = (pet_bbox[0] + pet_bbox[2]) / 2
    upper_height = int(pet_height * TARGET_UPPER_RATIO)
    overlap = max(3, int(pet_height * SEAM_OVERLAP_RATIO))
    lower_height = pet_height - upper_height + overlap

    split_y = int(crop.height * SOURCE_UPPER_SPLIT_RATIO)
    upper = crop.crop((0, 0, crop.width, split_y))
    lower = crop.crop((0, split_y, crop.width, crop.height))

    upper_scale = upper_height / max(1, upper.height)
    lower_scale = lower_height / max(1, lower.height)
    upper_width = max(1, min(int(pet_width * 1.04), int(upper.width * upper_scale * 0.93)))
    lower_width = max(1, min(int(pet_width * 0.86), int(lower.width * lower_scale * 0.92)))
    upper = upper.resize((upper_width, upper_height), Image.Resampling.LANCZOS)
    lower = lower.resize((lower_width, lower_height), Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    top_y = baseline - pet_height
    upper_x = int(center_x - upper.width / 2)
    lower_x = int(center_x - lower.width / 2)
    canvas.alpha_composite(upper, (upper_x, top_y))
    canvas.alpha_composite(lower, (lower_x, top_y + upper_height - overlap))
    return canvas


def save_review_sheet(frames: list[tuple[str, Image.Image]], path: Path, background: tuple[int, int, int]) -> None:
    thumb_height = 250
    padding_x = 30
    padding_y = 20
    gap_x = 34
    gap_y = 56
    columns = 4
    label_height = 25

    thumbs: list[tuple[str, Image.Image]] = []
    for label, frame in frames:
        bbox = frame.getchannel("A").getbbox()
        if bbox is None:
            continue
        crop = frame.crop(bbox)
        width = max(1, int(crop.width * thumb_height / crop.height))
        thumbs.append((label, crop.resize((width, thumb_height), Image.Resampling.LANCZOS)))

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
    ASSETS.mkdir(parents=True, exist_ok=True)
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    if not PET.exists():
        raise FileNotFoundError(PET)
    RUN_SOURCE.write_bytes(SOURCE.read_bytes())

    pet = Image.open(PET).convert("RGBA")
    pet_bbox = pet.getchannel("A").getbbox()
    if pet_bbox is None:
        raise RuntimeError("Main pet image has no visible content")
    pet_width = pet_bbox[2] - pet_bbox[0]
    pet_height = pet_bbox[3] - pet_bbox[1]
    baseline = pet_bbox[3]
    canvas_size = pet.size

    image = Image.open(SOURCE).convert("RGBA")
    alpha = subject_alpha(image)
    transparent = image.copy()
    transparent.putalpha(alpha)

    boxes = [box for _count, box in sorted(component_boxes(alpha), reverse=True)[:6]]
    boxes.sort(key=lambda box: (box[0] + box[2]) / 2)
    if len(boxes) < 5:
        raise RuntimeError(f"Expected at least 5 sprites, found {len(boxes)}")

    # The supplied reference is arranged left to right: idle, run, run, run, stand, idle.
    run_boxes = boxes[1:4]
    raw_sprites: list[Image.Image] = []
    for box in run_boxes:
        pad = 2
        x1 = max(0, box[0] - pad)
        y1 = max(0, box[1] - pad)
        x2 = min(transparent.width, box[2] + pad)
        y2 = min(transparent.height, box[3] + pad)
        raw_sprites.append(keep_largest_component(transparent.crop((x1, y1, x2, y2))))

    target_content_size = (pet_width, pet_height)
    right_sprites = []
    for sprite in raw_sprites:
        normalized = normalize(sprite, canvas_size, target_content_size, baseline)
        normalized = remove_light_edge_halo(normalized)
        balanced = rebalance_run_proportions(normalized, canvas_size, pet_bbox)
        balanced = soften_bright_head_edges(balanced)
        right_sprites.append(add_outer_outline(remove_light_edge_halo(balanced)))

    for path, sprite in zip(RUN_RIGHT, right_sprites):
        sprite.save(path)
        print(f"Wrote {path}")

    for path, sprite in zip(RUN_LEFT, right_sprites):
        mirrored = ImageOps.mirror(sprite)
        mirrored.save(path)
        print(f"Wrote {path}")

    review_frames = [("pet", pet)]
    review_frames.extend((f"run-right-{index}", sprite) for index, sprite in enumerate(right_sprites, start=1))
    review_frames.extend((f"run-left-{index}", Image.open(path).convert("RGBA")) for index, path in enumerate(RUN_LEFT, start=1))
    save_review_sheet(review_frames, RUN_REVIEW, (238, 235, 230))
    save_review_sheet(review_frames, RUN_REVIEW_DARK, (42, 38, 34))


if __name__ == "__main__":
    main()
