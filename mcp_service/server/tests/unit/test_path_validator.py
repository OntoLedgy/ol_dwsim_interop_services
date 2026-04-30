# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

from pathlib import Path

import pytest

from dwsim_mcp_server.utils.path_validator import resolve_case_path


def test_resolve_case_path_allows_within_root(tmp_path: Path) -> None:
    root = tmp_path / "cases"
    target = root / "case.dwxml"

    resolved = resolve_case_path(str(target), [str(root)])
    assert resolved == target.resolve(strict=False)


def test_resolve_case_path_rejects_outside_root(tmp_path: Path) -> None:
    root = tmp_path / "cases"
    outside = tmp_path / "other" / "case.dwxml"

    with pytest.raises(ValueError):
        resolve_case_path(str(outside), [str(root)])


def test_resolve_case_path_requires_value() -> None:
    with pytest.raises(ValueError):
        resolve_case_path("  ", ["/tmp"])
