# Requirements Document: MCP Resource Providers

## Introduction

This specification defines the implementation of MCP resource providers for the DWSIM MCP Server. MCP resources enable agents to access data beyond tool responses, including documentation, sample simulation cases, and large result sets. Resources provide a standardized way for LLM agents to retrieve contextual information needed for effective chemical process simulation workflows.

The resource providers will expose three primary resource categories:
1. **Session Results**: Detailed simulation results too large for tool responses
2. **Documentation**: DWSIM documentation, unit operation guides, and thermodynamic model references
3. **Sample Cases**: Pre-built flowsheets demonstrating common configurations (three-phase separator, distillation, etc.)

## Alignment with Product Vision

This feature directly supports the product vision outlined in product.md:

- **Enable AI-Powered Chemical Engineering**: Resources provide agents with documentation and examples needed to make informed decisions about simulation configuration
- **Resource Providers** are explicitly listed as a key feature: `resource://session/{id}/results/{path}`, `resource://docs/{topic}`, `resource://cases/{name}`
- **Composability**: Resources complement tools by providing large datasets and reference materials that would clutter tool responses
- **Developer Adoption**: Well-documented resources help developers and agents understand DWSIM capabilities

## Requirements

### Requirement 1: Session Results Resource Provider

**User Story:** As an LLM agent, I want to retrieve detailed simulation results for a session, so that I can analyze comprehensive property data without the limitations of tool response sizes.

#### Acceptance Criteria

1. WHEN a client requests `resource://session/{sessionId}/results` THEN the system SHALL return a JSON representation of all simulation objects and their properties
2. WHEN a client requests `resource://session/{sessionId}/results/{objectId}` THEN the system SHALL return detailed properties for that specific stream or unit operation
3. IF the sessionId does not exist THEN the system SHALL return an appropriate MCP error with code `NotFound`
4. IF the objectId does not exist within the session THEN the system SHALL return an MCP error with code `NotFound` and a descriptive message
5. WHEN results exceed 100KB THEN the system SHALL support pagination or streaming to handle large datasets efficiently
6. WHEN requesting results THEN the system SHALL include CAPE-OPEN standard property names and SI units

### Requirement 2: Documentation Resource Provider

**User Story:** As an LLM agent, I want to access DWSIM documentation and reference materials, so that I can provide accurate guidance on simulation configuration and troubleshooting.

#### Acceptance Criteria

1. WHEN a client requests `resource://docs` THEN the system SHALL return a list of available documentation topics
2. WHEN a client requests `resource://docs/{topic}` THEN the system SHALL return markdown-formatted documentation for that topic
3. WHEN a client requests `resource://docs/unit-operations` THEN the system SHALL return documentation about available unit operations and their parameters
4. WHEN a client requests `resource://docs/property-packages` THEN the system SHALL return documentation about thermodynamic property packages and their applicability
5. WHEN a client requests `resource://docs/compounds` THEN the system SHALL return information about the compound database and how to query it
6. IF a documentation topic does not exist THEN the system SHALL return an MCP error with code `NotFound` and suggest similar topics
7. WHEN documentation is returned THEN the system SHALL format it in Markdown suitable for LLM consumption

### Requirement 3: Sample Cases Resource Provider

**User Story:** As an LLM agent, I want to access sample simulation cases, so that I can reference working examples when building new flowsheets or helping users understand DWSIM capabilities.

#### Acceptance Criteria

1. WHEN a client requests `resource://cases` THEN the system SHALL return a list of available sample cases with descriptions
2. WHEN a client requests `resource://cases/{caseName}` THEN the system SHALL return metadata and configuration details for that sample case
3. WHEN a client requests `resource://cases/{caseName}/flowsheet` THEN the system SHALL return the flowsheet topology (streams, units, connections)
4. WHEN a sample case is available THEN it SHALL be loadable via the `load_case` MCP tool using the case name
5. IF a case name does not exist THEN the system SHALL return an MCP error with code `NotFound` and list available cases
6. WHEN listing cases THEN the system SHALL include: case name, description, compounds used, unit operations, and complexity level

### Requirement 4: Resource Discovery and Listing

**User Story:** As an LLM agent, I want to discover available resources through MCP's standard resource listing, so that I can explore what data is available.

#### Acceptance Criteria

1. WHEN an MCP client calls `list_resources` THEN the system SHALL return all available resource URIs with descriptions
2. WHEN listing resources THEN the system SHALL group resources by category (session, docs, cases)
3. WHEN listing session resources THEN the system SHALL only include active sessions
4. WHEN a session is closed THEN its resources SHALL no longer appear in resource listings
5. WHEN resources are listed THEN each resource SHALL include: URI, name, description, and MIME type

### Requirement 5: Resource Content Formatting

**User Story:** As an LLM agent, I want resources to be formatted appropriately for my context window, so that I can effectively process the information.

#### Acceptance Criteria

1. WHEN returning JSON resources THEN the system SHALL use properly indented, human-readable JSON
2. WHEN returning documentation THEN the system SHALL use well-structured Markdown with headings, code blocks, and tables
3. WHEN returning large result sets THEN the system SHALL provide summary information with options to request detailed subsections
4. WHEN property data includes units THEN the system SHALL clearly label units using SI standard notation
5. WHEN returning tabular data THEN the system SHALL format it as Markdown tables or structured JSON arrays

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**: Each resource provider (docs, samples, results) SHALL be implemented in a separate module under `mcp_service/server/dwsim_mcp_server/resources/`
- **Modular Design**: Resource providers SHALL share a common base class or protocol defining the resource interface
- **Dependency Injection**: Resource providers SHALL receive dependencies (session client, settings) through constructor injection
- **Clear Interfaces**: Each provider SHALL implement the MCP resource protocol with typed methods

### Performance

- Resource listing SHALL complete within 100ms for typical workloads
- Individual resource retrieval SHALL complete within 500ms for resources under 100KB
- Large result sets (>100KB) SHALL support efficient retrieval through pagination or streaming
- Documentation resources SHALL be cached in memory after first access

### Security

- Session result resources SHALL only be accessible to the session owner (validated by sessionId)
- File path parameters SHALL be validated against directory traversal attacks
- Sample case loading SHALL only access files within configured case directories
- Resource URIs SHALL be validated against injection attacks

### Reliability

- Resource providers SHALL gracefully handle missing or corrupted data files
- Session result resources SHALL return meaningful errors if simulation hasn't been run
- Documentation resources SHALL have fallback content if specific topics are unavailable
- Resource provider initialization failures SHALL not prevent server startup

### Observability

- All resource requests SHALL be logged with correlation IDs (sessionId, resourceUri)
- Resource access latency SHALL be recorded as metrics
- Failed resource requests SHALL include diagnostic information in logs
- Resource provider startup and initialization SHALL be logged

