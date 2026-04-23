from __future__ import annotations

from pathlib import Path

import pytest
import pytest_asyncio

from dwsim_mcp_server.adapter import DwsimAdapterConfig, build_dwsim_adapter
from dwsim_mcp_server.adapter.alias_seeder import (
    DWSIM_WORKER_SUPPORTED_PROPERTY_PACKAGE_MODEL_IDS,
)

pytestmark = pytest.mark.live_dwsim


@pytest_asyncio.fixture(scope="session", loop_scope="session")
async def live_adapter(request: pytest.FixtureRequest):
    dwsim_worker_dll_path = request.getfixturevalue("dwsim_worker_dll_path")
    assert isinstance(dwsim_worker_dll_path, Path)

    adapter = build_dwsim_adapter(
        DwsimAdapterConfig(dwsim_worker_dll_path=str(dwsim_worker_dll_path))
    )
    yield adapter


async def test_property_package_inventory_is_aligned_across_canonical_worker_and_runtime(
    live_adapter,
) -> None:
    canonical_registry_model_ids = {
        descriptor.spec.model_id
        for descriptor in live_adapter._property_package_registry.list_registered_property_packages()
    }
    runtime_loaded_model_ids = {
        descriptor.spec.model_id for descriptor in await live_adapter.list_property_packages()
    }
    worker_supported_model_ids = set(DWSIM_WORKER_SUPPORTED_PROPERTY_PACKAGE_MODEL_IDS)

    assert canonical_registry_model_ids == worker_supported_model_ids, (
        "canonical registry vs worker mismatch: "
        f"canonical_only={sorted(canonical_registry_model_ids - worker_supported_model_ids)}, "
        f"worker_only={sorted(worker_supported_model_ids - canonical_registry_model_ids)}"
    )
    assert runtime_loaded_model_ids == worker_supported_model_ids, (
        "runtime vs worker mismatch: "
        f"runtime_only={sorted(runtime_loaded_model_ids - worker_supported_model_ids)}, "
        f"worker_only={sorted(worker_supported_model_ids - runtime_loaded_model_ids)}"
    )
