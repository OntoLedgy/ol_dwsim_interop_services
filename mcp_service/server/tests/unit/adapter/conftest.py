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
