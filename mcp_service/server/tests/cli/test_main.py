# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

from pathlib import Path

import pytest
from typer.testing import CliRunner

from dwsim_mcp_server.cli import main


@pytest.fixture()
def runner():
    return CliRunner()


def test_run_command_invokes_server(mocker, runner):
    run_mock = mocker.patch.object(main.server, "run")

    result = runner.invoke(main.app, ["run"])

    assert result.exit_code == 0
    run_mock.assert_called_once_with()


def test_default_invokes_server(mocker, runner):
    run_mock = mocker.patch.object(main.server, "run")

    result = runner.invoke(main.app, [])

    assert result.exit_code == 0
    run_mock.assert_called_once_with()


def test_version_outputs_expected_fields(mocker, runner):
    mocker.patch.object(main.metadata, "version", return_value="1.2.3")
    mocker.patch.object(main.platform, "python_version", return_value="3.11.9")
    mocker.patch.object(main.platform, "platform", return_value="Windows-10")
    mocker.patch.object(main, "_get_dotnet_status", return_value="Available (pythonnet 3.0)")

    result = runner.invoke(main.app, ["version"])

    assert result.exit_code == 0
    assert "Package" in result.output
    assert "1.2.3" in result.output
    assert "Python" in result.output
    assert "3.11.9" in result.output
    assert "Platform" in result.output
    assert ".NET/pythonnet" in result.output


def test_init_writes_files(tmp_path, mocker, runner):
    mocker.patch.object(main, "_get_template_environment", return_value=object())
    mocker.patch.object(main, "_render_template", return_value="hello")

    result = runner.invoke(
        main.app,
        ["init", "--output", str(tmp_path), "--mcp-config"],
    )

    assert result.exit_code == 0
    assert (tmp_path / ".env.example").read_text(encoding="utf-8") == "hello"
    assert (tmp_path / "mcp.json").read_text(encoding="utf-8") == "hello"


def test_init_skips_existing_file_without_force(tmp_path, mocker, runner):
    existing = tmp_path / ".env.example"
    existing.write_text("existing", encoding="utf-8")

    mocker.patch.object(main, "_get_template_environment", return_value=object())
    mocker.patch.object(main, "_render_template", return_value="new-content")
    diff_mock = mocker.patch.object(main, "_print_diff")

    result = runner.invoke(main.app, ["init", "--output", str(tmp_path)])

    assert result.exit_code == 0
    assert "File exists; skipping" in result.output
    diff_mock.assert_called_once()
    assert existing.read_text(encoding="utf-8") == "existing"


def test_init_reports_output_dir_failure(tmp_path, mocker, runner):
    output_dir = tmp_path / "nested"

    def _raise(*args, **kwargs):
        raise OSError("nope")

    mocker.patch.object(Path, "mkdir", side_effect=_raise)

    result = runner.invoke(main.app, ["init", "--output", str(output_dir)])

    assert result.exit_code == 1
    assert "Failed to create output directory" in result.output
