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

import pytest

from dwsim_mcp_server.models.mcp_inputs import (
    AddStreamInput,
    AddUnitInput,
    SetObjectParameterInput,
    SetPropertyPackageInput,
)


def test_add_stream_requires_flow_for_source():
    """Source streams (is_source=True) require either molar_flow or mass_flow."""
    with pytest.raises(ValueError):
        AddStreamInput(
            session_id="s1",
            name="feed",
            temperature=298.15,
            pressure=101325.0,
            composition={"methane": 0.5, "ethane": 0.5},
            is_source=True,  # Source streams require flow specification
        )


def test_add_stream_outlet_allows_no_flow():
    """Outlet streams (is_source=False) don't require flow - DWSIM calculates them."""
    # This should NOT raise - outlets can omit flow
    stream = AddStreamInput(
        session_id="s1",
        name="outlet",
        temperature=298.15,
        pressure=101325.0,
        composition={"methane": 0.5, "ethane": 0.5},
        is_source=False,
    )
    assert stream.molar_flow is None
    assert stream.mass_flow is None


def test_add_stream_composition_sum_validation():
    with pytest.raises(ValueError):
        AddStreamInput(
            session_id="s1",
            name="feed",
            temperature=298.15,
            pressure=101325.0,
            molar_flow=10.0,
            composition={"methane": 0.7, "ethane": 0.7},
        )


def test_add_unit_rejects_unsupported_unit_type():
    with pytest.raises(ValueError):
        AddUnitInput(
            session_id="s1",
            unit_type="distillation",
            name="col-01",
            parameters={},
        )


def test_set_property_package_requires_supported_name():
    with pytest.raises(ValueError):
        SetPropertyPackageInput(session_id="s1", package_name="foo", options={})


def test_set_object_parameter_value_must_be_json_friendly():
    class NotSerializable:
        pass

    with pytest.raises(ValueError):
        SetObjectParameterInput(
            session_id="s1",
            object_id="obj-1",
            parameter_name="p",
            value=NotSerializable(),
        )
