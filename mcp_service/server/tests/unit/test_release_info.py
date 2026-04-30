# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

from __future__ import annotations

from dwsim_mcp_server import release_info


def test_get_release_info_returns_expected_keys(monkeypatch) -> None:
    monkeypatch.setattr(
        release_info.metadata,
        "version",
        lambda package_name: "1.2.3",
    )
    monkeypatch.setenv("DWSIM_MCP_COMMIT_SHA", "0123456789abcdef")
    monkeypatch.setattr(
        release_info,
        "import_module",
        lambda module_name: (_ for _ in ()).throw(ModuleNotFoundError(module_name)),
    )

    info = release_info.get_release_info()

    assert set(info) == {
        "package",
        "version",
        "commit_sha",
        "source_url",
        "license",
    }
    assert all(isinstance(value, str) and value for value in info.values())


def test_get_commit_sha_returns_unknown_without_module_or_env(monkeypatch) -> None:
    monkeypatch.delenv("DWSIM_MCP_COMMIT_SHA", raising=False)
    monkeypatch.setattr(
        release_info,
        "import_module",
        lambda module_name: (_ for _ in ()).throw(ModuleNotFoundError(module_name)),
    )

    assert release_info.get_commit_sha() == "unknown"


def test_get_commit_sha_uses_env_var_when_commit_module_is_missing(monkeypatch) -> None:
    monkeypatch.setenv("DWSIM_MCP_COMMIT_SHA", "deadbeef")
    monkeypatch.setattr(
        release_info,
        "import_module",
        lambda module_name: (_ for _ in ()).throw(ModuleNotFoundError(module_name)),
    )

    assert release_info.get_commit_sha() == "deadbeef"
