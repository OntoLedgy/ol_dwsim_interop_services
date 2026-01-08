import pytest


class TestPythonnetLoading:
    @pytest.fixture(autouse=True)
    def setup(self, dwsim_worker_types):
        (
            self.session_manager_type,
            self.flowsheet_context_builder_type,
            self.logger_configuration_type,
        ) = dwsim_worker_types

    def test_session_manager_instantiation(self):
        logger = self.logger_configuration_type().MinimumLevel.Debug().CreateLogger()
        config = (
            self.flowsheet_context_builder_type()
            .WithFlowsheetName("pythonnet-test")
            .Build()
        )
        manager = self.session_manager_type(logger, config)
        assert manager is not None
