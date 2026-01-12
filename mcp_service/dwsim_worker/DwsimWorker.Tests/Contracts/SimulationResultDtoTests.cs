using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Xunit;
using DwsimWorker.Contracts.CapeOpen;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Contracts
{
    public class SimulationResultDtoTests
    {
        [Fact]
        public void SimulationResultDto_RoundTripSerialization_PreservesValues()
        {
            var dto = new SimulationResultDto(
                status: "converged",
                convergenceState: "Converged",
                elapsedMilliseconds: 1234.5,
                streamResults: new List<MaterialStreamDto>
                {
                    new MaterialStreamDto
                    {
                        Id = "S1",
                        Name = "Feed",
                        TemperatureK = 298.15,
                        PressurePa = 101325,
                        TotalMolarFlowMolPerS = 5.0,
                        Phases = new List<PhaseDto>()
                    }
                },
                messages: new List<string> { "Calculation converged successfully." },
                massBalanceValid: true,
                massBalanceErrorPercent: 0.4);

            var json = JsonConvert.SerializeObject(dto);
            var roundTrip = JsonConvert.DeserializeObject<SimulationResultDto>(json);

            Assert.NotNull(roundTrip);
            Assert.Equal(dto.Status, roundTrip.Status);
            Assert.Equal(dto.ConvergenceState, roundTrip.ConvergenceState);
            Assert.Equal(dto.ElapsedMilliseconds, roundTrip.ElapsedMilliseconds);
            Assert.Single(roundTrip.StreamResults);
            Assert.Equal(dto.StreamResults[0].Name, roundTrip.StreamResults[0].Name);
            Assert.Equal(dto.Messages[0], roundTrip.Messages[0]);
            Assert.Equal(dto.MassBalanceValid, roundTrip.MassBalanceValid);
            Assert.Equal(dto.MassBalanceErrorPercent, roundTrip.MassBalanceErrorPercent);
        }

        [Fact]
        public void SimulationResultDto_Properties_AreReadOnly()
        {
            var properties = typeof(SimulationResultDto).GetProperties();
            foreach (var property in properties)
            {
                Assert.False(property.CanWrite, $"{property.Name} should be read-only.");
            }
        }

        [Fact]
        public void SimulationResultDto_CanBeBuiltFromCalculationResult()
        {
            var convergence = ConvergenceStatus.Converged(5, 0.01);
            var timing = CalculationTiming.FromTimestamps(DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
            var messages = new List<SolverMessage>
            {
                new SolverMessage(SolverMessageLevel.Info, "Calculation converged.", "Solver")
            };
            var massBalance = MassBalanceResult.Valid(10, 10);

            var composition = new Composition(new List<double> { 1.0 });
            var streamResult = new StreamResult(
                "S1",
                "Feed",
                300.0,
                101325.0,
                1.0,
                1.0,
                composition,
                0.0,
                1.0);

            var calculationResult = CalculationResult.SuccessResult(
                convergence,
                timing,
                new List<StreamResult> { streamResult },
                massBalance,
                messages);

            var dto = new SimulationResultDto(
                status: calculationResult.Success ? "converged" : "failed",
                convergenceState: calculationResult.ConvergenceStatus.State.ToString(),
                elapsedMilliseconds: calculationResult.Timing.TotalMilliseconds,
                streamResults: new List<MaterialStreamDto>(),
                messages: new List<string> { calculationResult.Messages[0].ToString() },
                massBalanceValid: calculationResult.MassBalance?.IsValid,
                massBalanceErrorPercent: calculationResult.MassBalance?.RelativeErrorPercent);

            Assert.Equal("converged", dto.Status);
            Assert.Equal("Converged", dto.ConvergenceState);
            Assert.True(dto.MassBalanceValid);
            Assert.NotEmpty(dto.Messages);
        }
    }
}
