#!/usr/bin/env python3
"""Build a legacy Unity .unitypackage archive without launching Unity."""

from __future__ import annotations

import argparse
import io
import re
import tarfile
from pathlib import Path, PurePosixPath


GUID_PATTERN = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)


def add_bytes(archive: tarfile.TarFile, name: str, data: bytes) -> None:
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mode = 0o644
    info.mtime = 0
    archive.addfile(info, io.BytesIO(data))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("package", type=Path, help="Package directory relative to the repository root")
    parser.add_argument("output", type=Path, help="Destination .unitypackage")
    args = parser.parse_args()

    package = args.package.resolve()
    repository = Path.cwd().resolve()
    if not package.is_dir():
        raise SystemExit(f"Package directory does not exist: {package}")

    try:
        package.relative_to(repository)
    except ValueError as error:
        raise SystemExit("Package must be located inside the repository") from error

    records: list[tuple[str, Path, Path]] = []
    seen_guids: set[str] = set()
    for meta in sorted(package.rglob("*.meta")):
        text = meta.read_text(encoding="utf-8-sig")
        match = GUID_PATTERN.search(text)
        if not match:
            raise SystemExit(f"Missing GUID in {meta}")

        guid = match.group(1).lower()
        if guid in seen_guids:
            raise SystemExit(f"Duplicate GUID {guid} in {meta}")
        seen_guids.add(guid)

        asset = Path(str(meta)[:-5])
        if not asset.exists():
            raise SystemExit(f"Meta file has no matching asset: {meta}")
        records.append((guid, asset, meta))

    assets_without_meta = [
        path
        for path in package.rglob("*")
        if path.is_file() and path.suffix != ".meta" and not Path(str(path) + ".meta").exists()
    ]
    if assets_without_meta:
        listing = "\n".join(str(path) for path in assets_without_meta)
        raise SystemExit(f"Assets missing .meta files:\n{listing}")

    if not records:
        raise SystemExit("No Unity assets found")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with tarfile.open(args.output, "w:gz", format=tarfile.PAX_FORMAT) as archive:
        for guid, asset, meta in records:
            prefix = guid
            pathname = PurePosixPath(asset.relative_to(repository).as_posix()).as_posix()
            add_bytes(archive, f"{prefix}/pathname", (pathname + "\n").encode("utf-8"))
            add_bytes(archive, f"{prefix}/asset.meta", meta.read_bytes())
            if asset.is_file():
                add_bytes(archive, f"{prefix}/asset", asset.read_bytes())

    print(f"Built {args.output} with {len(records)} assets")


if __name__ == "__main__":
    main()
