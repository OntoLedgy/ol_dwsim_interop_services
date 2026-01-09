"""Helpers for validating case file paths against allowed roots."""

from __future__ import annotations

from pathlib import Path
from typing import Iterable


def resolve_case_path(file_path: str, allowed_roots: Iterable[str]) -> Path:
    """Return a normalized path if it is within an allowed root."""
    if not file_path or not str(file_path).strip():
        raise ValueError("file_path is required.")

    roots = [Path(root).expanduser() for root in allowed_roots]
    if not roots:
        raise ValueError("No allowed case storage roots configured.")

    target = Path(file_path).expanduser().resolve(strict=False)
    for root in roots:
        resolved_root = root.resolve(strict=False)
        try:
            if target.is_relative_to(resolved_root):
                return target
        except ValueError:
            continue

    allowed_list = ", ".join(str(root) for root in roots)
    raise ValueError(f"file_path must be within allowed roots: {allowed_list}")
