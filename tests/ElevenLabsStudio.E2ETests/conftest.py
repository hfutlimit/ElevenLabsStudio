"""Shared fixtures for ElevenLabsStudio E2E suite.

Real tests launch the built .exe, attach via pywinauto / Win32 automation,
and assert on window state. Keep network calls off this layer — they
belong in IntegrationTests.
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
def launched_exe(elevenlabs_exe: Path) -> Iterator[subprocess.Popen]:
    """Launch ElevenLabsStudio.exe for the lifetime of one test.

    Cleanup:
        Terminates the process, then runs ``taskkill /IM /F`` as a
        belt-and-braces sweep so no zombie window survives the test.
    """
    creationflags = 0
    if sys.platform == "win32":
        # CREATE_NEW_PROCESS_GROUP so we can signal the whole tree on teardown.
        creationflags = subprocess.CREATE_NEW_PROCESS_GROUP  # type: ignore[attr-defined]

    proc = subprocess.Popen(
        [str(elevenlabs_exe)],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        creationflags=creationflags,
    )
    try:
        yield proc
    finally:
        proc.terminate()
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait(timeout=5)
        if sys.platform == "win32" and shutil.which("taskkill"):
            subprocess.run(
                ["taskkill", "/IM", "ElevenLabsStudio.exe", "/F"],
                check=False,
                capture_output=True,
            )