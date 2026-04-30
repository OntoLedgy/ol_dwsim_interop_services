# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Measurement combining value, quantity, and unit."""

from pydantic import BaseModel, ValidationInfo, field_validator

from dwsim_mcp_server.models.enums.physical_quantity_types import PhysicalQuantityTypes
from dwsim_mcp_server.models.measurements.units_of_measure import UnitsOfMeasure


class Measurements(BaseModel):
    """Represents a measurement of a physical quantity using a specific unit."""

    quantity_type: PhysicalQuantityTypes
    value: float
    unit: UnitsOfMeasure

    @field_validator("unit")
    @classmethod
    def validate_unit_matches_quantity(
        cls,
        unit: UnitsOfMeasure,
        info: ValidationInfo,
    ) -> UnitsOfMeasure:
        """Ensure unit's quantity type matches the measurement's quantity type."""
        if "quantity_type" in info.data:
            quantity_type = info.data["quantity_type"]
            if unit.quantity_type != quantity_type:
                raise ValueError(
                    "Unit type mismatch: Unit "
                    f"'{unit.unit_name}' is for {unit.quantity_type}, "
                    f"but quantity is {quantity_type}"
                )
        return unit
