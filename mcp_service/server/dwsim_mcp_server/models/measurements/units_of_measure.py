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

"""Unit of measure for physical quantities."""

from typing import Optional

from pydantic import BaseModel

from dwsim_mcp_server.models.enums.physical_quantity_types import PhysicalQuantityTypes
from dwsim_mcp_server.models.measurements.ranges import Ranges


class UnitsOfMeasure(BaseModel):
    """Represents a unit of measure for a specific physical quantity."""

    unit_name: str
    quantity_type: PhysicalQuantityTypes
    valid_range: Optional[Ranges] = None

    def is_value_valid(self, value: float) -> bool:
        """Validate that a value is within the valid range for this unit."""
        if self.valid_range is None:
            return True
        return self.valid_range.contains(value)
