# Testing Plan

This project contains two categories of tests:

## Lightweight tests (run in CI)
- Marked implicitly by running `pytest -m "not heavy"`.
- Only require the dependencies from the `testing` group.
- Executed automatically in GitHub Actions.
- Install and run with:
  ```bash
  pip install -e .[testing]
  pytest -m "not heavy" tests/unit_tests
  ```

## Heavy tests (run locally)
- Marked with `@pytest.mark.heavy` via path-based marking.
- Depend on additional libraries such as data-science stacks, LLM frameworks and database drivers.
- Intended to be executed locally before major Pull Requests.
- Install and run with:
  ```bash
  pip install -e .[testing,testing-heavy]
  pytest -m heavy tests
  ```

## Pre-push Hook Suggestion
Developers can add a local pre-push hook to ensure heavy tests pass before pushing:
```bash
# .git/hooks/pre-push
pip install -e .[testing,testing-heavy]
pytest -m heavy tests
```
Make the hook executable with `chmod +x .git/hooks/pre-push`.
