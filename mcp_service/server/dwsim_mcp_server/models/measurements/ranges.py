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

"""Range model for physical quantity measurements."""

from pydantic import BaseModel


class Ranges(BaseModel):
    """Represents a valid range for a physical quantity measurement."""

    min_value: float
    max_value: float

    def contains(self, value: float) -> bool:
        """Check if a value is within this range."""
        return self.min_value <= value <= self.max_value
