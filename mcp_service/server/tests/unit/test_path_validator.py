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
