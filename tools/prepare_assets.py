from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "assets"
SOURCE = ASSETS / "original-cartoon.png"
PET = ASSETS / "pet.png"
PET_BLINK_25 = ASSETS / "pet-blink-25.png"
PET_HALF_BLINK = ASSETS / "pet-blink-half.png"
PET_BLINK_75 = ASSETS / "pet-blink-75.png"
PET_BLINK = ASSETS / "pet-blink.png"
PET_HAPPY = ASSETS / "pet-happy.png"
PET_SLEEPY = ASSETS / "pet-sleepy.png"
PET_WRONGED = ASSETS / "pet-wronged.png"
PET_SURPRISED = ASSETS / "pet-surprised.png"
TARGET_HEIGHT = 1080


def _connected_background_mask(image: Image.Image) -> Image.Image:
    """Build a mask for the off-white background connected to the image edge."""
    rgb = image.convert("RGB")
    width, height = rgb.size
    pixels = rgb.load()
    corners = [pixels[0, 0], pixels[width - 1, 0], pixels[0, height - 1], pixels[width - 1, height - 1]]
    bg = tuple(sum(channel) // len(corners) for channel in zip(*corners))
    threshold = 52

    visited = bytearray(width * height)
    queue: deque[tuple[int, int]] = deque()

    def enqueue(x: int, y: int) -> None:
        idx = y * width + x
        if not visited[idx]:
            visited[idx] = 1
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


def _crop_subject(image: Image.Image, alpha: Image.Image) -> Image.Image:
    subject_mask = ImageOps.invert(alpha).filter(ImageFilter.MaxFilter(9))
    bbox = subject_mask.getbbox()
    if not bbox:
        raise RuntimeError("Could not find the character silhouette.")

    pad = 36
    left = max(0, bbox[0] - pad)
    top = max(0, bbox[1] - pad)
    right = min(image.width, bbox[2] + pad)
    bottom = min(image.height, bbox[3] + pad)
    return image.crop((left, top, right, bottom))


def _remove_background() -> Image.Image:
    image = Image.open(SOURCE).convert("RGBA")
    background = _connected_background_mask(image)

    # Slightly contract and feather the edge so the warm off-white background
    # does not leave a visible halo around hair strands and shoes.
    background = background.filter(ImageFilter.MaxFilter(5))
    subject_alpha = ImageOps.invert(background).filter(ImageFilter.GaussianBlur(1.0))

    transparent = image.copy()
    transparent.putalpha(subject_alpha)
    cropped = _crop_subject(transparent, background)

    target_height = TARGET_HEIGHT
    ratio = target_height / cropped.height
    target_width = max(1, int(cropped.width * ratio))
    resized = cropped.resize((target_width, target_height), Image.Resampling.LANCZOS)

    alpha = resized.getchannel("A")
    sharpened = resized.convert("RGB").filter(ImageFilter.UnsharpMask(radius=1.1, percent=70, threshold=3))
    return Image.merge("RGBA", (*sharpened.split(), alpha))


def _average_color(image: Image.Image, points: tuple[tuple[int, int], ...]) -> tuple[int, int, int, int]:
    pixels = image.load()
    colors = [pixels[x, y] for x, y in points]
    return tuple(sum(channel) // len(colors) for channel in zip(*colors))


def _eye_boxes(width: int, height: int) -> tuple[tuple[int, int, int, int], tuple[int, int, int, int]]:
    return (
        (int(width * 0.285), int(height * 0.395), int(width * 0.455), int(height * 0.448)),
        (int(width * 0.545), int(height * 0.395), int(width * 0.715), int(height * 0.448)),
    )


def _soft_patch(image: Image.Image, box: tuple[int, int, int, int], color: tuple[int, int, int, int]) -> None:
    mask = Image.new("L", image.size, 0)
    mask_draw = ImageDraw.Draw(mask)
    mask_draw.rounded_rectangle(box, radius=(box[3] - box[1]) // 2, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(0.6))

    patch = Image.new("RGBA", image.size, color)
    patch.putalpha(mask)
    image.alpha_composite(patch)


def _draw_curve(
    image: Image.Image,
    points: tuple[tuple[int, int], ...],
    color: tuple[int, int, int, int],
    width: int = 3,
) -> None:
    if len(points) == 3:
        start, control, end = points
        points = tuple(
            (
                int((1 - t) * (1 - t) * start[0] + 2 * (1 - t) * t * control[0] + t * t * end[0]),
                int((1 - t) * (1 - t) * start[1] + 2 * (1 - t) * t * control[1] + t * t * end[1]),
            )
            for t in [step / 22 for step in range(23)]
        )

    scale = 4
    layer = Image.new("RGBA", (image.width * scale, image.height * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    scaled = [(x * scale, y * scale) for x, y in points]
    draw.line(scaled, fill=color, width=width * scale, joint="curve")
    layer = layer.resize(image.size, Image.Resampling.LANCZOS)
    image.alpha_composite(layer)


def _make_blink_frame(pet: Image.Image, amount: float) -> Image.Image:
    width, height = pet.size

    skin = _average_color(
        pet,
        (
            (int(width * 0.30), int(height * 0.48)),
            (int(width * 0.50), int(height * 0.40)),
            (int(width * 0.70), int(height * 0.48)),
        ),
    )
    skin = (skin[0], skin[1], skin[2], 255)
    line = (58, 32, 25, 255)
    frame = pet.copy()

    for box in _eye_boxes(width, height):
        x1, y1, x2, y2 = box
        eye_height = y2 - y1
        eye_width = x2 - x1
        if amount >= 0.96:
            patch_box = (x1 - int(eye_width * 0.07), y1 - 2, x2 + int(eye_width * 0.07), y2 + 4)
            _soft_patch(frame, patch_box, skin)
            line_y = y1 + int(eye_height * 0.55)
            curve_depth = max(2, int(eye_height * 0.055))
            inset = int(eye_width * 0.28)
            _draw_curve(
                frame,
                ((x1 + inset, line_y), ((x1 + x2) // 2, line_y + curve_depth), (x2 - inset, line_y)),
                line,
                width=3,
            )
            continue

        cover_bottom = y1 + int(eye_height * (0.10 + 0.90 * amount))
        patch_box = (x1 - int(eye_width * 0.045), y1 - 1, x2 + int(eye_width * 0.045), cover_bottom + 2)
        _soft_patch(frame, patch_box, skin)

        edge_y = cover_bottom - int(eye_height * 0.03)
        curve_depth = max(1, int(eye_height * (0.018 + 0.038 * amount)))
        inset = int(eye_width * (0.10 + 0.08 * amount))
        edge = (112, 58, 43, int(80 + 80 * amount))
        _draw_curve(
            frame,
            ((x1 + inset, edge_y), ((x1 + x2) // 2, edge_y + curve_depth), (x2 - inset, edge_y)),
            edge,
            width=1 if amount < 0.62 else 2,
        )

    return frame


def _make_blink_frames(pet: Image.Image) -> tuple[Image.Image, Image.Image, Image.Image, Image.Image]:
    return (
        _make_blink_frame(pet, 0.16),
        _make_blink_frame(pet, 0.46),
        _make_blink_frame(pet, 0.74),
        _make_blink_frame(pet, 1.00),
    )


def _skin_color(pet: Image.Image) -> tuple[int, int, int, int]:
    width, height = pet.size
    skin = _average_color(
        pet,
        (
            (int(width * 0.36), int(height * 0.49)),
            (int(width * 0.50), int(height * 0.48)),
            (int(width * 0.64), int(height * 0.49)),
        ),
    )
    return (skin[0], skin[1], skin[2], 255)


def _mouth_box(width: int, height: int) -> tuple[int, int, int, int]:
    center_x = int(width * 0.50)
    center_y = int(height * 0.515)
    half_w = int(width * 0.082)
    half_h = int(height * 0.020)
    return center_x - half_w, center_y - half_h, center_x + half_w, center_y + half_h


def _replace_mouth(frame: Image.Image, skin: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    x1, y1, x2, y2 = _mouth_box(*frame.size)
    _soft_patch(frame, (x1, y1, x2, y2), skin)
    return x1, y1, x2, y2


def _draw_soft_cheeks(frame: Image.Image, color: tuple[int, int, int, int]) -> None:
    width, height = frame.size
    layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer, "RGBA")
    cheek_w = int(width * 0.060)
    cheek_h = int(height * 0.016)
    y = int(height * 0.480)
    for x in (int(width * 0.335), int(width * 0.665)):
        draw.ellipse((x - cheek_w, y - cheek_h, x + cheek_w, y + cheek_h), fill=color)
    layer = layer.filter(ImageFilter.GaussianBlur(4.0))
    frame.alpha_composite(layer)


def _draw_small_oval_mouth(frame: Image.Image, box: tuple[int, int, int, int], line: tuple[int, int, int, int]) -> None:
    x1, y1, x2, y2 = box
    cx = (x1 + x2) // 2
    cy = (y1 + y2) // 2
    rx = max(5, (x2 - x1) // 14)
    ry = max(6, (y2 - y1) // 5)
    layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer, "RGBA")
    draw.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=(69, 38, 33, 170))
    draw.ellipse((cx - rx + 2, cy - ry + 2, cx + rx - 2, cy + ry - 2), outline=(92, 50, 44, 70), width=1)
    frame.alpha_composite(layer)


def _make_expression_frame(pet: Image.Image, expression: str) -> Image.Image:
    width, height = pet.size
    skin = _skin_color(pet)
    line = (58, 32, 25, 255)

    if expression == "sleepy":
        frame = _make_blink_frame(pet, 0.50)
    elif expression == "wronged":
        frame = _make_blink_frame(pet, 0.24)
    else:
        frame = pet.copy()

    mouth = _replace_mouth(frame, skin)
    x1, y1, x2, y2 = mouth
    cx = (x1 + x2) // 2
    cy = (y1 + y2) // 2
    width_m = x2 - x1

    if expression == "happy":
        _draw_soft_cheeks(frame, (255, 143, 175, 34))
        _draw_curve(
            frame,
            (
                (cx - int(width_m * 0.25), cy - 2),
                (cx, cy + int(height * 0.014)),
                (cx + int(width_m * 0.25), cy - 2),
            ),
            line,
            width=2,
        )
    elif expression == "sleepy":
        _draw_curve(
            frame,
            (
                (cx - int(width_m * 0.22), cy),
                (cx, cy + int(height * 0.004)),
                (cx + int(width_m * 0.22), cy),
            ),
            (70, 39, 34, 230),
            width=2,
        )
    elif expression == "wronged":
        _draw_soft_cheeks(frame, (143, 164, 201, 30))
        _draw_curve(
            frame,
            (
                (cx - int(width_m * 0.17), cy + 3),
                (cx, cy - int(height * 0.006)),
                (cx + int(width_m * 0.17), cy + 3),
            ),
            line,
            width=2,
        )
    elif expression == "surprised":
        _draw_small_oval_mouth(frame, mouth, line)
    else:
        _draw_curve(
            frame,
            (
                (cx - int(width_m * 0.22), cy),
                (cx, cy + 1),
                (cx + int(width_m * 0.22), cy),
            ),
            line,
            width=2,
        )

    return frame


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)
    pet = _remove_background()
    blink_25, half_blink, blink_75, blink = _make_blink_frames(pet)
    happy = _make_expression_frame(pet, "happy")
    sleepy = _make_expression_frame(pet, "sleepy")
    wronged = _make_expression_frame(pet, "wronged")
    surprised = _make_expression_frame(pet, "surprised")
    pet.save(PET)
    blink_25.save(PET_BLINK_25)
    half_blink.save(PET_HALF_BLINK)
    blink_75.save(PET_BLINK_75)
    blink.save(PET_BLINK)
    happy.save(PET_HAPPY)
    sleepy.save(PET_SLEEPY)
    wronged.save(PET_WRONGED)
    surprised.save(PET_SURPRISED)
    print(f"Wrote {PET}")
    print(f"Wrote {PET_BLINK_25}")
    print(f"Wrote {PET_HALF_BLINK}")
    print(f"Wrote {PET_BLINK_75}")
    print(f"Wrote {PET_BLINK}")
    print(f"Wrote {PET_HAPPY}")
    print(f"Wrote {PET_SLEEPY}")
    print(f"Wrote {PET_WRONGED}")
    print(f"Wrote {PET_SURPRISED}")


if __name__ == "__main__":
    main()
