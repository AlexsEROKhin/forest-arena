from pathlib import Path
from PIL import Image
from collections import deque

generated = Path(r"C:/Users/okyer/.codex/generated_images/019f7c0c-02e4-7f63-8976-a642d68ebe48")
resources = Path("Assets/Resources/Characters/BlueKnight/WeaponAttack")
art = Path("Assets/Art/Characters/BlueKnight/Animations/WeaponAttack")

jobs = [
    ("Basic", generated / "exec-151b6a90-4e40-4693-a0c3-2e7b4b408e5f.png", 6, "attack"),
    ("Aerial", generated / "exec-c89ef99f-a92a-4a36-a791-02df5f0e9f51.png", 6, "aerial"),
    ("Dash", generated / "exec-8af254eb-86eb-440d-ba2c-950334debe3a.png", 8, "dash"),
]


def remove_green(image):
    image = image.convert("RGBA")
    cleaned = []
    for r, g, b, _ in image.getdata():
        green = g > r * 1.08 and g > b * 1.08 and g - max(r, b) > 8
        cleaned.append((r, g, b, 0 if green else 255))
    image.putdata(cleaned)
    return image


def extract_components(image, count):
    alpha = image.getchannel("A")
    width, height = image.size
    pixels = alpha.load()
    visited = bytearray(width * height)
    components = []
    for y in range(height):
        for x in range(width):
            flat = y * width + x
            if visited[flat] or pixels[x, y] == 0:
                continue
            queue = deque([(x, y)])
            visited[flat] = 1
            component = []
            while queue:
                cx, cy = queue.popleft()
                component.append((cx, cy))
                for nx, ny in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                    if nx < 0 or nx >= width or ny < 0 or ny >= height:
                        continue
                    index = ny * width + nx
                    if not visited[index] and pixels[nx, ny] != 0:
                        visited[index] = 1
                        queue.append((nx, ny))
            if len(component) > 80:
                components.append(component)
    components = sorted(components, key=len, reverse=True)[:count]
    extracted = []
    for component in sorted(components, key=lambda points: sum(x for x, _ in points) / len(points)):
        min_x = min(x for x, _ in component)
        max_x = max(x for x, _ in component)
        min_y = min(y for _, y in component)
        max_y = max(y for _, y in component)
        crop = image.crop((min_x, min_y, max_x + 1, max_y + 1))
        mask = Image.new("L", crop.size, 0)
        mask_pixels = mask.load()
        for x, y in component:
            mask_pixels[x - min_x, y - min_y] = pixels[x, y]
        crop.putalpha(mask)
        extracted.append(crop)
    return extracted


for folder, source_path, frame_count, prefix in jobs:
    source = Image.open(source_path).convert("RGBA")
    out_dir = resources / folder
    art_dir = art / folder
    out_dir.mkdir(parents=True, exist_ok=True)
    art_dir.mkdir(parents=True, exist_ok=True)
    source.save(art_dir / f"{prefix}_weapon_strip_chroma.png")

    frames = extract_components(remove_green(source), frame_count)
    if len(frames) != frame_count:
        raise RuntimeError(f"Expected {frame_count} connected frames for {folder}, found {len(frames)}")

    max_width = max(frame.width for frame in frames) + 28
    max_height = max(frame.height for frame in frames) + 28
    for index, frame in enumerate(frames):
        canvas = Image.new("RGBA", (max_width, max_height), (0, 0, 0, 0))
        x = (max_width - frame.width) // 2
        y = max_height - frame.height - 14
        canvas.alpha_composite(frame, (x, y))
        canvas.save(out_dir / f"{prefix}_{index}.png")
