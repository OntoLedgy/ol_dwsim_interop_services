# Agent Guidelines for DWSIM Interop Services

This document provides guidelines for AI agents working on this codebase.

## Code Development Standards

All code contributions must follow the standards documented in `docs/standards/`. Review these thoroughly before making changes.

### Clean Coding Standards

Reference: [docs/standards/clean_coding/](../docs/standards/clean_coding/)

Key documents to follow:
- [clean_coding_standards.md](../docs/standards/clean_coding/clean_coding_standards.md) - Overview of all clean code principles
- [clean_coding_full_details.md](../docs/standards/clean_coding/clean_coding_full_details.md) - Comprehensive details
- [meaningful_names.md](../docs/standards/clean_coding/meaningful_names.md) - Naming conventions
- [functions.md](../docs/standards/clean_coding/functions.md) - Function design guidelines
- [classes.md](../docs/standards/clean_coding/classes.md) - Class design principles
- [error_handling.md](../docs/standards/clean_coding/error_handling.md) - Exception and error patterns
- [formatting.md](../docs/standards/clean_coding/formatting.md) - Code formatting rules
- [comments.md](../docs/standards/clean_coding/comments.md) - When and how to comment
- [objects_and_data_structures.md](../docs/standards/clean_coding/objects_and_data_structures.md) - Data modeling
- [boundaries.md](../docs/standards/clean_coding/boundaries.md) - Interface boundaries
- [concurrency.md](../docs/standards/clean_coding/concurrency.md) - Threading and async patterns
- [systems.md](../docs/standards/clean_coding/systems.md) - System-level design
- [emergence.md](../docs/standards/clean_coding/emergence.md) - Emergent design principles
- [smells_and_heuristics.md](../docs/standards/clean_coding/smells_and_heuristics.md) - Code smell detection

#### Summary of Key Principles
- Follow standard conventions and keep solutions simple
- Always leave code better than you found it (boy scout rule)
- Keep functions small and focused on a single task
- Use descriptive, searchable, and pronounceable names
- Prefer polymorphism to large if/else or switch statements
- Separate threading concerns from business logic
- Apply SOLID principles for maintainable code
- Use established design patterns (Factory, Strategy, Adapter, Repository, etc.)

### Testing Standards

Reference: [docs/standards/testing/](../docs/standards/testing/)

Key documents to follow:
- [TESTING_GUIDELINES.md](../docs/standards/testing/TESTING_GUIDELINES.md) - Comprehensive testing guidelines
- [TEST_QUALITY_REQUIREMENTS.md](../docs/standards/testing/TEST_QUALITY_REQUIREMENTS.md) - Quality requirements for tests
- [unit_tests.md](../docs/standards/testing/unit_tests.md) - Unit testing best practices
- [test_plan.md](../docs/standards/testing/test_plan.md) - Test planning documentation

#### Summary of Key Principles
- Tests should be fast, independent, and repeatable
- Maintain clear separation between unit tests and integration tests
- Use mocks for external services in unit tests
- Write tests that contain one assertion per test when practical
- Follow the Arrange-Act-Assert pattern

## Workflow Requirements

1. **Before coding**: Review relevant standards documents for the type of change
2. **During development**: Apply clean code principles consistently
3. **Before committing**: Ensure tests pass and code follows standards
4. **Commit messages**: Use conventional commit standard

## Task Management

Use the spec workflow MCP to track tasks in `tasks.md` files within spec folders.

## Repository Management

- Commit changes at the end of each completed task
- Use conventional commit standard for all commits
