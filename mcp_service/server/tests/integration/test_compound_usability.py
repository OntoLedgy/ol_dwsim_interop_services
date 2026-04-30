# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import asyncio
from types import SimpleNamespace

import pytest

from dwsim_mcp_server.models.mcp_inputs.compound_validation import ListCompoundsInput, ValidateCompoundsInput

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.ipc.exceptions import AssemblyLoadError, InteropError, SessionError
from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.service import FlowsheetService


class _MockSessionClient:
    async def run_session_operation(self, session_id, func, *, timeout_override=None):
        del session_id, timeout_override
        return func()

    async def close_session(self, session_id):
        del session_id
        return True

    async def stop_monitoring(self):
        return None


class _MockFlowsheetClient:
    def __init__(self):
        self._compounds = [
            {
                "name": "Methane",
                "category": "Hydrocarbon",
                "aliases": ["CH4"],
                "formula": "CH4",
            },
            {
                "name": "Propane",
                "category": "Hydrocarbon",
                "aliases": ["C3H8"],
                "formula": "C3H8",
            },
            {
                "name": "Carbon Dioxide",
                "category": "Inorganic",
                "aliases": ["CO2"],
                "formula": "CO2",
            },
            {
                "name": "Isobutane",
                "category": "Hydrocarbon",
                "aliases": ["isobutane"],
                "formula": "C4H10",
            },
            {
                "name": "Water",
                "category": "Inorganic",
                "aliases": ["H2O"],
                "formula": "H2O",
            },
        ]

    def validate_compounds(self, session_id: str, *, compound_names: list[str]):
        del session_id
        results = []
        for name in compound_names:
            resolved, alias_used = self._resolve_compound(name)
            if resolved is None:
                results.append(
                    {
                        "input_name": name,
                        "valid": False,
                        "canonical_name": None,
                        "alias_used": False,
                        "suggestions": self._suggest(name),
                    }
                )
                continue

            results.append(
                {
                    "input_name": name,
                    "valid": True,
                    "canonical_name": resolved["name"],
                    "alias_used": alias_used,
                    "suggestions": [],
                }
            )
        return {"results": results}

    def list_compounds(
        self,
        session_id: str,
        *,
        pattern: str | None,
        category: str | None,
        limit: int,
        offset: int,
    ):
        del session_id
        compounds = list(self._compounds)
        if pattern:
            needle = pattern.lower()
            compounds = [c for c in compounds if needle in c["name"].lower()]
        if category:
            category_lower = category.lower()
            compounds = [c for c in compounds if c["category"].lower() == category_lower]

        total_count = len(compounds)
        page = compounds[offset : offset + limit]
        return {
            "compounds": page,
            "total_count": total_count,
            "has_more": offset + limit < total_count,
        }

    def _resolve_compound(self, name: str):
        name_lower = name.lower()
        for compound in self._compounds:
            if compound["name"].lower() == name_lower:
                return compound, False
            for alias in compound.get("aliases", []):
                if alias.lower() == name_lower:
                    return compound, True
        return None, False

    def _suggest(self, name: str):
        name_lower = name.lower()
        suggestions = [
            compound["name"]
            for compound in self._compounds
            if compound["name"].lower().startswith(name_lower)
        ]
        if suggestions:
            return suggestions

        suggestions = [
            compound["name"]
            for compound in self._compounds
            if name_lower in compound["name"].lower()
        ]
        return suggestions


async def _close_session(session_client, session_id: str) -> None:
    try:
        await session_client.close_session(session_id)
    except SessionError:
        pass
    await session_client.stop_monitoring()


def _mock_context():
    mock_session = _MockSessionClient()
    mock_client = _MockFlowsheetClient()
    service = FlowsheetService(session_client=mock_session, flowsheet_client=mock_client)
    return SimpleNamespace(
        session_client=mock_session,
        session_id="mock-session",
        service=service,
        using_mock=True,
    )


def _result_by_input(results, input_name: str):
    return next((result for result in results if result.input_name == input_name), None)


@pytest.fixture
def compound_context():
    async def _setup():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()

        try:
            session_id = await session_client.create_session(flowsheet_name="mcp-compound-usability")
        except (AssemblyLoadError, InteropError) as exc:
            await session_client.stop_monitoring()
            return _mock_context()

        try:
            flowsheet_client = FlowsheetClient(session_client)
        except (AssemblyLoadError, InteropError):
            await _close_session(session_client, session_id)
            return _mock_context()

        flowsheet_service = FlowsheetService(
            session_client=session_client,
            flowsheet_client=flowsheet_client,
        )
        return SimpleNamespace(
            session_client=session_client,
            session_id=session_id,
            service=flowsheet_service,
            using_mock=False,
        )

    context = asyncio.run(_setup())
    try:
        yield context
    finally:
        asyncio.run(_close_session(context.session_client, context.session_id))


@pytest.mark.integration
class TestCompoundUsability:
    def test_alias_resolution(self, compound_context):
        async def _run():
            payload = ValidateCompoundsInput(
                session_id=compound_context.session_id,
                compound_names=["CO2", "co2", "Co2", "isobutane"],
            )
            result = await compound_context.service.validate_compounds(payload)
            assert result.results, "Expected validation results to be returned."

            for name in ["CO2", "co2", "Co2"]:
                entry = _result_by_input(result.results, name)
                assert entry is not None
                assert entry.valid is True
                assert entry.canonical_name is not None
                assert entry.canonical_name.lower() == "carbon dioxide"
                assert entry.alias_used is True

            iso_entry = _result_by_input(result.results, "isobutane")
            assert iso_entry is not None
            assert iso_entry.valid is True
            assert iso_entry.canonical_name is not None
            assert iso_entry.canonical_name.lower() == "i-butane"
            assert iso_entry.alias_used is True

        asyncio.run(_run())

    def test_fuzzy_matching_suggestions(self, compound_context):
        async def _run():
            payload = ValidateCompoundsInput(
                session_id=compound_context.session_id,
                compound_names=["Methan", "Propan"],
            )
            result = await compound_context.service.validate_compounds(payload)
            assert result.results, "Expected validation results to be returned."

            methan = _result_by_input(result.results, "Methan")
            assert methan is not None
            assert methan.valid is False
            assert any(suggestion.lower() == "methane" for suggestion in methan.suggestions)

            propan = _result_by_input(result.results, "Propan")
            assert propan is not None
            assert propan.valid is False
            assert any(suggestion.lower() == "propane" for suggestion in propan.suggestions)

        asyncio.run(_run())

    def test_case_insensitive_matching(self, compound_context):
        async def _run():
            payload = ValidateCompoundsInput(
                session_id=compound_context.session_id,
                compound_names=["methane", "WATER"],
            )
            result = await compound_context.service.validate_compounds(payload)
            assert result.results, "Expected validation results to be returned."

            methane = _result_by_input(result.results, "methane")
            assert methane is not None
            assert methane.valid is True
            assert methane.canonical_name is not None
            assert methane.canonical_name.lower() == "methane"

            water = _result_by_input(result.results, "WATER")
            assert water is not None
            assert water.valid is True
            assert water.canonical_name is not None
            assert water.canonical_name.lower() == "water"

        asyncio.run(_run())

    def test_list_compounds_filters(self, compound_context):
        async def _run():
            base_payload = ListCompoundsInput(
                session_id=compound_context.session_id,
                pattern=None,
                category=None,
                limit=2,
                offset=0,
            )
            base_result = await compound_context.service.list_compounds(base_payload)
            assert base_result.total_count >= len(base_result.compounds)
            assert len(base_result.compounds) <= 2

            offset_payload = ListCompoundsInput(
                session_id=compound_context.session_id,
                pattern=None,
                category=None,
                limit=2,
                offset=2,
            )
            offset_result = await compound_context.service.list_compounds(offset_payload)
            assert len(offset_result.compounds) <= 2

            if base_result.total_count > 2:
                assert base_result.has_more is True
                assert offset_result.compounds, "Expected compounds on the second page."
                assert base_result.compounds[0].name != offset_result.compounds[0].name

            pattern_payload = ListCompoundsInput(
                session_id=compound_context.session_id,
                pattern="Meth",
                category=None,
                limit=50,
                offset=0,
            )
            pattern_result = await compound_context.service.list_compounds(pattern_payload)
            assert pattern_result.compounds, "Expected pattern filter to return results."
            assert any(
                compound.name.lower() == "methane"
                for compound in pattern_result.compounds
            )

            category_source = base_result.compounds[0] if base_result.compounds else None
            if category_source is None:
                pytest.skip("No compounds available for category filtering.")
            category = category_source.category
            category_payload = ListCompoundsInput(
                session_id=compound_context.session_id,
                pattern=None,
                category=category,
                limit=50,
                offset=0,
            )
            category_result = await compound_context.service.list_compounds(category_payload)
            assert category_result.compounds, "Expected category filter to return results."
            assert all(
                compound.category.lower() == category.lower()
                for compound in category_result.compounds
            )

        asyncio.run(_run())
