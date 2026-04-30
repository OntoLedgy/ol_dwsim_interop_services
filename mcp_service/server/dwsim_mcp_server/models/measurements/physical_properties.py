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

"""Named physical property with measurement."""

from typing import Optional

from pydantic import BaseModel

from dwsim_mcp_server.models.measurements.measurements import Measurements


class PhysicalProperties(BaseModel):
    """Represents a named physical property with its measurement."""

    name: str
    measurement: Optional[Measurements] = None

    @property
    def value(self) -> Optional[float]:
        """Get the numeric value of this property's measurement."""
        if self.measurement is None:
            return None
        return self.measurement.value

    @property
    def unit_name(self) -> Optional[str]:
        """Get the unit name of this property's measurement."""
        if self.measurement is None:
            return None
        return self.measurement.unit.unit_name
