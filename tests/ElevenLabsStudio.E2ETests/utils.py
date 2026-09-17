"""Standalone helpers usable by both conftest.py and individual tests.

`conftest.py` is pytest-private — importing from it directly raises
``ImportError`` because it is not on sys.path. Putting utility functions
in this module keeps the surface area clean.
"""

from __future__ import annotations

import os
import time
from typing import Callable, TypeVar

T = TypeVar("T")


def wait_until(predicate: Callable[[], T], timeout: float = 30.0,
               interval: float = 0.5, default: T | None = None) -> T | None:
    """Spin until ``predicate()`` returns truthy or ``timeout`` elapses."""
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        result = predicate()
        if result:
            return result
        time.sleep(interval)
    return default


def api_key_from_env() -> str | None:
    """Read API key from either standard env var or ASP.NET-style nested key."""
    return os.environ.get("ELEVENLABS_API_KEY") or os.environ.get("ELEVENLABS__APIKEY")