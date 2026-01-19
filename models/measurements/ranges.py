"""Range model for physical quantity measurements."""

from pydantic import BaseModel


class Ranges(BaseModel):
    """Represents a valid range for a physical quantity measurement."""

    min_value: float
    max_value: float

    def contains(self, value: float) -> bool:
        """Check if a value is within this range."""
        return self.min_value <= value <= self.max_value
