<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

This file is part of the OntoLedgy Thermodynamics Architecture and is
dual-licensed:

  1. Open source under the GNU Affero General Public License v3.0 or
     later (AGPL-3.0-or-later). See the LICENSE file in the repository
     root for the full licence text and NOTICE for attribution.
  2. Commercial under a separate proprietary licence offered by
     OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

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
