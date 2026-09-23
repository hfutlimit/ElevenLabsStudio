"""Real end-to-end smoke test for ElevenLabsStudio.

Spins up the built ElevenLabsStudio.exe via Windows UI Automation
(pywinauto), waits for the main window and real Agent GET to settle,
then shuts everything down.

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
import time
from pywinauto import Desktop


def _wait_for_agents(main_window, timeout: float = 30):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        agents = main_window.descendants(control_type="ListItem")
        if agents:
            return agents
        time.sleep(0.25)
    return []


def _wait_for_tab_with_child(main_window, child_text: str, timeout: float = 30):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        for tab in main_window.descendants(control_type="TabItem"):
            if child_text in tab.children_texts():
                return tab
        time.sleep(0.25)
    return None


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
    assert main_window.exists(timeout=30), "main window did not appear within 30s"
    assert "ElevenLabs Studio" in main_window.window_text()


@pytest.mark.e2e
def test_left_sidebar_lists_the_focused_real_agent(launched_exe) -> None:
    """The sidebar contains the single Agent loaded by the real API."""
    launched_exe.main_window.wait("ready", timeout=15)
    main_window = launched_exe.main_window
    agents = _wait_for_agents(main_window)
    assert len(agents) == 1, f"expected one configured Agent, found {len(agents)}"
    assert agents[0].window_text().strip(), "real Agent was loaded without a display name"


@pytest.mark.e2e
def test_workflow_separates_canvas_and_raw_json_into_tabs(launched_exe) -> None:
    """The visual workflow gets the full editor area and Raw JSON is separate."""
    main_window = launched_exe.main_window
    main_window.wait("ready", timeout=15)

    agents = _wait_for_agents(main_window)
    assert len(agents) == 1, f"expected one configured Agent, found {len(agents)}"
    staging = agents[0]
    staging.select()

    outer_workflow = _wait_for_tab_with_child(main_window, "Workflow")
    assert outer_workflow is not None, "Workflow tab was not loaded from the real Agent"
    outer_workflow.select()

    canvas_tab = main_window.child_window(title="Workflow canvas", control_type="TabItem")
    raw_json_tab = main_window.child_window(title="Raw JSON", control_type="TabItem")
    assert canvas_tab.exists(timeout=5), "full-size Workflow canvas tab not found"
    assert raw_json_tab.exists(timeout=5), "separate Raw JSON tab not found"

    raw_json_tab.select()
    assert raw_json_tab.is_selected(), "Raw JSON tab did not become active"


@pytest.mark.e2e
def test_gear_button_opens_settings_dialog(launched_exe) -> None:
    """Click the gear caption button and assert the Settings dialog
    opens with its ApiKey hint and Mock mode toggle."""
    launched_exe.main_window.wait("ready", timeout=15)
    main_window = launched_exe.main_window

    gear = main_window.child_window(
        title="Settings", auto_id="OpenSettings", control_type="Button")
    # pywinauto can fall back to text match if auto_id is empty.
    if not gear.exists():
        gear = main_window.child_window(title="Settings", control_type="Button")
    assert gear.exists(timeout=5), "settings gear button not found in title bar"

    gear.invoke()
    # Owned WPF dialogs are located by native handle first; see helper.
    settings = _owned_dialog(launched_exe, "Settings.*ElevenLabs Studio")
    settings.wait("ready", timeout=10)
    assert settings.exists(), "Settings dialog did not open after clicking the gear"

    # Mock mode CheckBox + ApiKey PasswordBox are present.
    assert settings.child_window(title_re="Use offline mock data.*", control_type="CheckBox").exists()
    assert settings.child_window(control_type="Edit", auto_id="ApiKey").exists() or \
           settings.child_window(control_type="Edit").exists()


@pytest.mark.e2e
def test_pull_button_opens_pull_agent_dialog(launched_exe) -> None:
    """Click the sidebar action and assert the pull-by-ID dialog opens."""
    launched_exe.main_window.wait("ready", timeout=15)
    main_window = launched_exe.main_window

    pull = main_window.child_window(
        title_re=".*Pull Agent.*", auto_id="PullAgentById", control_type="Button")
    if not pull.exists():
        pull = main_window.child_window(title_re=".*Pull Agent.*", control_type="Button")
    assert pull.exists(timeout=5), "pull Agent button not found in sidebar"

    pull.invoke()
    dialog = _owned_dialog(launched_exe, "Pull Agent by ID")
    dialog.wait("ready", timeout=10)
    assert dialog.exists(), "Pull Agent dialog did not open after clicking the button"
