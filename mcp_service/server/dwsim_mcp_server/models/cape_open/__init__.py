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

"""CAPE-OPEN standard interface models.

CAPE-OPEN provides standardized interfaces for process simulation interoperability.
These models implement CAPE-OPEN 1.0 and 1.1 specifications.
"""

from .material_stream import MaterialStream
from .thermo_property_package import ThermoPropertyPackage
from .unit_operation import UnitOperation

__all__ = ["MaterialStream", "ThermoPropertyPackage", "UnitOperation"]
