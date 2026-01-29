import pytest

from dwsim_mcp_server.models.mcp_inputs import (
    ObjectiveSpec,
    OptimizationRequest,
    OutputSpec,
    ParameterSweepRequest,
    RangeSpec,
    SensitivityAnalysisRequest,
    SweepVariableSpec,
    VariableSpec,
    VariableWithBounds,
)


def build_variable_spec():
    return VariableSpec(object_id="obj-1", property_name="pressure")


def build_output_spec():
    return OutputSpec(object_id="obj-1", property_name="temperature")


def build_range():
    return RangeSpec(min_value=1.0, max_value=2.0)


def test_sensitivity_analysis_request_accepts_valid_payload():
    payload = SensitivityAnalysisRequest(
        session_id="s1",
        variable=build_variable_spec(),
        range=build_range(),
        steps=5,
        outputs=[build_output_spec()],
    )

    assert payload.steps == 5


def test_parameter_sweep_request_accepts_valid_payload():
    payload = ParameterSweepRequest(
        session_id="s1",
        variables=[
            SweepVariableSpec(
                object_id="obj-1",
                property_name="pressure",
                range=build_range(),
                steps=3,
            )
        ],
        outputs=[build_output_spec()],
    )

    assert payload.variables[0].steps == 3


def test_optimization_request_accepts_valid_payload():
    payload = OptimizationRequest(
        session_id="s1",
        objective=ObjectiveSpec(
            object_id="obj-1",
            property_name="duty",
            direction="minimize",
        ),
        variables=[
            VariableWithBounds(
                object_id="obj-2",
                property_name="temperature",
                lower=300.0,
                upper=400.0,
                initial=350.0,
            )
        ],
        constraints=None,
        max_iterations=50,
    )

    assert payload.objective.direction == "minimize"


@pytest.mark.parametrize("steps", [0, 1, 101])
def test_sensitivity_analysis_rejects_invalid_steps(steps):
    with pytest.raises(ValueError):
        SensitivityAnalysisRequest(
            session_id="s1",
            variable=build_variable_spec(),
            range=build_range(),
            steps=steps,
            outputs=[build_output_spec()],
        )


@pytest.mark.parametrize("steps", [0, 1, 101])
def test_parameter_sweep_rejects_invalid_steps(steps):
    with pytest.raises(ValueError):
        ParameterSweepRequest(
            session_id="s1",
            variables=[
                SweepVariableSpec(
                    object_id="obj-1",
                    property_name="pressure",
                    range=build_range(),
                    steps=steps,
                )
            ],
            outputs=[build_output_spec()],
        )


@pytest.mark.parametrize(
    ("min_value", "max_value"),
    [(1.0, 1.0), (2.0, 1.0)],
)
def test_range_spec_rejects_invalid_bounds(min_value, max_value):
    with pytest.raises(ValueError):
        RangeSpec(min_value=min_value, max_value=max_value)


@pytest.mark.parametrize(
    ("min_value", "max_value"),
    [(float("inf"), 2.0), (1.0, float("inf")), (float("nan"), 2.0), (1.0, float("nan"))],
)
def test_range_spec_rejects_non_finite_bounds(min_value, max_value):
    with pytest.raises(ValueError):
        RangeSpec(min_value=min_value, max_value=max_value)


@pytest.mark.parametrize(
    ("lower", "upper"),
    [(1.0, 1.0), (5.0, 3.0)],
)
def test_variable_with_bounds_rejects_invalid_bounds(lower, upper):
    with pytest.raises(ValueError):
        VariableWithBounds(
            object_id="obj-1",
            property_name="pressure",
            lower=lower,
            upper=upper,
        )


@pytest.mark.parametrize(
    ("lower", "upper"),
    [
        (float("inf"), 2.0),
        (1.0, float("inf")),
        (float("nan"), 2.0),
        (1.0, float("nan")),
    ],
)
def test_variable_with_bounds_rejects_non_finite_bounds(lower, upper):
    with pytest.raises(ValueError):
        VariableWithBounds(
            object_id="obj-1",
            property_name="pressure",
            lower=lower,
            upper=upper,
        )


def test_objective_spec_rejects_invalid_direction():
    with pytest.raises(ValueError):
        ObjectiveSpec(
            object_id="obj-1",
            property_name="duty",
            direction="min",
        )


def test_sensitivity_models_json_schema_includes_expected_fields():
    schema = SensitivityAnalysisRequest.model_json_schema()

    assert "properties" in schema
    assert "steps" in schema["properties"]
