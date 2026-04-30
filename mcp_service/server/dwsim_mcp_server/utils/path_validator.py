# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# This file is part of the OntoLedgy Thermodynamics Architecture and is
# dual-licensed:
#
#   1. Open source under the GNU Affero General Public License v3.0 or
#      later (AGPL-3.0-or-later). See the LICENSE file in the repository
#      root for the full licence text and NOTICE for attribution.
#   2. Commercial under a separate proprietary licence offered by
#      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

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
