# AI Services Testing Guidelines

## Overview
This document provides comprehensive guidelines for developing and maintaining tests in the AI Services project. The testing framework uses pytest with a clear separation between lightweight unit tests and heavy integration tests.

## Test Structure

### Directory Organization
```
tests/
├── unit_tests/           # Lightweight, fast-running unit tests
│   ├── agents/
│   ├── configurations/
│   ├── embeddings/
│   ├── endpoints/
│   ├── graph_rag/
│   ├── model_management/
│   ├── text_extraction/
│   ├── tokenisation/
│   ├── tools/
│   └── training/
├── integration_tests/    # Tests requiring external services
├── fixtures/            # Shared test fixtures
├── data/               # Test data files
│   ├── inputs/         # Input test data
│   ├── outputs/        # Expected outputs
│   └── expected/       # Expected results
├── configurations/     # Test configuration files
└── conftest.py        # Pytest configuration

```

## Test Categories

### 1. Lightweight Tests (Default)
- **Marker**: No special marker needed
- **Characteristics**:
  - Fast execution (<1 second per test)
  - No external dependencies
  - Use mocks for external services
  - Run in CI/CD pipeline

### 2. Heavy Tests
- **Marker**: `@pytest.mark.heavy`
- **Characteristics**:
  - Require external services (OpenAI, Neo4j, PostgreSQL)
  - Large model downloads
  - Long execution times
  - Run locally only

## Writing Tests

### Test Class Structure
```python
class TestYourComponent:
    """Test suite for YourComponent functionality."""
    
    @pytest.fixture(autouse=True)
    def setup_method(self, input_folder_fixture):
        """Setup test data before each test."""
        self.test_file = os.path.join(input_folder_fixture, "test.pdf")
    
    def test_basic_functionality(self):
        """Test basic component functionality."""
        # Arrange
        component = YourComponent()
        
        # Act
        result = component.process(self.test_file)
        
        # Assert
        assert result is not None
```

### Using Fixtures

#### Path Fixtures (Session Scope)
```python
def test_with_paths(inputs_folder_absolute_path, outputs_folder_absolute_path):
    """Use predefined path fixtures."""
    input_file = os.path.join(inputs_folder_absolute_path, "data.csv")
    output_file = os.path.join(outputs_folder_absolute_path, "results.json")
```

#### Configuration Fixtures
```python
def test_with_config(open_ai_configuration, test_data_configuration):
    """Use configuration fixtures."""
    api_key = open_ai_configuration.get_config(
        section_name="open_ai_configuration",
        key="api_key"
    )
```

#### Mock Fixtures
```python
@pytest.fixture
def mock_service():
    """Create a mock service."""
    service = MagicMock()
    service.process.return_value = {"status": "success"}
    return service

def test_with_mock(mock_service):
    """Test using mock service."""
    result = mock_service.process()
    assert result["status"] == "success"
```

### Async Testing
```python
@pytest.mark.anyio
async def test_async_function():
    """Test async functionality."""
    result = await async_function()
    assert result is not None
```

### Integration Testing Pattern
```python
@pytest.mark.integration
@pytest.mark.heavy
class TestIntegration:
    """Integration test requiring external services."""
    
    @pytest.mark.skipif(
        not os.environ.get("OPENAI_API_KEY"),
        reason="OpenAI API key not available"
    )
    async def test_with_openai(self):
        """Test requiring OpenAI API."""
        # Test implementation
```

## Running Tests

### IMPORTANT: Setting PYTHONPATH
**The PYTHONPATH must include the tests folder for fixtures to load correctly:**

```bash
# Linux/Mac
export PYTHONPATH="${PYTHONPATH}:$(pwd)/tests"

# Windows Command Prompt
set PYTHONPATH=%PYTHONPATH%;%cd%\tests

# Windows PowerShell
$env:PYTHONPATH = "$env:PYTHONPATH;$(pwd)\tests"

# Or run pytest with PYTHONPATH set inline
PYTHONPATH=tests pytest tests/unit_tests
```

**Note**: Many test failures are caused by missing PYTHONPATH configuration. Always ensure the tests folder is in your PYTHONPATH before running tests, otherwise fixtures from `tests/fixtures/` will not be found and imports like `from tests.fixtures.configurations import *` will fail.

### Command Line Usage
```bash
# Run all lightweight tests (with PYTHONPATH set)
PYTHONPATH=tests pytest tests/unit_tests

# Run heavy tests only
PYTHONPATH=tests pytest -m heavy

# Run specific test file
PYTHONPATH=tests pytest tests/unit_tests/text_extraction/test_document_parser.py

# Run with coverage
PYTHONPATH=tests pytest --cov=ol_ai_services tests/

# Run with verbose output
PYTHONPATH=tests pytest -v tests/
```

### CI/CD Configuration
The GitHub Actions workflow automatically:
1. Runs lightweight unit tests only
2. Uses Python 3.11 and 3.12
3. Performs linting with ruff
4. Checks spelling with codespell

## Best Practices

### 1. Test Isolation
- Each test should be independent
- Use fixtures for shared setup
- Clean up resources in teardown

### 2. Mocking External Dependencies
```python
from unittest.mock import patch, MagicMock

@patch('ol_ai_services.llms.clients.open_ai_clients.OpenAiClients')
def test_with_mock_openai(mock_client):
    mock_client.return_value.generate.return_value = "mocked response"
```

### 3. Test Data Management
- Store test data in `tests/data/`
- Use small, representative samples
- Version control test data files

### 4. Assertion Guidelines
```python
# Be specific with assertions
assert result.status == "success"  # Good
assert result  # Too vague

# Use meaningful assertion messages
assert len(results) == 3, f"Expected 3 results, got {len(results)}"
```

### 5. Test Naming Conventions
- Test classes: `TestComponentName`
- Test methods: `test_specific_behavior`
- Be descriptive: `test_parse_pdf_with_invalid_path` not `test_error`

## Environment-Specific Considerations

### Local Development
```bash
# Install all dependencies including heavy ones
pip install -e ".[testing,testing-heavy]"

# CRITICAL: Set PYTHONPATH to include tests folder
export PYTHONPATH="${PYTHONPATH}:$(pwd)/tests"

# Set environment variables
export OPENAI_API_KEY="your-key"
export NEO4J_URI="bolt://localhost:7687"
```

### CI/CD Environment
- Only lightweight dependencies installed
- No external service credentials
- Fast execution priority

### Windows-Specific
The conftest.py handles Windows event loop policy automatically:
```python
if sys.platform.startswith('win'):
    asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())
```

## Troubleshooting

### Common Issues
1. **Import Errors**: Check if heavy dependencies are installed
2. **Fixture Not Found**: **MOST COMMON ISSUE** - Ensure PYTHONPATH includes tests folder:
   ```bash
   # This error means PYTHONPATH is not set correctly:
   # ImportError: cannot import name 'fixture_name' from 'tests.fixtures'
   
   # Fix:
   export PYTHONPATH="${PYTHONPATH}:$(pwd)/tests"
   ```
3. **Async Test Failures**: Use `@pytest.mark.anyio` decorator
4. **Path Issues**: Use `os.path.join()` and absolute paths from fixtures

### Debug Tips
```python
# Add debug output
def test_debug_example(capfd):
    print("Debug info")
    result = function_under_test()
    out, err = capfd.readouterr()
    assert "Debug info" in out
```

## Suggested Improvements

### 1. Enhanced Test Coverage
- Add property-based testing with Hypothesis
- Implement mutation testing
- Set minimum coverage threshold (80%)

### 2. Test Organization
- Create test factories for common test data
- Implement custom pytest plugins for project-specific needs
- Add performance benchmarks

### 3. Documentation
- Add docstrings to all test methods
- Create test scenario documentation
- Maintain test coverage reports

### 4. Environment Management
- Use pytest-env for environment variable management
- Implement test containers for database tests
- Add fixture factories for complex setups

### 5. CI/CD Enhancements
- Add parallel test execution
- Implement test result caching
- Add automated test report generation

### 6. Code Quality
- Add type hints to test code
- Implement custom assertions
- Use parametrized tests more extensively

### 7. Missing Test Implementations
Complete stub tests in configuration parser:
- `test_parse_configuration_with_invalid_file`
- `test_parse_configuration_with_invalid_json`
- `test_parse_configuration_with_missing_fields`
- `test_parse_configuration_with_invalid_fields`

### 8. Test Data Improvements
- Implement test data generators
- Add data validation for test inputs
- Create smaller, focused test datasets

## Quick Reference

### Essential Fixtures
- `inputs_folder_absolute_path`: Path to test input data
- `outputs_folder_absolute_path`: Path for test outputs
- `open_ai_configuration`: OpenAI config manager
- `test_data_configuration`: Test data config manager

### Key Decorators
- `@pytest.mark.heavy`: Mark heavy tests
- `@pytest.mark.anyio`: Mark async tests
- `@pytest.mark.skipif()`: Conditional test skipping
- `@pytest.fixture`: Define reusable test fixtures

### Testing Commands Cheatsheet
```bash
# ALWAYS SET PYTHONPATH FIRST!
export PYTHONPATH="${PYTHONPATH}:$(pwd)/tests"

# Quick test run
PYTHONPATH=tests pytest tests/unit_tests -x  # Stop on first failure

# Test discovery
PYTHONPATH=tests pytest --collect-only  # Show what tests would run

# Parallel execution (requires pytest-xdist)
PYTHONPATH=tests pytest -n auto  # Use all CPU cores

# Test filtering
PYTHONPATH=tests pytest -k "test_parse"  # Run tests containing "test_parse"

# One-liner for quick testing (combines PYTHONPATH and pytest)
PYTHONPATH=tests pytest tests/unit_tests/configurations/test_configuration_parser.py -v
```