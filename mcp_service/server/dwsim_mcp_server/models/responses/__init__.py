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

"""Response DTO models for MCP tools."""

from .close_session_response import CloseSessionResponse
from .create_session_response import CreateSessionResponse
from .load_case_response import LoadCaseResponse
from .save_case_response import SaveCaseResponse
from .flash_results import FlashResults, PhaseResults
from .sensitivity_results import (
    OptimizationResult,
    ResultRow,
    SensitivityStudyResult,
    StudyStatus,
)
from .simulation_result_response import SimulationResultResponse
from .simulation_status_response import SimulationStatusResponse
from .stream_properties_response import StreamPropertiesResponse

__all__ = [
    "CloseSessionResponse",
    "CreateSessionResponse",
    "FlashResults",
    "LoadCaseResponse",
    "OptimizationResult",
    "PhaseResults",
    "ResultRow",
    "SaveCaseResponse",
    "SensitivityStudyResult",
    "SimulationResultResponse",
    "SimulationStatusResponse",
    "StudyStatus",
    "StreamPropertiesResponse",
]
