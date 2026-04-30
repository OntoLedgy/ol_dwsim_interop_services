#!/usr/bin/env python3

# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

from __future__ import annotations

import argparse
import json
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path


DEFAULT_SOURCE = "mcp_service/dwsim_worker/DwsimWorker/bin/Debug"
DEFAULT_DEST = "mcp_service/server/prebuilt/DwsimWorker"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Package DwsimWorker prebuilt binaries.",
    )
    parser.add_argument(
        "--source",
        default=DEFAULT_SOURCE,
        help=f"Build output directory (default: {DEFAULT_SOURCE})",
    )
    parser.add_argument(
        "--dest",
        default=DEFAULT_DEST,
        help=f"Destination directory (default: {DEFAULT_DEST})",
    )
    return parser.parse_args()


def should_include(path: Path) -> bool:
    name_lower = path.name.lower()
    if path.suffix.lower() == ".pdb":
        return False
    if name_lower.startswith("dwsim.") and path.suffix.lower() == ".dll":
        return False
    return True


def format_timestamp(ts: float) -> str:
    return datetime.fromtimestamp(ts, tz=timezone.utc).isoformat()


def build_manifest(dest: Path, files: list[Path], source: Path) -> dict:
    entries: list[dict] = []
    for file_path in sorted(files, key=lambda item: item.name.lower()):
        stat = file_path.stat()
        entries.append(
            {
                "name": file_path.name,
                "size_bytes": stat.st_size,
                "modified_utc": format_timestamp(stat.st_mtime),
            }
        )
    return {
        "generated_at_utc": format_timestamp(datetime.now(tz=timezone.utc).timestamp()),
        "source": str(source),
        "dest": str(dest),
        "files": entries,
    }


def main() -> int:
    args = parse_args()
    source = Path(args.source)
    dest = Path(args.dest)

    if not source.exists():
        print(f"Source directory does not exist: {source}", file=sys.stderr)
        return 1
    if not source.is_dir():
        print(f"Source path is not a directory: {source}", file=sys.stderr)
        return 1

    dest.mkdir(parents=True, exist_ok=True)

    source_files = [path for path in source.iterdir() if path.is_file()]
    if not any(path.name == "DwsimWorker.dll" for path in source_files):
        print("DwsimWorker.dll not found in source directory.", file=sys.stderr)
        return 1

    copied_files: list[Path] = []
    for path in source_files:
        if not should_include(path):
            continue
        target = dest / path.name
        shutil.copy2(path, target)
        copied_files.append(target)

    dest_worker = dest / "DwsimWorker.dll"
    if not dest_worker.exists():
        print("DwsimWorker.dll was not copied to destination.", file=sys.stderr)
        return 1

    manifest = build_manifest(dest, copied_files, source)
    manifest_path = dest / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2, sort_keys=True), encoding="utf-8")

    print(f"Copied {len(copied_files)} files to {dest}")
    print(f"Manifest written to {manifest_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
