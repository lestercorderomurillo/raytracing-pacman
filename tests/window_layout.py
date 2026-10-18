"""Check the actual Linux/X11 window, including Vulkan presentation, after a Release build.

Requires Python with Pillow, xdotool, and ImageMagick's import command.
Run from any directory: python3 tests/window_layout.py
"""

import os
from pathlib import Path
import shutil
import subprocess
import time

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "artifacts" / "window-layout"
SIZES = [(1280, 720), (640, 480), (800, 800), (1280, 600), (720, 1080), (1920, 1080), (1280, 720)]


def verify_capture(path, expected_size):
    with Image.open(path) as image:
        assert image.size == expected_size, f"Unexpected client size: {image.size}"
        width, height = image.size

        # The first maze's saturated purple walls locate the board independently
        # of engine matrices, render-target readbacks, and the HUD.
        pixels = image.convert("RGB")
        points = [
            (column, row)
            for row in range(height)
            for column in range(width)
            if (color := pixels.getpixel((column, row)))[2] > 110
            and color[2] > color[0] * 1.5
            and color[2] > color[1] * 2
        ]
        assert points, "The presented game frame is empty."

        left = min(point[0] for point in points)
        right = max(point[0] for point in points)
        top = min(point[1] for point in points)
        bottom = max(point[1] for point in points)

        assert abs((left + right + 1) / 2 - width / 2) < 8, "Maze is not horizontally centered."
        assert abs((top + bottom + 1) / 2 - height / 2) < 8, "Maze is not vertically centered."
        assert 0.85 < (right - left) / (bottom - top) < 1.05, "Maze is stretched."
        assert left > 0 and top > 0 and right < width - 1 and bottom < height - 1, "Maze is clipped."
        print(f"PASS: {width}x{height}, centered and proportional; {path.name}", flush=True)


def main():
    assert os.environ.get("DISPLAY"), "Run in a graphical X11/XWayland desktop session."

    for command in ["xdotool", "import"]:
        assert shutil.which(command), f"Install {command} before running this check."

    executable = ROOT / "bin/Release/net8.0/raytracing-pacman"
    assert executable.exists(), "Run dotnet build -c Release first."
    OUTPUT.mkdir(parents=True, exist_ok=True)

    with (OUTPUT / "game.log").open("w") as game_log, (OUTPUT / "desktop.log").open("w") as desktop_log:
        process = subprocess.Popen([str(executable)], cwd=ROOT, stdout=game_log, stderr=game_log)
        try:
            window = None

            for _ in range(100):
                assert process.poll() is None, "Game exited during startup; see artifacts/window-layout/game.log."
                result = subprocess.run(
                    ["xdotool", "search", "--onlyvisible", "--pid", str(process.pid)],
                    text=True, stdout=subprocess.PIPE, stderr=desktop_log,
                )
                if result.stdout.strip():
                    window = result.stdout.splitlines()[0]
                    break

                time.sleep(0.1)

            assert window, "Game window did not appear."
            time.sleep(2)

            for index, size in enumerate(SIZES):
                if index:
                    subprocess.run(["xdotool", "windowsize", window, *map(str, size)],
                                   stdout=desktop_log, stderr=desktop_log, check=True)
                    time.sleep(1)

                path = OUTPUT / f"{index}-{size[0]}x{size[1]}.png"
                subprocess.run(["import", "-window", window, str(path)],
                               stdout=desktop_log, stderr=desktop_log, check=True)
                verify_capture(path, size)
        finally:
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()


if __name__ == "__main__":
    main()
