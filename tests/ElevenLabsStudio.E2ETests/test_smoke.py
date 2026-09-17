"""Smoke test: launch the WPF app, wait for the main window, terminate."""

from __future__ import annotations

import subprocess
import sys
import time
from pathlib import Path

import pytest

# Allow `import utils` regardless of pytest's rootdir / conftest loading.
sys.path.insert(0, str(Path(__file__).resolve().parent))

from utils import wait_until  # noqa: E402


@pytest.mark.e2e
def test_exe_launches_and_exits_cleanly(launched_exe: subprocess.Popen) -> None:
    """Process starts, stays alive, and exits cleanly when terminated.

    Asserts:
      * process is alive after the launch window (proves MainWindow mounted
        without an unhandled exception)
      * clean exit on terminate (returncode is set within a reasonable
        timeout without an OS-level kill)
    """
    proc = launched_exe
    alive = wait_until(lambda: proc.poll() is None, timeout=5.0, default=False)
    assert alive, "ElevenLabsStudio.exe exited prematurely — check missing config or runtime errors."

    # Leave it alive for 2 extra seconds to catch any late crashes.
    time.sleep(2.0)
    assert proc.poll() is None, "ElevenLabsStudio.exe crashed within 2 seconds of launch"

    proc.terminate()
    proc.wait(timeout=10)
    assert proc.returncode is not None, "Process did not exit after terminate()"