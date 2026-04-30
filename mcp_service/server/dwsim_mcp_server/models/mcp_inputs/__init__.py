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

"""Package for MCP input/output models."""

from .flowsheet_build import (  # noqa: F401
    AddCompoundInput,
    AddCompoundOutput,
    AddStreamInput,
    AddStreamOutput,
    AddUnitInput,
    AddUnitOutput,
    COMPOSITION_TOLERANCE,
    ConnectionSummary,
    ConnectInput,
    ConnectOutput,
    DeleteObjectInput,
    DeleteObjectOutput,
    FlashStreamInput,
    FlashStreamOutput,
    GetStreamPropertiesInput,
    ListObjectsInput,
    ListObjectsOutput,
    SetBinaryInteractionParameterInput,
    SetBinaryInteractionParameterOutput,
    SetObjectParameterInput,
    SetObjectParameterOutput,
    SetPropertyPackageInput,
    SetPropertyPackageOutput,
    StreamSummary,
    SUPPORTED_PROPERTY_PACKAGES,
    SUPPORTED_UNIT_TYPES,
    UnitSummary,
)
from .flash_inputs import (  # noqa: F401
    FlashPHInputs,
    FlashPSInputs,
    FlashTPInputs,
)
from .export_inputs import (  # noqa: F401
    ExportCsvInput,
    ExportCsvOutput,
    ExportJsonInput,
    ExportJsonOutput,
    GenerateReportInput,
    GenerateReportOutput,
    SaveCaseInput,
    SaveCaseOutput,
)
from .compound_validation import (  # noqa: F401
    CompoundInfo,
    CompoundValidationResult,
    ListCompoundsInput,
    ListCompoundsOutput,
    ValidateCompoundsInput,
    ValidateCompoundsOutput,
)
from .sensitivity_inputs import (  # noqa: F401
    ConstraintSpec,
    ObjectiveSpec,
    OptimizationRequest,
    OutputSpec,
    ParameterSweepRequest,
    RangeSpec,
    SensitivityAnalysisRequest,
    SweepVariableSpec,
    VariableSpec,
    VariableWithBounds,
)
