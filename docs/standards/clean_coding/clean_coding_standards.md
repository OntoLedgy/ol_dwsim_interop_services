# Code Quality Guidelines

This file provides guidance for making code changes in this repository. All contributions should follow Clean Code principles inspired by Robert C. Martin and established best practices for service-oriented architectures.

## Clean Code Summary
- **General Rules**
  - Follow standard conventions and keep solutions simple.
  - Always leave code better than you found it (boy scout rule).
  - Identify and address root causes of problems.
- **Design Rules**
  - Keep configurable data at higher levels of your code.
  - Prefer polymorphism to large `if/else` or `switch` statements.
  - Separate threading concerns from business logic.
  - Prevent over-configurability and use dependency injection where possible.
  - Respect the Law of Demeter: classes should know only direct dependencies.
- **Understandability Tips**
  - Be consistent and use explanatory variables.
  - Encapsulate boundary conditions and avoid negative conditionals.
  - Use dedicated value objects rather than primitives when possible.
- **Naming**
  - Choose descriptive, searchable, and pronounceable names.
  - Replace magic numbers with named constants and avoid type-encoded names.
- **Functions**
  - Keep functions small and focused on a single task.
  - Use descriptive names and minimize argument counts.
  - Avoid side effects and flag arguments.
- **Comments**
  - Strive to make code self-explanatory; comments should clarify intent or warn about consequences.
  - Remove commented-out code.
- **Source Code Structure**
  - Separate distinct concepts vertically and keep related code together.
  - Declare variables near their usage and place dependent functions close to each other.
  - Favor short lines, avoid horizontal alignment, and use white space strategically.
- **Objects and Data Structures**
  - Hide internal structures and favor small, single-responsibility classes.
  - Prefer non-static methods and avoid hybrid objects/data structures.
- **Tests**
  - Tests should be fast, independent, repeatable, and typically contain one assertion each.
- **Code Smells**
  - Watch for rigidity, fragility, needless complexity/repetition, immobility, and opacity.

## Design Patterns and Best Practices
- Use established design patterns in all languages (including Python) when appropriate, e.g. Factory, Singleton, Strategy, Adapter, or Repository patterns.
- Apply SOLID principles to ensure well-structured, maintainable code.
- Favor dependency injection and interface-based programming to increase modularity.
- Keep services small and focused; avoid hard-coding dependencies inside modules.
- Document the responsibilities of each component and maintain clear boundaries between layers (e.g. services, data access, business logic, presentation).

## Code Formatting
- Maintain vertical formatting for readability. Group related lines into small, focused paragraphs separated by blank lines.
- Use spaces for indentation (no tabs) and avoid trailing whitespace.
- Keep lines under 120 characters when possible.

## General Workflow
1. Create or update unit tests in the `tests` folder alongside your code changes.
2. Run relevant tests with `pytest` (only the affected tests or the full suite if needed).
3. Keep commits focused and include clear messages describing the change.

