# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
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
