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

import os
import platform
import subprocess
import sys
import tempfile
import venv
import zipfile
from pathlib import Path

import pytest

pytestmark = pytest.mark.integration

PROJECT_ROOT = Path(__file__).resolve().parents[3]


def _run_command(command: list[str], *, cwd: Path | None = None) -> subprocess.CompletedProcess:
    try:
        result = subprocess.run(
            command,
            cwd=str(cwd) if cwd else None,
            text=True,
            capture_output=True,
            env=_command_env(),
        )
    except FileNotFoundError as exc:
        command_display = " ".join(str(part) for part in command)
        pytest.fail(f"Command not found: {command_display}\n{exc}")
    if result.returncode != 0:
        command_display = " ".join(str(part) for part in command)
        stdout = result.stdout.strip()
        stderr = result.stderr.strip()
        message = [f"Command failed: {command_display}"]
        if stdout:
            message.append(f"stdout:\n{stdout}")
        if stderr:
            message.append(f"stderr:\n{stderr}")
        pytest.fail("\n".join(message))
    return result


def _command_env() -> dict[str, str]:
    env = os.environ.copy()
    env["PIP_DISABLE_PIP_VERSION_CHECK"] = "1"
    env["PYTHONNOUSERSITE"] = "1"
    return env


def _venv_python_path(venv_dir: Path) -> Path:
    if os.name == "nt":
        return venv_dir / "Scripts" / "python.exe"
    return venv_dir / "bin" / "python"


def _venv_script_path(venv_dir: Path, script_name: str) -> Path:
    scripts_dir = venv_dir / ("Scripts" if os.name == "nt" else "bin")
    if os.name != "nt":
        candidate = scripts_dir / script_name
        if not candidate.exists():
            pytest.fail(f"Expected {script_name} in venv at {candidate}")
        return candidate
    candidates = [
        scripts_dir / f"{script_name}.exe",
        scripts_dir / f"{script_name}.cmd",
        scripts_dir / f"{script_name}.bat",
        scripts_dir / script_name,
    ]
    for candidate in candidates:
        if candidate.exists():
            return candidate
    pytest.fail(f"Expected {script_name} entry point in venv at {scripts_dir}")


def _create_venv(venv_dir: Path) -> Path:
    builder = venv.EnvBuilder(with_pip=True)
    builder.create(str(venv_dir))
    python_path = _venv_python_path(venv_dir)
    assert python_path.exists(), f"Expected venv python at {python_path}"
    return python_path


def _install_wheel(python_path: Path, wheel_path: Path) -> None:
    _run_command([str(python_path), "-m", "pip", "install", str(wheel_path)])


@pytest.fixture(scope="module")
def built_wheel_path() -> Path:
    with tempfile.TemporaryDirectory() as temp_dir:
        out_dir = Path(temp_dir)
        _run_command(
            [
                sys.executable,
                "-m",
                "build",
                "--wheel",
                "--outdir",
                str(out_dir),
                "--no-isolation",
            ],
            cwd=PROJECT_ROOT,
        )
        wheels = sorted(out_dir.glob("*.whl"))
        assert wheels, "Wheel build succeeded but no wheel was produced."
        yield wheels[0]


@pytest.fixture()
def fresh_venv(built_wheel_path: Path) -> tuple[Path, Path]:
    with tempfile.TemporaryDirectory() as temp_dir:
        venv_dir = Path(temp_dir) / "venv"
        python_path = _create_venv(venv_dir)
        _install_wheel(python_path, built_wheel_path)
        yield venv_dir, python_path


def test_build_wheel(built_wheel_path: Path) -> None:
    assert built_wheel_path.exists()


def test_wheel_contents(built_wheel_path: Path) -> None:
    with zipfile.ZipFile(built_wheel_path) as wheel:
        wheel_entries = set(wheel.namelist())

    template_dir = PROJECT_ROOT / "dwsim_mcp_server" / "templates"
    templates = sorted(template_dir.glob("*.j2"))
    assert templates, "Expected template files in dwsim_mcp_server/templates."
    for template in templates:
        expected = f"dwsim_mcp_server/templates/{template.name}"
        assert expected in wheel_entries, f"Missing template in wheel: {expected}"

    prebuilt_dir = PROJECT_ROOT / "prebuilt"
    dlls = sorted(prebuilt_dir.glob("*.dll")) if prebuilt_dir.exists() else []
    if platform.system() == "Windows" and dlls:
        for dll in dlls:
            expected = f"prebuilt/{dll.name}"
            assert expected in wheel_entries, f"Missing DLL in wheel: {expected}"


def test_install_in_fresh_venv(fresh_venv: tuple[Path, Path]) -> None:
    _venv_dir, python_path = fresh_venv
    _run_command([str(python_path), "-c", "import dwsim_mcp_server"])


def test_cli_help(fresh_venv: tuple[Path, Path]) -> None:
    venv_dir, _python_path = fresh_venv
    cli_path = _venv_script_path(venv_dir, "dwsim-mcp")
    result = _run_command([str(cli_path), "--help"])
    output = f"{result.stdout}\n{result.stderr}"
    assert "Usage" in output or "usage" in output


def test_cli_version(fresh_venv: tuple[Path, Path]) -> None:
    venv_dir, _python_path = fresh_venv
    cli_path = _venv_script_path(venv_dir, "dwsim-mcp")
    result = _run_command([str(cli_path), "version"])
    output = f"{result.stdout}\n{result.stderr}"
    assert "Package" in output
