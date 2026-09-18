"""Diagnostic: launch the Release exe, click the PullAgentById button
(convention-bound to an Async-suffixed VM method, same mechanism as the
settings gear), and list top-level windows. Discriminates between
"a11y/convention broken everywhere" vs "Settings-specific failure".
"""

from __future__ import annotations

import subprocess
import sys
import time
from pathlib import Path

from pywinauto import Application

PROJECT_ROOT = Path(__file__).resolve().parents[2]
EXE = PROJECT_ROOT / "src" / "ElevenLabsStudio" / "bin" / "Release" / "net10.0-windows" / "ElevenLabsStudio.exe"


def dump_windows(app: Application, label: str) -> None:
    print(f"=== top-level windows {label} ===")
    for w in app.windows():
        print(f"  [{w.element_info.control_type}] {w.window_text()!r}")


def main() -> int:
    proc = subprocess.Popen(
        [str(EXE)],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    try:
        app = Application(backend="uia").connect(process=proc.pid, timeout=15)
        main_window = app.window(title_re="ElevenLabs Studio.*")
        main_window.wait("ready", timeout=15)
        time.sleep(3)
        dump_windows(app, "BEFORE")

        pull = main_window.child_window(auto_id="PullAgentById", control_type="Button")
        print("pull button exists:", pull.exists(timeout=5))
        pull.click()
        time.sleep(4)
        dump_windows(app, "AFTER pull click")
        return 0
    finally:
        proc.terminate()
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait(timeout=5)
        out, err = proc.communicate()
        print("=== stdout ===")
        print((out or "")[-3000:])
        print("=== stderr ===")
        print((err or "")[-3000:])


if __name__ == "__main__":
    sys.exit(main())
