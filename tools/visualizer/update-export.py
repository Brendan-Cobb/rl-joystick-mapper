"""Refresh the editable fragment inside the existing standalone browser wrapper."""

from html import escape, unescape
from pathlib import Path
import re


def main() -> None:
    directory = Path(__file__).resolve().parent
    fragment = (directory / "comparison.fragment.html").read_text(encoding="utf-8")
    destination = directory / "index.html"
    document = destination.read_text(encoding="utf-8")
    marker = re.compile(
        r"<!-- RLREMAPPER_VISUALIZER_START -->.*?<!-- RLREMAPPER_VISUALIZER_END -->",
        re.DOTALL,
    )
    frame_attribute = re.compile(r'data-srcdoc="([^"]*)"')
    matches = list(frame_attribute.finditer(document))
    if len(matches) != 1 or len(marker.findall(fragment)) != 1:
        raise ValueError("Expected one standalone iframe and one source fragment.")
    match = matches[0]
    frame = unescape(match.group(1))
    if len(marker.findall(frame)) != 1:
        raise ValueError("The standalone document's fragment markers are missing.")
    updated_frame = marker.sub(lambda _: fragment.strip(), frame)
    updated_document = (
        document[: match.start(1)]
        + escape(updated_frame, quote=True)
        + document[match.end(1) :]
    )
    destination.write_text(updated_document, encoding="utf-8", newline="\n")
    print(destination)


if __name__ == "__main__":
    main()
