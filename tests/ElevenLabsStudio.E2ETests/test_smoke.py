"""Real end-to-end smoke test for ElevenLabsStudio.

Spins up the built ElevenLabsStudio.exe via Windows UI Automation
(pywinauto), waits for the main window to settle, asserts the three
mock agents are populated (mock mode is on by default), opens the
Settings dialog via the gear button, then shuts everything down.

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

import pytest
from pywinauto import Desktop


def _owned_dialog(launched_exe, title_re: str):
    """Resolve an owned WPF window by HWND, then inspect it through UIA.

    pywinauto's UIA top-level enumeration omits Caliburn's owned dialog
    windows even though they are visible and have native handles.
    """
    native = Desktop(backend="win32").window(
        process=launched_exe.proc.pid, title_re=title_re)
    native.wait("visible", timeout=10)
    return Desktop(backend="uia").window(handle=native.handle)


@pytest.mark.e2e
def test_window_title_is_elevenlabs_studio(launched_exe) -> None:
    """The exe boots, the main window appears, the title matches.

    Acts as the canary: any failure here usually means a
    composition-root exception (e.g. CM5 ViewLocator can't find a
    view) and the rest of the suite is irrelevant.
    """
    main_window = launched_exe.main_window
    assert main_window.exists(timeout=15), "main window did not appear within 15s"
    assert "ElevenLabs Studio" in main_window.window_text()


@pytest.mark.e2e
def test_left_sidebar_lists_three_mock_agents(launched_exe) -> None:
    """With Mock=true (the default in appsettings.json) the sidebar
    shows the three seeded agents: Sales Rep, Support Bot, Onboarding
    Guide. We match by name so a future seeded re-order doesn't
    break the test."""
    launched_exe.main_window.wait("ready", timeout=15)
    main_window = launched_exe.main_window
    for name in ("Sales Rep", "Support Bot", "Onboarding Guide"):
        assert main_window.child_window(title=name, control_type="ListItem").exists(timeout=10), (
            f"Mock agent {name!r} not found in the sidebar"
        )


@pytest.mark.e2e
def test_gear_button_opens_settings_dialog(launched_exe) -> None:
    """Click the gear caption button and assert the Settings dialog
    opens with its ApiKey hint and Mock mode toggle."""
    launched_exe.main_window.wait("ready", timeout=15)
    main_window = launched_exe.main_window

    gear = main_window.child_window(
        title="设置", auto_id="OpenSettings", control_type="Button")
    # pywinauto can fall back to text match if auto_id is empty.
    if not gear.exists():
        gear = main_window.child_window(title="设置", control_type="Button")
    assert gear.exists(timeout=5), "settings gear button not found in title bar"

    gear.invoke()
    # Owned WPF dialogs are located by native handle first; see helper.
    settings = _owned_dialog(launched_exe, "设置.*ElevenLabs Studio")
    settings.wait("ready", timeout=10)
    assert settings.exists(), "Settings dialog did not open after clicking the gear"

    # Mock mode CheckBox + ApiKey PasswordBox are present.
    assert settings.child_window(title_re="使用离线 Mock 数据.*", control_type="CheckBox").exists()
    assert settings.child_window(control_type="Edit", auto_id="ApiKey").exists() or \
           settings.child_window(control_type="Edit").exists()


@pytest.mark.e2e
def test_pull_button_opens_pull_agent_dialog(launched_exe) -> None:
    """Click the sidebar action and assert the pull-by-ID dialog opens."""
    launched_exe.main_window.wait("ready", timeout=15)
    main_window = launched_exe.main_window

    pull = main_window.child_window(
        title_re=".*拉取 Agent.*", auto_id="PullAgentById", control_type="Button")
    if not pull.exists():
        pull = main_window.child_window(title_re=".*拉取 Agent.*", control_type="Button")
    assert pull.exists(timeout=5), "pull Agent button not found in sidebar"

    pull.invoke()
    dialog = _owned_dialog(launched_exe, "按 ID 拉取 Agent")
    dialog.wait("ready", timeout=10)
    assert dialog.exists(), "Pull Agent dialog did not open after clicking the button"
