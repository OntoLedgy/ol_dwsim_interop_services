# Requirements Document: MCP Export Tools and LLM Usability Improvements

## Introduction

This specification defines the requirements for export and reporting tools in the DWSIM MCP Server, along with critical LLM usability improvements identified during real-world agent testing. The feature set enables LLM agents to export simulation results, generate reports, and save simulation cases. Additionally, it addresses several friction points discovered when using the MCP tools with AI agents, including compound validation, outlet stream handling, and documentation improvements.

The scope covers:
1. **Export Tools**: CSV and JSON export of simulation results
2. **Report Generation**: Human-readable reports in Markdown/HTML format
3. **Save Functionality**: Fix and enhance case saving to DWSIM native formats
4. **LLM Usability**: Compound validation, auto-composition for outlet streams, and improved documentation

## Alignment with Product Vision

This feature directly supports the DWSIM MCP Server's core mission of making professional process simulation accessible to LLM agents. As stated in product.md:

- **Composable Operations**: Export tools enable agents to persist and share simulation results
- **Observable by Default**: Reports provide structured output for analysis and debugging
- **AI-Native Interface**: LLM usability improvements reduce friction and errors during agent workflows
- **Safety First**: File path sandboxing ensures secure export operations

The LLM usability improvements address critical gaps identified during real-world testing (Scenario A - Light Sweet Crude separation), where agents encountered issues with compound naming, outlet stream composition requirements, and missing save functionality.

## Requirements

### REQ-1: CSV Export Tool

**User Story:** As an LLM agent, I want to export simulation results to CSV format, so that I can share data with external analysis tools like Excel or pandas.

#### Acceptance Criteria

1. WHEN the agent calls `export_csv` with a valid `sessionId` and `filePath` THEN the system SHALL export all stream and unit operation properties to a CSV file at the specified path.

2. IF the agent provides `objectIds` parameter THEN the system SHALL export only the properties of the specified objects.

3. WHEN the `filePath` attempts directory traversal (e.g., `../../../sensitive.csv`) THEN the system SHALL reject the request with a clear error message indicating path violation.

4. WHEN the export completes successfully THEN the system SHALL return the absolute file path of the created CSV file.

5. IF the session does not exist THEN the system SHALL return a `NotFound` error with message "Session not found: {sessionId}".

### REQ-2: JSON Export Tool

**User Story:** As an LLM agent, I want to export the flowsheet state to JSON format, so that I can programmatically analyze or transfer simulation configurations.

#### Acceptance Criteria

1. WHEN the agent calls `export_json` with a valid `sessionId` THEN the system SHALL return a JSON representation of the complete flowsheet state.

2. IF the agent specifies `format: "summary"` THEN the system SHALL return a condensed JSON with key properties only (stream flows, temperatures, pressures, compositions).

3. IF the agent specifies `format: "full"` THEN the system SHALL return complete JSON including all CAPE-OPEN properties, unit operation parameters, and convergence state.

4. WHEN the JSON export completes THEN the system SHALL include metadata: session ID, export timestamp, DWSIM version, and property package name.

5. IF the simulation has not been run THEN the system SHALL include a warning in the response indicating results may be incomplete.

### REQ-3: Report Generation Tool

**User Story:** As an LLM agent, I want to generate human-readable reports from simulation results, so that I can present findings to users or publish to documentation systems.

#### Acceptance Criteria

1. WHEN the agent calls `generate_report` with a valid `sessionId` THEN the system SHALL generate a Markdown report containing stream tables, unit operation summaries, and convergence status.

2. IF the agent specifies `template: "html"` THEN the system SHALL generate an HTML report with styled tables and headings.

3. WHEN generating a report THEN the system SHALL include:
   - Simulation metadata (session ID, timestamp, property package)
   - Feed stream specifications (T, P, composition, flow)
   - Product stream results (all phases with compositions)
   - Mass and energy balance summary
   - Convergence status and any warnings

4. IF `filePath` is provided THEN the system SHALL write the report to the specified file and return the path.

5. IF `filePath` is not provided THEN the system SHALL return the report content inline in the response.

### REQ-4: Save Case Functionality (Fix Required)

**User Story:** As an LLM agent, I want to save the current simulation to a DWSIM file, so that I can preserve my work and reload it later.

#### Acceptance Criteria

1. WHEN the agent calls `save_case` with a valid `sessionId` and `filePath` ending in `.dwxmz` THEN the system SHALL save the flowsheet to DWSIM compressed XML format.

2. WHEN the agent calls `save_case` with `filePath` ending in `.dwxml` THEN the system SHALL save the flowsheet to DWSIM uncompressed XML format.

3. WHEN the save operation completes successfully THEN the system SHALL return the absolute file path of the saved case.

4. IF the save operation fails THEN the system SHALL return an actionable error message explaining the cause (e.g., "Flowsheet not initialized", "Invalid file path").

5. WHEN the file already exists at the path THEN the system SHALL overwrite it (no confirmation required for agent workflows).

### REQ-5: Compound Validation Tool

**User Story:** As an LLM agent, I want to validate compound names before adding them to a session, so that I can avoid errors and receive suggestions for correct names.

#### Acceptance Criteria

1. WHEN the agent calls `validate_compounds` with a list of compound names THEN the system SHALL return validation status for each compound (valid/invalid).

2. IF a compound name is invalid THEN the system SHALL return up to 5 similar compound names from the DWSIM database (fuzzy matching).

3. WHEN validating compounds THEN the system SHALL perform case-insensitive matching (e.g., "methane", "Methane", "METHANE" all match).

4. IF the agent provides common aliases (e.g., "isobutane", "i-C4", "iC4", "2-methylpropane") THEN the system SHALL map them to the correct DWSIM compound name and indicate the mapping.

5. WHEN called with an empty list THEN the system SHALL return an error indicating at least one compound is required.

### REQ-6: List Available Compounds Tool

**User Story:** As an LLM agent, I want to list available compounds in the DWSIM database, so that I can discover correct compound names.

#### Acceptance Criteria

1. WHEN the agent calls `list_available_compounds` without parameters THEN the system SHALL return a paginated list of all compound names (first 100 by default).

2. IF the agent provides a `pattern` parameter THEN the system SHALL return compounds matching the pattern (e.g., "meth*" returns Methane, Methanol, etc.).

3. IF the agent provides a `category` parameter (e.g., "alkanes", "aromatics") THEN the system SHALL filter compounds by category.

4. WHEN returning compound names THEN the system SHALL include: name, formula, molecular weight, and CAS number (if available).

5. IF no compounds match the pattern THEN the system SHALL return an empty list with a message suggesting alternative search terms.

### REQ-7: Auto-Composition for Outlet Streams

**User Story:** As an LLM agent, I want outlet streams to be created without requiring explicit composition, so that I can focus on flowsheet structure rather than placeholder values.

#### Acceptance Criteria

1. WHEN the agent calls `add_stream` with `is_source: false` and no `composition` parameter THEN the system SHALL automatically generate a placeholder composition using all compounds in the session.

2. WHEN auto-generating composition THEN the system SHALL use equal mole fractions for all session compounds (e.g., 3 compounds = 0.333 each).

3. IF the agent provides explicit composition for an outlet stream THEN the system SHALL use the provided values (override auto-generation).

4. WHEN an outlet stream is connected to a unit operation and the simulation runs THEN the system SHALL calculate actual composition from the unit operation (overwriting placeholder values).

5. IF no compounds have been added to the session THEN the system SHALL return an error: "Cannot create outlet stream: no compounds in session. Add compounds first using add_compound."

### REQ-8: Case-Insensitive Compound Matching

**User Story:** As an LLM agent, I want compound name matching to be case-insensitive, so that I don't have to remember exact capitalization.

#### Acceptance Criteria

1. WHEN the agent calls `add_compound` with a compound name in any case THEN the system SHALL match against the DWSIM database case-insensitively.

2. IF multiple compounds match case-insensitively (rare) THEN the system SHALL return the exact match if one exists, otherwise return the first match alphabetically.

3. WHEN a compound is added successfully THEN the response SHALL include the canonical DWSIM compound name for reference.

4. IF the compound is not found (case-insensitive) THEN the system SHALL suggest similar compound names as per REQ-5.

### REQ-9: Common Compound Alias Support

**User Story:** As an LLM agent, I want to use common compound aliases, so that I can use familiar names without looking up DWSIM's exact naming.

#### Acceptance Criteria

1. WHEN the agent uses common aliases THEN the system SHALL map them to DWSIM compound names:
   - "isobutane", "i-butane", "i-C4", "iC4", "2-methylpropane" → "Isobutane"
   - "isopentane", "i-pentane", "i-C5", "iC5", "2-methylbutane" → "Isopentane"
   - "CO2", "carbon dioxide" → "Carbon Dioxide"
   - "N2", "nitrogen" → "Nitrogen"
   - "H2O", "water" → "Water"
   - "H2S", "hydrogen sulfide" → "Hydrogen Sulfide"

2. WHEN an alias is used THEN the response SHALL indicate the mapping: "Alias 'i-C4' mapped to 'Isobutane'".

3. IF an alias could map to multiple compounds THEN the system SHALL return an error requesting clarification.

4. WHEN adding the alias mapping THEN the system SHALL log the alias → name mapping for debugging.

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**: Each export tool (CSV, JSON, report) should be a separate module/class
- **Modular Design**: Compound validation and alias mapping should be reusable utility functions
- **Dependency Management**: Export tools should depend only on session state and DWSIM adapters
- **Clear Interfaces**: Export functions should have typed Pydantic input/output models

### Performance

- **CSV Export**: SHALL complete in under 5 seconds for flowsheets with up to 50 streams
- **JSON Export**: SHALL complete in under 2 seconds for any flowsheet size
- **Report Generation**: SHALL complete in under 10 seconds including all formatting
- **Compound Validation**: SHALL complete in under 500ms for up to 20 compounds
- **List Compounds**: SHALL return first page in under 1 second

### Security

- **Path Sandboxing**: All file operations SHALL be restricted to configured allowed directories
- **No Directory Traversal**: File paths SHALL be validated to prevent `../` attacks
- **Content Sanitization**: Report content SHALL be sanitized to prevent injection attacks if rendered as HTML

### Reliability

- **Graceful Degradation**: If a single stream export fails, the system SHALL continue with other streams and report partial results
- **Error Recovery**: Failed compound validation SHALL not affect subsequent operations
- **Idempotency**: Multiple calls with same parameters SHALL produce identical results

### Usability

- **Clear Error Messages**: All errors SHALL include actionable guidance for the LLM agent
- **Consistent Naming**: Tool names and parameter names SHALL follow MCP conventions
- **Self-Documenting**: Tool descriptions SHALL be sufficient for LLM agents to use correctly without external documentation
- **Progress Indication**: Long-running exports SHALL provide progress updates if possible
