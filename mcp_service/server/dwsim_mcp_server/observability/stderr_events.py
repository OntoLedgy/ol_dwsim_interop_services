# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Helpers for safe JSON event emission on stderr."""

from __future__ import annotations

import json
import sys
from typing import Any


def emit_event_to_stderr(**payload: Any) -> None:
    print(json.dumps(payload), file=sys.stderr, flush=True)
