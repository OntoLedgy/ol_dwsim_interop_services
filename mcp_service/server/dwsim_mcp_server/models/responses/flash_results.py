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

"""Response models for flash calculation results."""

from typing import Dict, List, Optional

from pydantic import BaseModel, Field

from dwsim_mcp_server.models.enums.flash_calculation_types import FlashCalculationTypes
from dwsim_mcp_server.models.enums.phase_types import PhaseTypes
from dwsim_mcp_server.models.measurements.physical_properties import PhysicalProperties


class PhaseResults(BaseModel):
    """Results for a single phase in a flash calculation."""

    phase_type: PhaseTypes = Field(..., description="Phase identifier")
    fraction: float = Field(..., ge=0.0, le=1.0, description="Phase mole fraction")
    composition: Dict[str, float] = Field(..., description="Component mole fractions")
    properties: List[PhysicalProperties] = Field(
        default_factory=list,
        description="Physical properties for the phase",
    )


class FlashResults(BaseModel):
    """Complete results from a flash calculation."""

    calculation_type: FlashCalculationTypes = Field(
        ..., description="Flash calculation type (TP/PH/PS/etc.)"
    )
    temperature: PhysicalProperties = Field(..., description="Equilibrium temperature")
    pressure: PhysicalProperties = Field(..., description="Equilibrium pressure")
    converged: bool = Field(..., description="True if the flash calculation converged")
    phases: List[PhaseResults] = Field(
        default_factory=list,
        description="Per-phase results from the flash calculation",
    )
    message: Optional[str] = Field(None, description="Error or warning message")
    iteration_count: Optional[int] = Field(
        None, description="Number of iterations used by the solver"
    )
