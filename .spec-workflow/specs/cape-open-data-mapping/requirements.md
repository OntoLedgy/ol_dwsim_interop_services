# Requirements Document

## Introduction

This spec defines the CAPE-OPEN data model mapping layer for the .NET worker. It introduces DTOs and converters that map DWSIM CAPE-OPEN interfaces to JSON-serializable structures and back, enabling consistent data exchange ahead of RPC serialization.

## Alignment with Product Vision

This feature implements the product's "Structured Data Exchange" and "CAPE-OPEN as Domain Model" principles by standardizing how material streams, property packages, unit operations, and flash results are represented and exchanged across process boundaries.

## Requirements

### Requirement 1: CAPE-OPEN DTO Definitions

**User Story:** As a developer integrating DWSIM with external systems, I want standardized DTOs for CAPE-OPEN concepts so that data can be exchanged consistently and validated.

#### Acceptance Criteria

1. WHEN a CAPE-OPEN DTO is created THEN it SHALL define explicit fields for identifiers, names, and core CAPE-OPEN properties.
2. WHEN a DTO represents a material stream THEN it SHALL include phases, composition, and thermodynamic state fields.
3. WHEN a DTO represents a flash result THEN it SHALL include phase fractions, phase compositions, and key properties.

### Requirement 2: Bidirectional Conversion

**User Story:** As a developer, I want to convert between DWSIM CAPE-OPEN objects and DTOs so that state can be extracted and restored for serialization and testing.

#### Acceptance Criteria

1. WHEN converting from DWSIM to DTO THEN the system SHALL use CAPE-OPEN interface methods (e.g., GetProp/SetProp) as the source of truth.
2. WHEN converting from DTO to DWSIM THEN the system SHALL apply DTO values to CAPE-OPEN interfaces and update object state.
3. IF a property is unsupported or unavailable THEN the system SHALL return a clear, actionable error message indicating the property name and context.

### Requirement 3: CAPE-OPEN Property Name Mapping

**User Story:** As a developer, I want a canonical mapping of CAPE-OPEN property names so that conversions are consistent and portable across simulators.

#### Acceptance Criteria

1. WHEN a property is referenced THEN the system SHALL use a centralized mapping of CAPE-OPEN standard property names.
2. WHEN adding new properties THEN the system SHALL update the centralized mapping without scattering string literals across code.
3. WHEN an unknown property is requested THEN the system SHALL report it as unsupported with guidance for valid names.

### Requirement 4: Unit Handling and SI Normalization

**User Story:** As a developer, I want all DTO values normalized to SI units so that downstream systems can assume consistent units.

#### Acceptance Criteria

1. WHEN extracting values from DWSIM THEN the system SHALL normalize to SI units based on CAPE-OPEN unit metadata.
2. WHEN ingesting DTO values THEN the system SHALL accept only SI units or apply a validated conversion to SI.
3. IF a unit conversion is undefined THEN the system SHALL fail with a descriptive unit error.

### Requirement 5: JSON Serialization Compatibility

**User Story:** As a developer, I want DTOs to be JSON-serializable so they can be transmitted via JSON-RPC and stored for tests.

#### Acceptance Criteria

1. WHEN serializing DTOs with Newtonsoft.Json THEN the output SHALL include all required fields with stable naming.
2. WHEN deserializing DTOs THEN missing required fields SHALL produce validation errors.
3. WHEN round-tripping DTOs (serialize -> deserialize) THEN values SHALL remain equivalent within numeric tolerance.

### Requirement 6: Validation and Error Handling

**User Story:** As a developer, I want DTO validation so that invalid data is caught before applying it to DWSIM.

#### Acceptance Criteria

1. WHEN a DTO is validated THEN required fields SHALL be enforced and numeric ranges SHALL be checked.
2. IF composition values are provided THEN the system SHALL validate they sum to 1.0 within a tolerance.
3. WHEN validation fails THEN the system SHALL return structured, user-readable errors.

## Non-Functional Requirements

### Code Architecture and Modularity
- **Single Responsibility Principle**: DTOs, converters, unit conversion, and validation must be isolated into separate files.
- **Modular Design**: Mapping logic should be reusable by future JSON-RPC handlers and tests.
- **Dependency Management**: Converters should depend on CAPE-OPEN interfaces and DTOs only, not higher-level services.
- **Clear Interfaces**: Conversion methods must have explicit inputs/outputs and avoid global state.

### Performance
- DTO extraction and mapping SHOULD complete within 50 ms for a typical material stream.

### Security
- Conversion logic MUST NOT perform network I/O or file system access beyond in-memory conversion.

### Reliability
- Conversion methods MUST be deterministic for identical inputs and produce stable results across runs.

### Usability
- Error messages MUST include the CAPE-OPEN property name, unit (if relevant), and object context.
