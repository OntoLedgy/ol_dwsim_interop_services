// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using DwsimWorker.Utilities;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Utilities
{
    /// <summary>
    /// Unit tests for MassBalanceValidator static utility class.
    /// Tests overall and component-wise mass balance validation with various scenarios:
    /// balanced flows, unbalanced flows, edge cases, and tolerance handling.
    /// </summary>
    public class MassBalanceValidatorTests
    {
        #region Validate Method Tests

        [Fact]
        public void Validate_WithBalancedFlows_ReturnsValidResult()
        {
            // Arrange
            var composition = new Composition(new List<double> { 0.5, 0.5 });
            var inletStream = new StreamResult("IN", "Inlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);
            var outletStream = new StreamResult("OUT", "Outlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);

            var inlets = new List<StreamResult> { inletStream };
            var outlets = new List<StreamResult> { outletStream };

            // Act
            var result = MassBalanceValidator.Validate(inlets, outlets);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Equal(100, result.InletMolarFlow);
            Assert.Equal(100, result.OutletMolarFlow);
            Assert.Equal(0.0, result.AbsoluteError);
            Assert.Equal(0.0, result.RelativeErrorPercent);
        }

        [Fact]
        public void Validate_WithSlightImbalance_WithinTolerance_ReturnsValid()
        {
            // Arrange - 0.5% error (within 1% tolerance)
            var composition = new Composition(new List<double> { 1.0 });
            var inletStream = new StreamResult("IN", "Inlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);
            var outletStream = new StreamResult("OUT", "Outlet", 300, 101325, 100.5, 80.4, composition, 0.5, 0.5, null);

            var inlets = new List<StreamResult> { inletStream };
            var outlets = new List<StreamResult> { outletStream };

            // Act
            var result = MassBalanceValidator.Validate(inlets, outlets, 1.0);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(0.5, result.RelativeErrorPercent);
        }

        [Fact]
        public void Validate_WithImbalance_OutsideTolerance_ReturnsInvalid()
        {
            // Arrange - 5% error (outside 1% tolerance)
            var composition = new Composition(new List<double> { 1.0 });
            var inletStream = new StreamResult("IN", "Inlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);
            var outletStream = new StreamResult("OUT", "Outlet", 300, 101325, 105, 84, composition, 0.5, 0.5, null);

            var inlets = new List<StreamResult> { inletStream };
            var outlets = new List<StreamResult> { outletStream };

            // Act
            var result = MassBalanceValidator.Validate(inlets, outlets, 1.0);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(100, result.InletMolarFlow);
            Assert.Equal(105, result.OutletMolarFlow);
            Assert.Equal(5.0, result.AbsoluteError);
            Assert.Equal(5.0, result.RelativeErrorPercent);
        }

        [Fact]
        public void Validate_WithMultipleInletsAndOutlets_SumsCorrectly()
        {
            // Arrange
            var composition = new Composition(new List<double> { 1.0 });
            var inlet1 = new StreamResult("IN1", "Inlet1", 300, 101325, 60, 48, composition, 0.5, 0.5, null);
            var inlet2 = new StreamResult("IN2", "Inlet2", 300, 101325, 40, 32, composition, 0.5, 0.5, null);
            var outlet1 = new StreamResult("OUT1", "Outlet1", 300, 101325, 70, 56, composition, 0.5, 0.5, null);
            var outlet2 = new StreamResult("OUT2", "Outlet2", 300, 101325, 30, 24, composition, 0.5, 0.5, null);

            var inlets = new List<StreamResult> { inlet1, inlet2 };
            var outlets = new List<StreamResult> { outlet1, outlet2 };

            // Act
            var result = MassBalanceValidator.Validate(inlets, outlets);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(100, result.InletMolarFlow); // 60 + 40
            Assert.Equal(100, result.OutletMolarFlow); // 70 + 30
        }

        [Fact]
        public void Validate_WithCustomTolerance_UsesTolerance()
        {
            // Arrange - 2% error
            var composition = new Composition(new List<double> { 1.0 });
            var inletStream = new StreamResult("IN", "Inlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);
            var outletStream = new StreamResult("OUT", "Outlet", 300, 101325, 102, 81.6, composition, 0.5, 0.5, null);

            var inlets = new List<StreamResult> { inletStream };
            var outlets = new List<StreamResult> { outletStream };

            // Act - with 5% tolerance
            var result = MassBalanceValidator.Validate(inlets, outlets, 5.0);

            // Assert
            Assert.True(result.IsValid); // 2% error is within 5% tolerance
            Assert.Equal(2.0, result.RelativeErrorPercent);
            Assert.Equal(5.0, result.TolerancePercent);
        }

        [Fact]
        public void Validate_WithNullInletStreams_ThrowsArgumentNullException()
        {
            // Arrange
            var composition = new Composition(new List<double> { 1.0 });
            var outletStream = new StreamResult("OUT", "Outlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);
            var outlets = new List<StreamResult> { outletStream };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                MassBalanceValidator.Validate(null, outlets));
        }

        [Fact]
        public void Validate_WithNullOutletStreams_ThrowsArgumentNullException()
        {
            // Arrange
            var composition = new Composition(new List<double> { 1.0 });
            var inletStream = new StreamResult("IN", "Inlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);
            var inlets = new List<StreamResult> { inletStream };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                MassBalanceValidator.Validate(inlets, null));
        }

        [Fact]
        public void Validate_WithNegativeTolerance_ThrowsArgumentException()
        {
            // Arrange
            var composition = new Composition(new List<double> { 1.0 });
            var inletStream = new StreamResult("IN", "Inlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);
            var outletStream = new StreamResult("OUT", "Outlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);

            var inlets = new List<StreamResult> { inletStream };
            var outlets = new List<StreamResult> { outletStream };

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                MassBalanceValidator.Validate(inlets, outlets, -1.0));
        }

        [Fact]
        public void Validate_WithDefaultTolerance_Uses1Percent()
        {
            // Arrange
            var composition = new Composition(new List<double> { 1.0 });
            var inletStream = new StreamResult("IN", "Inlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);
            var outletStream = new StreamResult("OUT", "Outlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);

            var inlets = new List<StreamResult> { inletStream };
            var outlets = new List<StreamResult> { outletStream };

            // Act
            var result = MassBalanceValidator.Validate(inlets, outlets);

            // Assert
            Assert.Equal(MassBalanceValidator.DefaultTolerancePercent, result.TolerancePercent);
            Assert.Equal(1.0, result.TolerancePercent);
        }

        #endregion

        #region ValidateComponentBalances Method Tests

        [Fact]
        public void ValidateComponentBalances_WithBalancedComponents_ReturnsAllValid()
        {
            // Arrange
            var compoundNames = new List<string> { "Water", "Ethanol" };
            var composition = new Composition(new List<double> { 0.6, 0.4 });

            var inletStream = new StreamResult("IN", "Inlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);
            var outletStream = new StreamResult("OUT", "Outlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);

            var inlets = new List<StreamResult> { inletStream };
            var outlets = new List<StreamResult> { outletStream };

            // Act
            var balances = MassBalanceValidator.ValidateComponentBalances(inlets, outlets, compoundNames);

            // Assert
            Assert.NotNull(balances);
            Assert.Equal(2, balances.Count);
            Assert.All(balances, b => Assert.True(b.IsValid));
            Assert.Contains(balances, b => b.CompoundName == "Water");
            Assert.Contains(balances, b => b.CompoundName == "Ethanol");
        }

        [Fact]
        public void ValidateComponentBalances_CalculatesComponentFlowsCorrectly()
        {
            // Arrange
            var compoundNames = new List<string> { "Water", "Methane" };
            var composition = new Composition(new List<double> { 0.7, 0.3 }); // 70% Water, 30% Methane

            // 100 mol/s total, so 70 mol/s Water, 30 mol/s Methane
            var inletStream = new StreamResult("IN", "Inlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);
            var outletStream = new StreamResult("OUT", "Outlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);

            var inlets = new List<StreamResult> { inletStream };
            var outlets = new List<StreamResult> { outletStream };

            // Act
            var balances = MassBalanceValidator.ValidateComponentBalances(inlets, outlets, compoundNames);

            // Assert
            var waterBalance = balances.First(b => b.CompoundName == "Water");
            var methaneBalance = balances.First(b => b.CompoundName == "Methane");

            Assert.Equal(70.0, waterBalance.InletMoles, precision: 10);
            Assert.Equal(70.0, waterBalance.OutletMoles, precision: 10);
            Assert.Equal(30.0, methaneBalance.InletMoles, precision: 10);
            Assert.Equal(30.0, methaneBalance.OutletMoles, precision: 10);
        }

        [Fact]
        public void ValidateComponentBalances_WithImbalancedComponent_IdentifiesInvalid()
        {
            // Arrange
            var compoundNames = new List<string> { "Water", "Ethanol" };
            var inletComp = new Composition(new List<double> { 0.6, 0.4 }); // 60% Water, 40% Ethanol
            var outletComp = new Composition(new List<double> { 0.5, 0.5 }); // 50% Water, 50% Ethanol (imbalanced!)

            var inletStream = new StreamResult("IN", "Inlet", 300, 101325, 100, 80, inletComp, 0.5, 0.5, null);
            var outletStream = new StreamResult("OUT", "Outlet", 300, 101325, 100, 80, outletComp, 0.5, 0.5, null);

            var inlets = new List<StreamResult> { inletStream };
            var outlets = new List<StreamResult> { outletStream };

            // Act
            var balances = MassBalanceValidator.ValidateComponentBalances(inlets, outlets, compoundNames, 1.0);

            // Assert
            var waterBalance = balances.First(b => b.CompoundName == "Water");
            var ethanolBalance = balances.First(b => b.CompoundName == "Ethanol");

            // Water: 60 mol/s in, 50 mol/s out = 16.67% error (invalid)
            // Ethanol: 40 mol/s in, 50 mol/s out = 25% error (invalid)
            Assert.False(waterBalance.IsValid);
            Assert.False(ethanolBalance.IsValid);
        }

        [Fact]
        public void ValidateComponentBalances_WithNullInputs_ReturnsEmptyList()
        {
            // Arrange
            var compoundNames = new List<string> { "Water" };

            // Act
            var balances = MassBalanceValidator.ValidateComponentBalances(null, null, compoundNames);

            // Assert
            Assert.NotNull(balances);
            Assert.Empty(balances);
        }

        [Fact]
        public void ValidateComponentBalances_WithEmptyCompoundNames_ReturnsEmptyList()
        {
            // Arrange
            var composition = new Composition(new List<double> { 1.0 });
            var inletStream = new StreamResult("IN", "Inlet", 300, 101325, 100, 80, composition, 0.5, 0.5, null);
            var outlets = new List<StreamResult> { inletStream };

            // Act
            var balances = MassBalanceValidator.ValidateComponentBalances(
                new List<StreamResult> { inletStream },
                outlets,
                new List<string>());

            // Assert
            Assert.NotNull(balances);
            Assert.Empty(balances);
        }

        #endregion

        #region IsWithinTolerance Method Tests

        [Fact]
        public void IsWithinTolerance_WithPerfectMatch_ReturnsTrue()
        {
            // Act
            var result = MassBalanceValidator.IsWithinTolerance(100.0, 100.0, 1.0);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsWithinTolerance_WithinTolerance_ReturnsTrue()
        {
            // Arrange - 0.5% error, 1% tolerance
            double inlet = 100.0;
            double outlet = 100.5;

            // Act
            var result = MassBalanceValidator.IsWithinTolerance(inlet, outlet, 1.0);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsWithinTolerance_OutsideTolerance_ReturnsFalse()
        {
            // Arrange - 5% error, 1% tolerance
            double inlet = 100.0;
            double outlet = 105.0;

            // Act
            var result = MassBalanceValidator.IsWithinTolerance(inlet, outlet, 1.0);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsWithinTolerance_BothZero_ReturnsTrue()
        {
            // Act
            var result = MassBalanceValidator.IsWithinTolerance(0.0, 0.0, 1.0);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsWithinTolerance_InletZeroOutletNonZero_ReturnsFalse()
        {
            // Act
            var result = MassBalanceValidator.IsWithinTolerance(0.0, 10.0, 1.0);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsWithinTolerance_WithNegativeValues_ReturnsFalse()
        {
            // Act
            var result1 = MassBalanceValidator.IsWithinTolerance(-100.0, 100.0, 1.0);
            var result2 = MassBalanceValidator.IsWithinTolerance(100.0, -100.0, 1.0);

            // Assert
            Assert.False(result1);
            Assert.False(result2);
        }

        [Fact]
        public void IsWithinTolerance_AtExactToleranceLimit_ReturnsTrue()
        {
            // Arrange - exactly 1% error
            double inlet = 100.0;
            double outlet = 101.0;

            // Act
            var result = MassBalanceValidator.IsWithinTolerance(inlet, outlet, 1.0);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region CalculateRelativeError Method Tests

        [Fact]
        public void CalculateRelativeError_WithPerfectMatch_ReturnsZero()
        {
            // Act
            var error = MassBalanceValidator.CalculateRelativeError(100.0, 100.0);

            // Assert
            Assert.Equal(0.0, error);
        }

        [Fact]
        public void CalculateRelativeError_With5PercentError_Returns5()
        {
            // Arrange
            double inlet = 100.0;
            double outlet = 105.0;

            // Act
            var error = MassBalanceValidator.CalculateRelativeError(inlet, outlet);

            // Assert
            Assert.Equal(5.0, error);
        }

        [Fact]
        public void CalculateRelativeError_WithDeficit_ReturnsPositiveError()
        {
            // Arrange - 10% deficit
            double inlet = 100.0;
            double outlet = 90.0;

            // Act
            var error = MassBalanceValidator.CalculateRelativeError(inlet, outlet);

            // Assert
            Assert.Equal(10.0, error);
        }

        [Fact]
        public void CalculateRelativeError_BothZero_ReturnsZero()
        {
            // Act
            var error = MassBalanceValidator.CalculateRelativeError(0.0, 0.0);

            // Assert
            Assert.Equal(0.0, error);
        }

        [Fact]
        public void CalculateRelativeError_InletZeroOutletNonZero_ReturnsInfinity()
        {
            // Act
            var error = MassBalanceValidator.CalculateRelativeError(0.0, 10.0);

            // Assert
            Assert.Equal(double.PositiveInfinity, error);
        }

        [Fact]
        public void CalculateRelativeError_WithNegativeInlet_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                MassBalanceValidator.CalculateRelativeError(-100.0, 100.0));
        }

        [Fact]
        public void CalculateRelativeError_WithNegativeOutlet_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                MassBalanceValidator.CalculateRelativeError(100.0, -100.0));
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void MassBalanceValidator_CompleteWorkflow_ThreePhaseSeparator()
        {
            // Arrange - simulate three-phase separator with one inlet, three outlets
            var compoundNames = new List<string> { "Water", "Propane", "Methane" };

            // Inlet: 100 mol/s total (30% Water, 40% Propane, 30% Methane)
            var inletComp = new Composition(new List<double> { 0.3, 0.4, 0.3 });
            var inletStream = new StreamResult("FEED", "Feed", 298.15, 500000, 100, 83, inletComp, 0.3, 0.7, null);

            // Vapor outlet: 30 mol/s (10% Water, 50% Propane, 40% Methane)
            var vaporComp = new Composition(new List<double> { 0.1, 0.5, 0.4 });
            var vaporStream = new StreamResult("VAPOR", "Vapor", 298.15, 500000, 30, 20, vaporComp, 1.0, 0.0, null);

            // Liquid1 outlet: 40 mol/s (30% Water, 45% Propane, 25% Methane)
            var liquid1Comp = new Composition(new List<double> { 0.3, 0.45, 0.25 });
            var liquid1Stream = new StreamResult("LIQ1", "Liquid1", 298.15, 500000, 40, 35, liquid1Comp, 0.0, 1.0, null);

            // Liquid2 outlet: 30 mol/s (50% Water, 20% Propane, 30% Methane)
            var liquid2Comp = new Composition(new List<double> { 0.5, 0.2, 0.3 });
            var liquid2Stream = new StreamResult("LIQ2", "Liquid2", 298.15, 500000, 30, 28, liquid2Comp, 0.0, 1.0, null);

            var inlets = new List<StreamResult> { inletStream };
            var outlets = new List<StreamResult> { vaporStream, liquid1Stream, liquid2Stream };

            // Act - validate overall and component balances
            var overallBalance = MassBalanceValidator.Validate(inlets, outlets);
            var componentBalances = MassBalanceValidator.ValidateComponentBalances(inlets, outlets, compoundNames);

            // Assert - overall balance
            Assert.True(overallBalance.IsValid);
            Assert.Equal(100, overallBalance.InletMolarFlow);
            Assert.Equal(100, overallBalance.OutletMolarFlow); // 30 + 40 + 30

            // Assert - component balances
            Assert.Equal(3, componentBalances.Count);

            // Water: Inlet = 30, Outlets = 3 + 12 + 15 = 30 ✓
            var waterBalance = componentBalances.First(b => b.CompoundName == "Water");
            Assert.True(waterBalance.IsValid);
            Assert.Equal(30.0, waterBalance.InletMoles, precision: 5);
            Assert.Equal(30.0, waterBalance.OutletMoles, precision: 5);

            // Propane: Inlet = 40, Outlets = 15 + 18 + 6 = 39 (close enough within tolerance)
            var propaneBalance = componentBalances.First(b => b.CompoundName == "Propane");
            Assert.Equal(40.0, propaneBalance.InletMoles, precision: 5);
            Assert.Equal(39.0, propaneBalance.OutletMoles, precision: 5);

            // Methane: Inlet = 30, Outlets = 12 + 10 + 9 = 31 (close enough within tolerance)
            var methaneBalance = componentBalances.First(b => b.CompoundName == "Methane");
            Assert.Equal(30.0, methaneBalance.InletMoles, precision: 5);
            Assert.Equal(31.0, methaneBalance.OutletMoles, precision: 5);
        }

        #endregion
    }
}
