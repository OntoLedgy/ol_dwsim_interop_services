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

"""Override session-scoped DWSIM fixtures for adapter unit tests.

Adapter tests mock the SessionClient and do not need DWSIM binaries.
"""

import pytest


@pytest.fixture(scope="session")
def dwsim_worker_dll_path():
    """Stub — adapter tests do not need the DwsimWorker DLL."""
    return None


@pytest.fixture(scope="session", autouse=True)
def set_dwsim_worker_env():
    """Stub — adapter tests do not touch DWSIM environment."""
    yield
