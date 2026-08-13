from pathlib import Path
from statistics import median
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Art/Characters/BlueKnight/Animations"
OUTPUT = ROOT / "Assets/Resources/Characters/BlueKnight"
CANVAS = 768
TARGET_MEDIAN_HEIGHT = 570

ANIMATIONS = {
    "Idle": ("Idle/idle_strip.png", 6, True),
    "Walk": ("Walk/walk_strip.png", 8, True),
    "Jump/Ground": ("Jump/jump_strip.png", 6, False),
    "Jump/Air": ("DoubleJump/double_jump_strip.png", 5, False),
    "Dodge": ("Dash/dash_strip.png", 6, True),
    "Attack/Basic": ("LightAttack/light_attack_strip.png", 6, True),
    "Attack/Heavy": ("HeavyAttack/heavy_attack_strip.png", 8, True),
    "Attack/Aerial": ("AerialAttack/aerial_attack_strip.png", 6, False),
    "Kick": ("Kick/kick_strip.png", 6, True),
}


for animation, (relative_source, count, grounded) in ANIMATIONS.items():
    strip = Image.open(SOURCE / relative_source).convert("RGBA")
    raw_frames = []
    heights = []
    alpha = strip.getchannel("A").point(lambda value: 255 if value >= 64 else 0)
    active_columns = []
    for x in range(strip.width):
        column = alpha.crop((x, 0, x + 1, strip.height))
        active_columns.append(column.getbbox() is not None)

    segments = []
    start = None
    for x, active in enumerate(active_columns + [False]):
        if active and start is None:
            start = x
        elif not active and start is not None:
            segments.append((start, x))
            start = None

    # With a clean transparent strip each character forms one horizontal island.
    # Fall back to equal cells only if the source unexpectedly has a different count.
    if len(segments) != count:
        segments = [
            (round(index * strip.width / count), round((index + 1) * strip.width / count))
            for index in range(count)
        ]

    for left, right in segments:
        cell = strip.crop((max(0, left - 3), 0, min(strip.width, right + 3), strip.height))
        cell_mask = cell.getchannel("A").point(lambda value: 255 if value >= 64 else 0)
        bbox = cell_mask.getbbox()
        if bbox is None:
            raise RuntimeError(f"Empty frame {animation} {index}")
        frame = cell.crop(bbox)
        raw_frames.append(frame)
        heights.append(frame.height)

    # ImageGen chooses a slightly different drawing scale for each strip.
    # Apply one uniform scale per animation, never a different scale per frame.
    # Grounded dash begins and ends near the idle stance, so its first frame is
    # the reliable scale reference. Median height makes crouched poses inflate.
    reference_height = heights[0] if animation == "Dodge" else median(heights)
    animation_scale = TARGET_MEDIAN_HEIGHT / reference_height
    destination = OUTPUT / animation
    destination.mkdir(parents=True, exist_ok=True)
    prefix = {
        "Idle": "idle", "Walk": "walk", "Jump/Ground": "jump",
        "Jump/Air": "jump", "Dodge": "dodge", "Attack/Basic": "attack",
        "Attack/Heavy": "heavy", "Attack/Aerial": "aerial", "Kick": "kick",
    }[animation]

    for index, frame in enumerate(raw_frames):
        size = (round(frame.width * animation_scale), round(frame.height * animation_scale))
        frame = frame.resize(size, Image.Resampling.LANCZOS)
        # Idle was generated narrower than the other animations. Bake this
        # correction into the pixels so runtime Transform scale never jumps.
        if animation == "Idle":
            frame = frame.resize(
                (round(frame.width * 1.12), frame.height),
                Image.Resampling.LANCZOS)
        # The generated dash recovery frame has an oversized helmet/upper body.
        # Normalize that single source inconsistency without changing its pose.
        if animation == "Dodge" and index == 4:
            frame = frame.resize(
                (round(frame.width * 0.89), round(frame.height * 0.89)),
                Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
        x = (CANVAS - frame.width) // 2
        y = CANVAS - frame.height - 34 if grounded else (CANVAS - frame.height) // 2
        canvas.alpha_composite(frame, (x, y))
        canvas.save(destination / f"{prefix}_{index}.png")

print(f"Sliced Blue Knight animations into {OUTPUT}")
