import pytest
from pydantic import ValidationError

from models.mcp_inputs import (
    CompoundInfo,
    CompoundValidationResult,
    ExportCsvInput,
    ExportCsvOutput,
    ExportJsonInput,
    ExportJsonOutput,
    GenerateReportInput,
    GenerateReportOutput,
    ListCompoundsInput,
    ListCompoundsOutput,
    SaveCaseInput,
    SaveCaseOutput,
    ValidateCompoundsInput,
    ValidateCompoundsOutput,
)



class TestExportModels:
    def test_export_csv_input_accepts_csv_path(self):
        model = ExportCsvInput(session_id="session-1", file_path="results/output.csv")
        assert model.file_path.endswith(".csv")


    @pytest.mark.parametrize("file_path", ["output.txt", "output.xls", "outputcsv"])
    def test_export_csv_input_rejects_non_csv_path(self, file_path: str):
        with pytest.raises(ValidationError, match="file_path must end with '.csv'"):
            ExportCsvInput(session_id="session-1", file_path=file_path)


    @pytest.mark.parametrize("format_value", ["summary", "FULL"])
    def test_export_json_input_accepts_supported_format(self, format_value: str):
        model = ExportJsonInput(session_id="session-1", format=format_value)
        assert model.format in {"summary", "full"}


    def test_export_json_input_rejects_unsupported_format(self):
        with pytest.raises(ValidationError, match="format must be one of: summary, full"):
            ExportJsonInput(session_id="session-1", format="detailed")


    def test_export_json_output_accepts_payload(self):
        model = ExportJsonOutput(data={"status": "ok", "count": 2})
        assert model.data["count"] == 2


    @pytest.mark.parametrize("template", ["summary", "DETAILED"])
    def test_generate_report_input_accepts_template_and_path(self, template: str):
        model = GenerateReportInput(
            session_id="session-1",
            template=template,
            file_path="reports/summary.md",
        )
        assert model.template in {"summary", "detailed"}


    @pytest.mark.parametrize(
        "template,file_path,expected_error",
        [
            ("overview", "reports/summary.md", "template must be one of: summary, detailed"),
            ("summary", "reports/summary.txt", "file_path must end with '.md'"),
        ],
    )
    def test_generate_report_input_rejects_invalid_values(
        self, template: str, file_path: str, expected_error: str
    ):
        with pytest.raises(ValidationError, match=expected_error):
            GenerateReportInput(
                session_id="session-1",
                template=template,
                file_path=file_path,
            )


    def test_generate_report_output_accepts_success(self):
        model = GenerateReportOutput(success=True, file_path="reports/summary.md")
        assert model.success is True


    @pytest.mark.parametrize("file_path", ["case.dwxmz", "case.dwxml"])
    def test_save_case_input_accepts_supported_extensions(self, file_path: str):
        model = SaveCaseInput(session_id="session-1", file_path=file_path)
        assert model.file_path == file_path


    def test_save_case_input_rejects_unsupported_extension(self):
        with pytest.raises(
            ValidationError, match="file_path must end with one of: .dwxmz, .dwxml"
        ):
            SaveCaseInput(session_id="session-1", file_path="case.txt")


    def test_save_case_output_accepts_success(self):
        model = SaveCaseOutput(success=True, file_path="case.dwxmz")
        assert model.file_path.endswith(".dwxmz")


    @pytest.mark.parametrize(
        "row_count,expected_error",
        [
            (0, None),
            (5, None),
            (-1, "Input should be greater than or equal to 0"),
        ],
    )
    def test_export_csv_output_row_count_bounds(self, row_count: int, expected_error: str | None):
        if expected_error:
            with pytest.raises(ValidationError, match=expected_error):
                ExportCsvOutput(success=True, file_path="export.csv", row_count=row_count)
        else:
            model = ExportCsvOutput(success=True, file_path="export.csv", row_count=row_count)
            assert model.row_count == row_count


    def test_validate_compounds_input_accepts_names(self):
        model = ValidateCompoundsInput(
            session_id="session-1", compound_names=["methane", "ethane"]
        )
        assert model.compound_names == ["methane", "ethane"]


    def test_validate_compounds_input_rejects_empty_list(self):
        with pytest.raises(
            ValidationError, match="compound_names must contain at least 1 item"
        ):
            ValidateCompoundsInput(session_id="session-1", compound_names=[])


    @pytest.mark.parametrize("limit", [1, 50, 100])
    def test_list_compounds_input_accepts_limit_bounds(self, limit: int):
        model = ListCompoundsInput(session_id="session-1", limit=limit)
        assert model.limit == limit


    @pytest.mark.parametrize("limit", [0, 101])
    def test_list_compounds_input_rejects_invalid_limit(self, limit: int):
        with pytest.raises(ValidationError, match="limit must be between 1 and 100"):
            ListCompoundsInput(session_id="session-1", limit=limit)


    @pytest.mark.parametrize("offset", [0, 5, 25])
    def test_list_compounds_input_accepts_non_negative_offset(self, offset: int):
        model = ListCompoundsInput(session_id="session-1", offset=offset)
        assert model.offset == offset


    def test_list_compounds_input_rejects_negative_offset(self):
        with pytest.raises(ValidationError, match="offset must be >= 0"):
            ListCompoundsInput(session_id="session-1", offset=-1)


    def test_compound_info_model_defaults(self):
        model = CompoundInfo(name="methane", category="Hydrocarbon")
        assert model.aliases == []
        assert model.formula is None


    def test_compound_validation_result_model_defaults(self):
        model = CompoundValidationResult(
            input_name="MeOH",
            valid=False,
            canonical_name=None,
            alias_used=False,
        )
        assert model.suggestions == []


    def test_validate_compounds_output_accepts_nested_results(self):
        result = CompoundValidationResult(
            input_name="methane",
            valid=True,
            canonical_name="methane",
            alias_used=False,
            suggestions=[],
        )
        output = ValidateCompoundsOutput(results=[result])
        assert output.results[0].canonical_name == "methane"


    @pytest.mark.parametrize(
        "total_count,expected_error",
        [
            (0, None),
            (2, None),
            (-1, "Input should be greater than or equal to 0"),
        ],
    )
    def test_list_compounds_output_total_count_bounds(
        self, total_count: int, expected_error: str | None
    ):
        compound = CompoundInfo(name="ethane", category="Hydrocarbon")
        if expected_error:
            with pytest.raises(ValidationError, match=expected_error):
                ListCompoundsOutput(
                    compounds=[compound],
                    total_count=total_count,
                    has_more=False,
                )
        else:
            output = ListCompoundsOutput(
                compounds=[compound],
                total_count=total_count,
                has_more=False,
            )
            assert output.total_count == total_count
