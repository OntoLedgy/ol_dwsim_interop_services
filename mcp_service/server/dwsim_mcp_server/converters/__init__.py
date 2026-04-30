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

"""Model converters between CAPE-OPEN, DWSIM, and DTOs."""

from .pythonnet_dto_converter import (
    from_csharp_material_stream,
    from_csharp_property_package,
    from_csharp_unit_operation,
    to_csharp_material_stream,
    to_csharp_property_package,
    to_csharp_unit_operation,
)

__all__ = [
    "from_csharp_material_stream",
    "from_csharp_property_package",
    "from_csharp_unit_operation",
    "to_csharp_material_stream",
    "to_csharp_property_package",
    "to_csharp_unit_operation",
]
