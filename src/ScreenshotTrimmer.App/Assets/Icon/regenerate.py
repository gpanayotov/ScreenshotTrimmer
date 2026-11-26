#!/usr/bin/env python3
"""Regenerates the icon's PNG sizes and the Windows .ico from screenshot-trimmer.svg.

Run it from anywhere after changing the SVG, then commit the changed files:

    python3 src/ScreenshotTrimmer.App/Assets/Icon/regenerate.py

It writes screenshot-trimmer-<size>.png for each size below and screenshot-trimmer.ico holding all of them, beside
this script. Rendering the same SVG again gives the same files byte for byte.

Needs librsvg through PyGObject (python3-gi, gir1.2-rsvg-2.0), pycairo (python3-gi-cairo) and Pillow
(python3-pil), all packaged by common Linux distributions.
"""

import io
from pathlib import Path

import cairo
import gi

gi.require_version("Rsvg", "2.0")
from gi.repository import Rsvg  # noqa: E402
from PIL import Image  # noqa: E402

SIZES = (16, 32, 48, 64, 128, 256)
HERE = Path(__file__).resolve().parent
SOURCE = HERE / "screenshot-trimmer.svg"


def render(svg: Rsvg.Handle, size: int) -> Image.Image:
    surface = cairo.ImageSurface(cairo.FORMAT_ARGB32, size, size)
    context = cairo.Context(surface)
    viewport = Rsvg.Rectangle()
    viewport.x, viewport.y, viewport.width, viewport.height = 0, 0, size, size
    svg.render_document(context, viewport)
    surface.flush()
    png = io.BytesIO()
    surface.write_to_png(png)
    png.seek(0)
    return Image.open(png).convert("RGBA")


def main() -> None:
    svg = Rsvg.Handle.new_from_file(str(SOURCE))
    images = [render(svg, size) for size in SIZES]
    for size, image in zip(SIZES, images):
        image.save(HERE / f"screenshot-trimmer-{size}.png", optimize=True)
    # Each size is rendered from the SVG rather than scaled down from the largest one.
    largest = images[-1]
    largest.save(
        HERE / "screenshot-trimmer.ico",
        sizes=[(size, size) for size in SIZES],
        append_images=images[:-1],
    )


if __name__ == "__main__":
    main()
