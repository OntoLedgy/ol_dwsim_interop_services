# Contributing to DWSIM MCP Server

Thank you for your interest in contributing to the DWSIM MCP Server project!

## Development Setup

### Prerequisites

- Python 3.10+
- .NET Framework 4.8 SDK (Windows) or .NET Core 6+ SDK
- DWSIM installation or source code
- Git

### Clone and Setup

```bash
# Clone the repository
git clone <repository-url>
cd dwsim_interop_services

# Install Python dependencies
cd mcp_service/server
pip install -r requirements.txt
pip install -e .

# Build C# worker
cd ../dwsim_worker
dotnet restore
dotnet build
```

## Code Standards

### Python Code

Follow PEP 8 and project-specific conventions defined in `.spec-workflow/steering/structure.md`:

- **Naming**: snake_case for files, PascalCase for classes, snake_case for functions
- **One file per class**: Each model/class in its own file
- **Type hints**: All public functions must have type annotations
- **Docstrings**: Google-style docstrings for all public APIs
- **Line length**: 100 characters maximum

**Format and lint before committing:**

```bash
black dwsim_mcp_server tests
ruff check dwsim_mcp_server tests
mypy dwsim_mcp_server
```

### C# Code

Follow Microsoft C# coding conventions and project structure guidelines:

- **Naming**: PascalCase for classes/methods, _camelCase for private fields
- **One file per class**: Each class in its own .cs file
- **XML docs**: All public APIs must have XML documentation comments
- **Namespaces**: `DwsimWorker.{Area}` pattern

### Commit Messages

Use Conventional Commits format:

```
feat: add flash_tp thermodynamic tool
fix: correct pressure unit conversion in material stream
docs: update API reference for session management
refactor: extract CAPE-OPEN converter to separate module
test: add integration tests for flowsheet building
```

## Testing

### Python Tests

```bash
cd mcp_service/server
pytest tests/ -v --cov=dwsim_mcp_server
```

### C# Tests

```bash
cd mcp_service/dwsim_worker
dotnet test
```

### Integration Tests

```bash
cd integration-tests
pytest test_scenarios.py
```

## Pull Request Process

1. **Create a feature branch**: `git checkout -b feature/your-feature-name`
2. **Make your changes**: Follow code standards and add tests
3. **Run all tests**: Ensure Python, C#, and integration tests pass
4. **Update documentation**: Add or update docs for new features
5. **Commit your changes**: Use conventional commit format
6. **Push to your fork**: `git push origin feature/your-feature-name`
7. **Open a pull request**: Provide clear description of changes

### PR Checklist

- [ ] Code follows project style guidelines
- [ ] All tests pass
- [ ] New tests added for new functionality
- [ ] Documentation updated (README, API docs, inline comments)
- [ ] Commit messages follow conventional format
- [ ] No breaking changes (or clearly documented if unavoidable)
- [ ] CHANGELOG.md updated

## Areas for Contribution

### High Priority

- **Core MCP Tools**: Implement tools defined in product.md
- **CAPE-OPEN Models**: Complete CAPE-OPEN interface implementations
- **Worker IPC**: JSON-RPC dispatcher and Named Pipe server
- **Session Management**: Multi-session isolation and lifecycle
- **Integration Tests**: End-to-end test scenarios

### Documentation

- API reference documentation
- User guides and tutorials
- Architecture diagrams
- Example notebooks

### Testing

- Unit test coverage improvements
- Integration test scenarios
- Golden case validation
- Performance benchmarks

## Questions or Issues?

- Open an issue for bugs or feature requests
- Start a discussion for questions or design proposals
- Contact maintainers at development@ontoledgy.ai

## License

By contributing, you agree that your contributions will be licensed under the AGPL-3.0-or-later License.
