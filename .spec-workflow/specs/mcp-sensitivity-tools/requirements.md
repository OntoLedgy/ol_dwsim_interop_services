# Requirements Document

## Introduction

The `mcp-sensitivity-tools` specification defines MCP tools for automated parameter studies, sensitivity analysis, and optimization workflows. These tools enable LLM agents to systematically explore how simulation outputs respond to changes in input parameters, identify optimal operating conditions, and perform what-if analyses—all through composable MCP tool calls.

This capability is essential for chemical engineers using AI assistants to explore design spaces, validate operating envelopes, and optimize process performance without manual iteration through DWSIM's GUI.

## Alignment with Product Vision

This feature directly supports the product goals outlined in `product.md`:

- **Enable AI-Powered Chemical Engineering**: Sensitivity analysis and optimization are core engineering workflows; exposing them via MCP enables AI agents to perform sophisticated process analysis autonomously.
- **Composable Operations**: The tools break complex multi-run studies into discrete, chainable operations (define study → run → retrieve results).
- **Safe Execution**: Long-running studies require progress reporting, cancellation support, and graceful handling of partial failures.
- **Structured Data Exchange**: Results are returned in tabular formats suitable for LLM parsing, plotting, and further analysis.

## Requirements

### Requirement 1: Sensitivity Analysis Tool

**User Story:** As an LLM agent, I want to run a sensitivity analysis that varies a single parameter across a range and collects specified outputs, so that I can understand how the simulation responds to parameter changes.

#### Acceptance Criteria

1. WHEN the `sensitivity_analysis` tool is called with a valid `session_id`, `variable` specification, `range` (min/max), `steps` count, and list of `outputs` THEN the system SHALL execute the simulation `steps` times with the variable set to evenly-spaced values within the range.
2. WHEN each simulation step completes THEN the system SHALL record the input parameter value and all requested output values in a structured result row.
3. IF a simulation step fails to converge THEN the system SHALL record the failure in the results table with a `null` or error marker for outputs, and SHALL continue with remaining steps.
4. WHEN all steps complete THEN the system SHALL return a structured table (list of rows) with columns for input value and each output value.
5. WHEN the study is in progress THEN the system SHALL support progress reporting indicating completed steps out of total steps.

### Requirement 2: Multi-Variable Sensitivity (Parameter Sweep)

**User Story:** As an LLM agent, I want to sweep multiple parameters simultaneously in a grid or list pattern, so that I can explore multi-dimensional design spaces.

#### Acceptance Criteria

1. WHEN the `parameter_sweep` tool is called with multiple `variables`, each with its own range and steps THEN the system SHALL generate a grid of all parameter combinations (Cartesian product).
2. IF a `combinations` list is provided instead of ranges THEN the system SHALL use the explicit list of parameter sets.
3. WHEN the sweep completes THEN the system SHALL return results for each combination, including all input values and requested outputs.
4. IF the total number of combinations exceeds a configurable limit (default: 100) THEN the system SHALL return an error before execution, suggesting the user reduce the study size.

### Requirement 3: Optimization Tool

**User Story:** As an LLM agent, I want to find optimal values for one or more variables that minimize or maximize an objective function subject to constraints, so that I can identify the best operating conditions.

#### Acceptance Criteria

1. WHEN the `optimize` tool is called with a valid `session_id`, `objective` (object ID and property to optimize), `direction` (minimize/maximize), `variables` (with bounds), and optional `constraints` THEN the system SHALL invoke an optimization algorithm.
2. WHEN optimization converges THEN the system SHALL return the optimal variable values, the objective value at the optimum, and convergence status.
3. IF optimization fails to converge within the iteration limit THEN the system SHALL return the best solution found with a warning message.
4. WHEN constraints are specified THEN the system SHALL enforce them during optimization (e.g., output property must be ≥ threshold).

### Requirement 4: Progress Reporting and Cancellation

**User Story:** As an LLM agent, I want to monitor the progress of long-running studies and cancel them if needed, so that I can manage compute resources effectively.

#### Acceptance Criteria

1. WHEN a sensitivity analysis or optimization is running THEN the system SHALL provide a `get_study_status` tool returning current progress (steps completed, estimated time remaining).
2. WHEN the `cancel_study` tool is called with a valid `study_id` THEN the system SHALL abort the running study and return partial results collected so far.
3. IF a study is cancelled THEN the returned results SHALL include a `cancelled: true` flag and all completed data points.

### Requirement 5: Result Export

**User Story:** As an LLM agent, I want to export sensitivity analysis and optimization results to CSV or JSON files, so that I can share results or perform further analysis.

#### Acceptance Criteria

1. WHEN the `export_study_results` tool is called with a valid `study_id` and `file_path` THEN the system SHALL write results to the specified file in CSV or JSON format based on file extension.
2. IF the file path is outside allowed directories THEN the system SHALL return a validation error.
3. WHEN exporting to CSV THEN the system SHALL include a header row with column names.

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**: Separate modules for study orchestration (`sensitivity_service.py`), optimization engine wrapper, and result formatting.
- **Modular Design**: Reuse existing `run` simulation tool internally; sensitivity/optimization tools compose over it.
- **Dependency Management**: Minimize coupling; study service depends only on session client and simulation runner.
- **Clear Interfaces**: Define `SensitivityStudyRequest`, `SensitivityStudyResult`, `OptimizationRequest`, `OptimizationResult` Pydantic models.

### Performance

- Single-variable sensitivity with 10 steps: complete within 30 seconds for simple flowsheets.
- Progress updates emitted at least every 5 seconds during long-running studies.
- Memory usage per study: incremental (store only result rows, not full simulation state per step).

### Security

- File path validation for result export (reuse existing `resolve_case_path` utility).
- Study size limits enforced to prevent resource exhaustion.
- Timeout enforcement inherited from session resource limits.

### Reliability

- Partial results preserved on failure or cancellation.
- Failed simulation steps do not abort entire study; continue with best-effort collection.
- Study state survives transient errors in individual simulation runs.

### Usability

- Tool descriptions clearly explain input schema and expected behavior for LLM agents.
- Error messages include actionable suggestions (e.g., "Reduce steps to ≤ 100" or "Check variable name exists in flowsheet").
- Results structured for easy plotting (input column followed by output columns).
