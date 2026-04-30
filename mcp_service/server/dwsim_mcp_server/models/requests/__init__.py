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

"""Request DTO models for MCP tools."""

from .add_stream_request import AddStreamRequest
from .add_unit_request import AddUnitRequest
from .close_session_request import CloseSessionRequest
from .connect_request import ConnectRequest
from .create_session_request import CreateSessionRequest
from .get_results_request import GetResultsRequest
from .get_status_request import GetStatusRequest
from .load_case_request import LoadCaseRequest
from .run_simulation_request import RunSimulationRequest
from .save_case_request import SaveCaseRequest

__all__ = [
    "AddStreamRequest",
    "AddUnitRequest",
    "CloseSessionRequest",
    "ConnectRequest",
    "CreateSessionRequest",
    "GetResultsRequest",
    "GetStatusRequest",
    "LoadCaseRequest",
    "RunSimulationRequest",
    "SaveCaseRequest",
]
