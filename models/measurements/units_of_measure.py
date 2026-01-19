"""Unit of measure for physical quantities."""

from typing import Optional

from pydantic import BaseModel

from models.enums.physical_quantity_types import PhysicalQuantityTypes
from models.measurements.ranges import Ranges


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
