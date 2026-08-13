from pathlib import Path

from PIL import Image


IDLE = Path("Assets/Resources/Characters/BlueKnight/Idle/idle_0.png")
SOURCE = Path("Assets/Resources/Characters/BlueKnight/Attack/DashPunchV3")
DESTINATION = Path("Assets/Resources/Characters/BlueKnight/Attack/DashPunchV8")
ACTIVE_SOURCE_FRAMES = (3, 4, 5)


def is_helmet_blue(pixel):
    red, green, blue, alpha = pixel
    return alpha > 70 and blue > 55 and blue > red * 1.12 and blue > green * 0.75


def largest_blue_component(image):
    pixels = image.load()
    remaining = {
        (x, y)
        for y in range(image.height)
        for x in range(image.width)
        if is_helmet_blue(pixels[x, y])
    }
    components = []
    while remaining:
        seed = remaining.pop()
        queue = [seed]
        component = [seed]
        for x, y in queue:
            for neighbor in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    queue.append(neighbor)
                    component.append(neighbor)
        components.append(component)
    return max(components, key=len)


def component_bbox(component):
    xs = [x for x, _ in component]
    ys = [y for _, y in component]
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def silhouette_mask(image, component, left_padding, right_padding):
    """Follow the helmet shell row by row, including its visor and outline."""
    rows = {}
    for x, y in component:
        rows.setdefault(y, []).append(x)
    top = min(rows)
    bottom = max(rows)
    alpha = image.getchannel("A").load()
    mask = Image.new("L", image.size, 0)
    out = mask.load()
    for y in range(max(0, top - 10), min(image.height, bottom + 7)):
        nearby = []
        for sy in range(max(top, y - 4), min(bottom, y + 4) + 1):
            nearby.extend(rows.get(sy, ()))
        if not nearby:
            continue
        left = max(0, min(nearby) - left_padding)
        right = min(image.width, max(nearby) + right_padding)
        for x in range(left, right):
            if alpha[x, y] > 0:
                out[x, y] = 255
    return mask


def extract_idle_head(idle):
    component = largest_blue_component(idle)
    bbox = component_bbox(component)
    mask = silhouette_mask(idle, component, 12, 30)
    crop_box = mask.getbbox()
    head = idle.crop(crop_box)
    head.putalpha(mask.crop(crop_box))
    anchor = (
        (bbox[0] + bbox[2]) / 2 - crop_box[0],
        bbox[3] - crop_box[1],
    )
    size = (bbox[2] - bbox[0], bbox[3] - bbox[1])
    return head, anchor, size


def replace_head(frame, head, head_anchor):
    component = largest_blue_component(frame)
    bbox = component_bbox(component)
    old_mask = silhouette_mask(frame, component, 22, 90)
    cleared = Image.composite(Image.new("RGBA", frame.size, (0, 0, 0, 0)), frame, old_mask)
    center_x = (bbox[0] + bbox[2]) / 2
    bottom = bbox[3]
    paste_x = round(center_x - head_anchor[0])
    paste_y = round(bottom - head_anchor[1])
    cleared.alpha_composite(head, (paste_x, paste_y))
    return cleared


def main():
    idle = Image.open(IDLE).convert("RGBA")
    head, anchor, target_size = extract_idle_head(idle)
    DESTINATION.mkdir(parents=True, exist_ok=True)
    for index in ACTIVE_SOURCE_FRAMES:
        frame = Image.open(SOURCE / f"dash_{index}.png").convert("RGBA")
        fixed = replace_head(frame, head, anchor)
        fixed.save(DESTINATION / f"dash_{index}.png")
        bbox = component_bbox(largest_blue_component(fixed))
        size = (bbox[2] - bbox[0], bbox[3] - bbox[1])
        print(f"dash_{index}: fixed helmet {size}; idle target {target_size}")


if __name__ == "__main__":
    main()
