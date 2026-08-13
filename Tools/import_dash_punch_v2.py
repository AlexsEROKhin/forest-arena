from pathlib import Path
from PIL import Image

SOURCE = Path("Assets/ArtSource/blue_knight_dash_punch_v3_sheet.png")
DESTINATION = Path("Assets/Resources/Characters/BlueKnight/Attack/DashPunchV3")
FRAME_COUNT = 8
CANVAS_SIZE = 1024
TARGET_BODY_HEIGHT = 554
BODY_BOTTOM = 981
BODY_CENTER_X = CANVAS_SIZE // 2


def body_bbox(image):
    points = []
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            blue = b > 55 and b > r * 1.12 and b > g * 0.75
            red = r > 120 and r > g * 1.35 and r > b * 1.25
            if a > 70 and (blue or red):
                points.append((x, y))
    if not points:
        return None
    return min(x for x, _ in points), min(y for _, y in points), max(x for x, _ in points) + 1, max(y for _, y in points) + 1


def find_character_spans(sheet):
    alpha = sheet.getchannel("A")
    spans = []
    start = None
    for x in range(sheet.width):
        occupied = alpha.crop((x, 0, x + 1, sheet.height)).getbbox() is not None
        if occupied and start is None:
            start = x
        elif not occupied and start is not None:
            spans.append((start, x))
            start = None
    if start is not None:
        spans.append((start, sheet.width))
    if len(spans) != FRAME_COUNT:
        raise RuntimeError(f"Expected {FRAME_COUNT} separated characters, found {len(spans)}: {spans}")
    return spans


def main():
    sheet = Image.open(SOURCE).convert("RGBA")
    spans = find_character_spans(sheet)
    DESTINATION.mkdir(parents=True, exist_ok=True)
    for index, (left, right) in enumerate(spans):
        frame = sheet.crop((left, 0, right, sheet.height))
        visible = frame.getchannel("A").getbbox()
        if visible is None:
            raise RuntimeError(f"Dash frame {index} is empty")
        frame = frame.crop(visible)
        body = body_bbox(frame)
        scale = TARGET_BODY_HEIGHT / (body[3] - body[1])
        frame = frame.resize((round(frame.width * scale), round(frame.height * scale)), Image.Resampling.LANCZOS)
        body = body_bbox(frame)
        body_center_x = (body[0] + body[2]) / 2
        canvas = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
        x = round(BODY_CENTER_X - body_center_x)
        y = round(BODY_BOTTOM - body[3])
        canvas.alpha_composite(frame, (x, y))
        canvas.save(DESTINATION / f"dash_{index}.png")
    print("Imported 8 normalized dash punch frames.")


if __name__ == "__main__":
    main()
