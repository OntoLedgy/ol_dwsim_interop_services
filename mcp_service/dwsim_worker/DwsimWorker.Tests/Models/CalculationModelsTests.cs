// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Models
{
    /// <summary>
    /// Unit tests for calculation-related data models.
    /// Tests all models created for three-phase separator calculation spec (tasks 1-7):
    /// ConvergenceStatus, SolverMessage, CalculationTiming, PhaseProperties, StreamResult,
    /// MassBalanceResult, ComponentMassBalance, and CalculationResult.
    /// </summary>
    public class CalculationModelsTests
    {
        #region ConvergenceStatus Tests

        [Fact]
        public void ConvergenceStatus_Converged_CreatesValidStatus()
        {
            // Arrange
            int iterations = 10;
            double residualError = 0.00001;

            // Act
            var status = ConvergenceStatus.Converged(iterations, residualError);

            // Assert
            Assert.Equal(ConvergenceState.Converged, status.State);
            Assert.Equal(iterations, status.Iterations);
            Assert.Equal(residualError, status.ResidualError);
            Assert.NotNull(status.Message);
            Assert.Contains("converged", status.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConvergenceStatus_NotConverged_CreatesValidStatus()
        {
            // Arrange
            string message = "Did not converge within iteration limit";
            int iterations = 100;
            double residualError = 0.1;

            // Act
            var status = ConvergenceStatus.NotConverged(message, iterations, residualError);

            // Assert
            Assert.Equal(ConvergenceState.NotConverged, status.State);
            Assert.Equal(iterations, status.Iterations);
            Assert.Equal(residualError, status.ResidualError);
            Assert.NotNull(status.Message);
            Assert.Equal(message, status.Message);
        }

        [Fact]
        public void ConvergenceStatus_Error_CreatesValidStatus()
        {
            // Arrange
            string errorMessage = "Solver failed: matrix singular";

            // Act
            var status = ConvergenceStatus.Error(errorMessage);

            // Assert
            Assert.Equal(ConvergenceState.Error, status.State);
            Assert.Equal(errorMessage, status.Message);
            Assert.Null(status.Iterations);
            Assert.Null(status.ResidualError);
        }

        [Fact]
        public void ConvergenceStatus_InProgress_CreatesValidStatus()
        {
            // Arrange
            string message = "Calculation in progress - iteration 5";

            // Act
            var status = ConvergenceStatus.InProgress(message);

            // Assert
            Assert.Equal(ConvergenceState.InProgress, status.State);
            Assert.Equal(message, status.Message);
        }

        [Fact]
        public void ConvergenceStatus_NotStarted_CreatesValidStatus()
        {
            // Act
            var status = ConvergenceStatus.NotStarted();

            // Assert
            Assert.Equal(ConvergenceState.NotStarted, status.State);
            Assert.Null(status.Iterations);
        }

        #endregion

        #region SolverMessage Tests

        [Fact]
        public void SolverMessage_WithAllParameters_CreatesValidMessage()
        {
            // Arrange
            var level = SolverMessageLevel.Warning;
            var message = "Convergence slow, consider adjusting parameters";
            var source = "FlowsheetSolver";
            var timestamp = DateTime.UtcNow;

            // Act
            var solverMessage = new SolverMessage(level, message, source, timestamp);

            // Assert
            Assert.Equal(level, solverMessage.Level);
            Assert.Equal(message, solverMessage.Message);
            Assert.Equal(source, solverMessage.Source);
            Assert.Equal(timestamp, solverMessage.Timestamp);
        }

        [Fact]
        public void SolverMessage_WithoutTimestamp_UsesCurrentTime()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;

            // Act
            var solverMessage = new SolverMessage(SolverMessageLevel.Info, "Test message", "TestSource");
            var afterCreation = DateTime.UtcNow;

            // Assert
            Assert.True(solverMessage.Timestamp >= beforeCreation);
            Assert.True(solverMessage.Timestamp <= afterCreation);
        }

        [Theory]
        [InlineData(SolverMessageLevel.Debug)]
        [InlineData(SolverMessageLevel.Info)]
        [InlineData(SolverMessageLevel.Warning)]
        [InlineData(SolverMessageLevel.Error)]
        public void SolverMessage_AllLevels_AreSupported(SolverMessageLevel level)
        {
            // Act
            var message = new SolverMessage(level, "Test", "Source");

            // Assert
            Assert.Equal(level, message.Level);
        }

        #endregion

        #region CalculationTiming Tests

        [Fact]
        public void CalculationTiming_FromTimestamps_CalculatesCorrectDuration()
        {
            // Arrange
            var start = new DateTime(2026, 1, 6, 12, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 1, 6, 12, 0, 5, DateTimeKind.Utc); // 5 seconds later

            // Act
            var timing = CalculationTiming.FromTimestamps(start, end);

            // Assert
            Assert.Equal(start, timing.StartedAt);
            Assert.Equal(end, timing.CompletedAt);
            Assert.Equal(TimeSpan.FromSeconds(5), timing.TotalTime);
            Assert.Equal(5000, timing.TotalMilliseconds);
        }

        [Fact]
        public void CalculationTiming_FromTimestamps_WithShortDuration_CreatesValidTiming()
        {
            // Arrange
            var start = DateTime.UtcNow;
            var end = start.AddSeconds(3.5);

            // Act
            var timing = CalculationTiming.FromTimestamps(start, end);

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(3.5), timing.TotalTime);
            Assert.Equal(3500, timing.TotalMilliseconds);
            Assert.Equal(start, timing.StartedAt);
            Assert.Equal(end, timing.CompletedAt);
        }

        [Fact]
        public void CalculationTiming_TotalMilliseconds_IsAccurate()
        {
            // Arrange
            var duration = TimeSpan.FromMilliseconds(123.456);
            var start = DateTime.UtcNow;
            var end = start + duration;

            // Act
            var timing = CalculationTiming.FromTimestamps(start, end);

            // Assert
            // TimeSpan precision may not preserve fractional milliseconds perfectly
            Assert.True(Math.Abs(timing.TotalMilliseconds - 123.456) < 1.0,
                $"Expected ~123.456ms, got {timing.TotalMilliseconds}ms");
        }

        #endregion

        #region PhaseProperties Tests

        [Fact]
        public void PhaseProperties_WithAllProperties_CreatesValidObject()
        {
            // Arrange
            var composition = new Composition(new List<double> { 0.5, 0.3, 0.2 });

            // Act
            var phase = new PhaseProperties(
                phaseName: "Vapor",
                molarFlowMolPerSec: 10.0,
                massFlowKgPerSec: 5.5,
                phaseFraction: 0.4,
                composition: composition,
                densityKgPerM3: 1.2,
                viscosityPaS: 0.00001,
                molecularWeightKgPerKmol: 28.0);

            // Assert
            Assert.Equal("Vapor", phase.PhaseName);
            Assert.Equal(10.0, phase.MolarFlowMolPerSec);
            Assert.Equal(5.5, phase.MassFlowKgPerSec);
            Assert.Equal(0.4, phase.PhaseFraction);
            Assert.Equal(composition, phase.Composition);
            Assert.Equal(1.2, phase.DensityKgPerM3);
            Assert.Equal(0.00001, phase.ViscosityPaS);
            Assert.Equal(28.0, phase.MolecularWeightKgPerKmol);
        }

        [Fact]
        public void PhaseProperties_WithOptionalPropertiesNull_IsValid()
        {
            // Arrange
            var composition = new Composition(new List<double> { 1.0 });

            // Act
            var phase = new PhaseProperties(
                phaseName: "Liquid1",
                molarFlowMolPerSec: 20.0,
                massFlowKgPerSec: 18.0,
                phaseFraction: 0.6,
                composition: composition);

            // Assert
            Assert.Equal("Liquid1", phase.PhaseName);
            Assert.Null(phase.DensityKgPerM3);
            Assert.Null(phase.ViscosityPaS);
            Assert.Null(phase.MolecularWeightKgPerKmol);
        }

        #endregion

        #region StreamResult Tests

        [Fact]
        public void StreamResult_WithAllProperties_CreatesValidObject()
        {
            // Arrange
            var overallComp = new Composition(new List<double> { 0.4, 0.4, 0.2 });
            var vaporPhase = new PhaseProperties("Vapor", 5.0, 2.5, 0.25, overallComp);
            var liquid1Phase = new PhaseProperties("Liquid1", 10.0, 9.0, 0.5, overallComp);
            var liquid2Phase = new PhaseProperties("Liquid2", 5.0, 4.5, 0.25, overallComp);

            var phases = new Dictionary<string, PhaseProperties>
            {
                { "Vapor", vaporPhase },
                { "Liquid1", liquid1Phase },
                { "Liquid2", liquid2Phase }
            };

            // Act
            var result = new StreamResult(
                streamId: "S1",
                streamName: "FEED",
                temperatureK: 298.15,
                pressurePa: 101325.0,
                molarFlowMolPerSec: 20.0,
                massFlowKgPerSec: 16.0,
                overallComposition: overallComp,
                vaporFraction: 0.25,
                liquidFraction: 0.75,
                phases: phases);

            // Assert
            Assert.Equal("S1", result.StreamId);
            Assert.Equal("FEED", result.StreamName);
            Assert.Equal(298.15, result.TemperatureK);
            Assert.Equal(101325.0, result.PressurePa);
            Assert.Equal(20.0, result.MolarFlowMolPerSec);
            Assert.Equal(16.0, result.MassFlowKgPerSec);
            Assert.Equal(overallComp, result.OverallComposition);
            Assert.Equal(0.25, result.VaporFraction);
            Assert.Equal(0.75, result.LiquidFraction);
            Assert.Equal(3, result.Phases.Count);
            Assert.True(result.Phases.ContainsKey("Vapor"));
            Assert.True(result.Phases.ContainsKey("Liquid1"));
            Assert.True(result.Phases.ContainsKey("Liquid2"));
        }

        [Fact]
        public void StreamResult_Phases_CanBeAccessedByExactKey()
        {
            // Arrange
            var composition = new Composition(new List<double> { 1.0 });
            var vaporPhase = new PhaseProperties("Vapor", 5.0, 2.5, 1.0, composition);
            var phases = new Dictionary<string, PhaseProperties>
            {
                { "Vapor", vaporPhase }
            };

            // Act
            var result = new StreamResult("S1", "TEST", 300, 100000, 5, 2.5, composition, 1.0, 0.0, phases);

            // Assert
            Assert.True(result.Phases.ContainsKey("Vapor"));
            Assert.Equal(vaporPhase, result.Phases["Vapor"]);
            Assert.Single(result.Phases);
        }

        #endregion

        #region ComponentMassBalance Tests

        [Fact]
        public void ComponentMassBalance_Create_WithBalancedFlows_IsValid()
        {
            // Arrange
            string compoundName = "Water";
            double inletMoles = 100.0;
            double outletMoles = 100.0;
            double tolerance = 1.0;

            // Act
            var balance = ComponentMassBalance.Create(compoundName, inletMoles, outletMoles, tolerance);

            // Assert
            Assert.Equal(compoundName, balance.CompoundName);
            Assert.Equal(inletMoles, balance.InletMoles);
            Assert.Equal(outletMoles, balance.OutletMoles);
            Assert.True(balance.IsValid);
            Assert.Equal(0.0, balance.RelativeErrorPercent);
        }

        [Fact]
        public void ComponentMassBalance_Create_WithUnbalancedFlows_IsInvalid()
        {
            // Arrange
            string compoundName = "Methane";
            double inletMoles = 100.0;
            double outletMoles = 110.0; // 10% error
            double tolerance = 1.0; // 1% tolerance

            // Act
            var balance = ComponentMassBalance.Create(compoundName, inletMoles, outletMoles, tolerance);

            // Assert
            Assert.Equal(compoundName, balance.CompoundName);
            Assert.False(balance.IsValid);
            Assert.Equal(10.0, balance.RelativeErrorPercent);
        }

        [Fact]
        public void ComponentMassBalance_Create_WithinTolerance_IsValid()
        {
            // Arrange
            double inletMoles = 100.0;
            double outletMoles = 100.5; // 0.5% error
            double tolerance = 1.0; // 1% tolerance

            // Act
            var balance = ComponentMassBalance.Create("Propane", inletMoles, outletMoles, tolerance);

            // Assert
            Assert.True(balance.IsValid);
            Assert.Equal(0.5, balance.RelativeErrorPercent);
        }

        #endregion

        #region MassBalanceResult Tests

        [Fact]
        public void MassBalanceResult_Create_WithBalancedFlows_IsValid()
        {
            // Arrange
            double inletFlow = 100.0;
            double outletFlow = 100.0;
            double tolerance = 1.0;

            // Act
            var result = MassBalanceResult.Create(inletFlow, outletFlow, tolerance, null);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(inletFlow, result.InletMolarFlow);
            Assert.Equal(outletFlow, result.OutletMolarFlow);
            Assert.Equal(0.0, result.AbsoluteError);
            Assert.Equal(0.0, result.RelativeErrorPercent);
            Assert.Equal(tolerance, result.TolerancePercent);
        }

        [Fact]
        public void MassBalanceResult_Create_WithUnbalancedFlows_IsInvalid()
        {
            // Arrange
            double inletFlow = 100.0;
            double outletFlow = 95.0; // 5 mol/s loss
            double tolerance = 1.0;

            // Act
            var result = MassBalanceResult.Create(inletFlow, outletFlow, tolerance, null);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(inletFlow, result.InletMolarFlow);
            Assert.Equal(outletFlow, result.OutletMolarFlow);
            Assert.Equal(5.0, result.AbsoluteError, precision: 10);
            Assert.Equal(5.0, result.RelativeErrorPercent, precision: 10);
        }

        [Fact]
        public void MassBalanceResult_Create_WithComponentBalances_StoresCorrectly()
        {
            // Arrange
            var componentBalances = new List<ComponentMassBalance>
            {
                ComponentMassBalance.Create("Water", 50, 50, 1.0),
                ComponentMassBalance.Create("Ethanol", 50, 50, 1.0)
            };

            // Act
            var result = MassBalanceResult.Create(100, 100, 1.0, componentBalances);

            // Assert
            Assert.NotNull(result.ComponentBalances);
            Assert.Equal(2, result.ComponentBalances.Count);
            Assert.Contains(result.ComponentBalances, cb => cb.CompoundName == "Water");
            Assert.Contains(result.ComponentBalances, cb => cb.CompoundName == "Ethanol");
        }

        [Fact]
        public void MassBalanceResult_Valid_FactoryMethod_CreatesValidResult()
        {
            // Arrange
            double inletFlow = 100.0;
            double outletFlow = 99.9;

            // Act
            var result = MassBalanceResult.Valid(inletFlow, outletFlow);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(inletFlow, result.InletMolarFlow);
            Assert.Equal(outletFlow, result.OutletMolarFlow);
        }

        [Fact]
        public void MassBalanceResult_Invalid_FactoryMethod_CreatesInvalidResult()
        {
            // Arrange
            double inletFlow = 100.0;
            double outletFlow = 90.0;
            double tolerance = 1.0;

            // Act
            var result = MassBalanceResult.Invalid(inletFlow, outletFlow, tolerance);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(inletFlow, result.InletMolarFlow);
            Assert.Equal(outletFlow, result.OutletMolarFlow);
            Assert.Equal(10.0, result.RelativeErrorPercent);
        }

        #endregion

        #region CalculationResult Tests

        [Fact]
        public void CalculationResult_SuccessResult_CreatesValidResult()
        {
            // Arrange
            var convergenceStatus = ConvergenceStatus.Converged(10, 0.0001);
            var start = DateTime.UtcNow;
            var end = start.AddSeconds(2);
            var timing = CalculationTiming.FromTimestamps(start, end);
            var streamResults = new List<StreamResult>();
            var massBalance = MassBalanceResult.Valid(100, 100);

            // Act
            var result = CalculationResult.SuccessResult(convergenceStatus, timing, streamResults, massBalance);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(convergenceStatus, result.ConvergenceStatus);
            Assert.Equal(timing, result.Timing);
            Assert.Equal(streamResults, result.StreamResults);
            Assert.Equal(massBalance, result.MassBalance);
            Assert.NotNull(result.Messages);
            Assert.Empty(result.Messages);
            Assert.Null(result.Error);
        }

        [Fact]
        public void CalculationResult_FailureResult_CreatesValidResult()
        {
            // Arrange
            string errorMessage = "Solver failed to initialize";
            var exception = new InvalidOperationException(errorMessage);
            var convergenceStatus = ConvergenceStatus.Error(errorMessage);
            var start = DateTime.UtcNow;
            var end = start.AddSeconds(0.5);
            var timing = CalculationTiming.FromTimestamps(start, end);

            // Act
            var result = CalculationResult.FailureResult(errorMessage, exception, convergenceStatus, timing);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(errorMessage, result.Message);
            Assert.Equal(exception, result.Error);
            Assert.Equal(convergenceStatus, result.ConvergenceStatus);
            Assert.Equal(timing, result.Timing);
            Assert.NotNull(result.StreamResults);
            Assert.Empty(result.StreamResults);
            Assert.Null(result.MassBalance);
            Assert.NotNull(result.Messages);
            Assert.Empty(result.Messages);
        }

        [Fact]
        public void CalculationResult_NotConvergedResult_CreatesValidResult()
        {
            // Arrange
            string convergenceMessage = "Did not converge";
            var convergenceStatus = ConvergenceStatus.NotConverged(convergenceMessage, 100, 0.05);
            var start = DateTime.UtcNow;
            var end = start.AddSeconds(5);
            var timing = CalculationTiming.FromTimestamps(start, end);
            var messages = new List<SolverMessage>
            {
                new SolverMessage(SolverMessageLevel.Warning, "Convergence slow", "Solver")
            };
            string detailMessage = "Failed to converge within iteration limit";

            // Act
            var result = CalculationResult.NotConvergedResult(convergenceStatus, timing, messages, detailMessage);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(detailMessage, result.Message);
            Assert.Equal(convergenceStatus, result.ConvergenceStatus);
            Assert.Equal(timing, result.Timing);
            Assert.Equal(messages, result.Messages);
            Assert.Null(result.Error);
        }

        [Fact]
        public void CalculationResult_SuccessResult_WithMessages_IncludesMessages()
        {
            // Arrange
            var convergenceStatus = ConvergenceStatus.Converged(5, 0.0001);
            var start = DateTime.UtcNow;
            var end = start.AddSeconds(1);
            var timing = CalculationTiming.FromTimestamps(start, end);
            var streamResults = new List<StreamResult>();
            var massBalance = MassBalanceResult.Valid(100, 100);
            var messages = new List<SolverMessage>
            {
                new SolverMessage(SolverMessageLevel.Info, "Calculation complete", "Solver")
            };

            // Act
            var result = CalculationResult.SuccessResult(convergenceStatus, timing, streamResults, massBalance, messages);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Messages);
            Assert.Single(result.Messages);
            Assert.Equal("Calculation complete", result.Messages[0].Message);
        }

        [Fact]
        public void CalculationResult_FailureResult_MinimalParameters_CreatesValidResult()
        {
            // Arrange
            string errorMessage = "Calculation failed";
            var exception = new Exception(errorMessage);

            // Act
            var result = CalculationResult.FailureResult(errorMessage, exception);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(errorMessage, result.Message);
            Assert.Equal(exception, result.Error);
            Assert.NotNull(result.ConvergenceStatus);
            Assert.Equal(ConvergenceState.Error, result.ConvergenceStatus.State);
            Assert.Null(result.Timing);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void CalculationModels_CompleteWorkflow_AllModelsWorkTogether()
        {
            // Arrange - simulate a complete three-phase separator calculation
            var convergenceStatus = ConvergenceStatus.Converged(15, 0.0001);
            var start = DateTime.UtcNow;
            var end = start.AddSeconds(3.5);
            var timing = CalculationTiming.FromTimestamps(start, end);

            var composition = new Composition(new List<double> { 0.3, 0.4, 0.3 });
            var vaporPhase = new PhaseProperties("Vapor", 30, 20, 0.3, composition);
            var liquid1Phase = new PhaseProperties("Liquid1", 40, 35, 0.4, composition);
            var liquid2Phase = new PhaseProperties("Liquid2", 30, 28, 0.3, composition);

            var streamResult = new StreamResult(
                "S1", "FEED", 298.15, 500000, 100, 83,
                composition, 0.3, 0.7,
                new Dictionary<string, PhaseProperties>
                {
                    { "Vapor", vaporPhase },
                    { "Liquid1", liquid1Phase },
                    { "Liquid2", liquid2Phase }
                });

            var streamResults = new List<StreamResult> { streamResult };

            var componentBalances = new List<ComponentMassBalance>
            {
                ComponentMassBalance.Create("Water", 30, 30, 1.0),
                ComponentMassBalance.Create("Propane", 40, 40, 1.0),
                ComponentMassBalance.Create("Methane", 30, 30, 1.0)
            };

            var massBalance = MassBalanceResult.Create(100, 100, 1.0, componentBalances);

            var messages = new List<SolverMessage>
            {
                new SolverMessage(SolverMessageLevel.Info, "Calculation started", "Solver"),
                new SolverMessage(SolverMessageLevel.Info, "Converged successfully", "Solver")
            };

            // Act
            var result = CalculationResult.SuccessResult(
                convergenceStatus, timing, streamResults, massBalance, messages);

            // Assert - verify complete result structure
            Assert.True(result.Success);
            Assert.Equal(ConvergenceState.Converged, result.ConvergenceStatus.State);
            Assert.Equal(3500, result.Timing.TotalMilliseconds);
            Assert.Single(result.StreamResults);
            Assert.Equal(3, result.StreamResults[0].Phases.Count);
            Assert.True(result.MassBalance.IsValid);
            Assert.Equal(3, result.MassBalance.ComponentBalances.Count);
            Assert.All(result.MassBalance.ComponentBalances, cb => Assert.True(cb.IsValid));
            Assert.Equal(2, result.Messages.Count);
        }

        #endregion
    }
}
