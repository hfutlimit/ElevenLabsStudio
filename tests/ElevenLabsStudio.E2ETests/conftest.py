"""Shared fixtures for ElevenLabsStudio E2E suite.

Real tests launch the built ElevenLabsStudio.exe via Windows UI
Automation (pywinauto), wait for the main window to settle, assert
the three mock agents are populated (mock mode is on by default),
opens the Settings dialog via the gear button, then shuts
everything down.

These tests are slow (5-10s per case, mostly process startup) and
require the Release .exe to be present at the conventional output
path; they deliberately do not run as part of `dotnet test`. Run
them with:

    pip install -r tests/ElevenLabsStudio.E2ETests/requirements.txt
    pytest -m e2e tests/ElevenLabsStudio.E2ETests/

The pytest.ini / conftest.py / utils.py already in this folder pick
up the right `launched_exe` fixture, kill stragglers on teardown,
and skip the run when the .exe is missing.
"""

from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path
from typing import Iterator

import pytest


PROJECT_ROOT = Path(__file__).resolve().parents[2]
EXE_CANDIDATES = [
    PROJECT_ROOT / "src" / "ElevenLabsStudio" / "bin" / "Release" / "net10.0-windows" / "ElevenLabsStudio.exe",
    PROJECT_ROOT / "src" / "ElevenLabsStudio" / "bin" / "Debug" / "net10.0-windows" / "ElevenLabsStudio.exe",
]


class LaunchedApp:
    """Thin wrapper around the subprocess + pywinauto Application."""

    def __init__(self, proc: subprocess.Popen) -> None:
        self.proc = proc
        # Imported lazily so pywinauto is only required when an e2e
        # test actually runs.
        from pywinauto import Application  # type: ignore
        self.app = Application(backend="uia").connect(process=proc.pid)

    @property
    def main_window(self):
        # The custom chrome means the WPF window has no system title
        # bar of its own; window_text() returns "ElevenLabs Studio".
        return self.app.window(title_re="ElevenLabs Studio.*")


def _locate_exe() -> Path:
    for candidate in EXE_CANDIDATES:
        if candidate.exists():
            return candidate
    raise SystemExit(
        "ElevenLabsStudio.exe not found. Build it first:\n"
        "  dotnet build ElevenLabsStudio.slnx -c Debug"
    )


@pytest.fixture(scope="session")
def elevenlabs_exe() -> Path:
    return _locate_exe()


@pytest.fixture()
def launched_exe(elevenlabs_exe: Path) -> Iterator[LaunchedApp]:
    """Launch the .exe and yield a LaunchedApp handle for the test.

    Cleanup kills the process tree and any stray ElevenLabsStudio.exe
    that survived (e.g. child processes spawned by the test process
    itself) so the next test starts clean.
    """
    creationflags = 0
    if sys.platform == "win32":
        creationflags = subprocess.CREATE_NEW_PROCESS_GROUP  # type: ignore[attr-defined]

    proc = subprocess.Popen(
        [str(elevenlabs_exe)],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        creationflags=creationflags,
    )
    app = LaunchedApp(proc)
    try:
        yield app
    finally:
        proc.terminate()
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait(timeout=5)
        if sys.platform == "win32" and shutil.which("taskkill"):
            subprocess.run(  # noqa: S603 — best-effort cleanup
                ["taskkill", "/IM", "ElevenLabsStudio.exe", "/F"],
                check=False,
                capture_output=True,
            )