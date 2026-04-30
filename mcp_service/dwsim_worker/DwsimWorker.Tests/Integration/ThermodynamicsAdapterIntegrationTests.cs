// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// This file is part of the OntoLedgy Thermodynamics Architecture and is
// dual-licensed:
//
//   1. Open source under the GNU Affero General Public License v3.0 or
//      later (AGPL-3.0-or-later). See the LICENSE file in the repository
//      root for the full licence text and NOTICE for attribution.
//   2. Commercial under a separate proprietary licence offered by
//      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Serilog;
using Serilog.Core;
using Xunit;
using Xunit.Abstractions;
using DwsimWorker.Engine;
using DwsimWorker.Adapters;

namespace DwsimWorker.Tests.Integration
{
    /// <summary>
    /// Integration tests for ThermodynamicsAdapter flash calculations.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Category", "Thermodynamics")]
    public class ThermodynamicsAdapterIntegrationTests : IDisposable
    {
        private readonly Logger _logger;
        private readonly ITestOutputHelper _output;
        private FlowsheetContext _context;
        private ThermodynamicsAdapter _thermodynamicsAdapter;

        public ThermodynamicsAdapterIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.TestOutput(output, Serilog.Events.LogEventLevel.Debug)
                .CreateLogger();
        }

        public void Dispose()
        {
            _context?.Dispose();
            _logger?.Dispose();
        }

        private bool SetupContext()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                _output.WriteLine("DWSIM assemblies not found, skipping test");
                return false;
            }

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .WithFlowsheetName("ThermodynamicsAdapter Integration Test")
                .Build();

            _context = new FlowsheetContext(_logger, config);
            _context.Initialize();

            // Add compound
            var compoundAdapter = new CompoundAdapter(_logger, _context);
            var addResult = compoundAdapter.AddCompound("Methane");
            _output.WriteLine($"AddCompound result: Success={addResult.Success}, Message={addResult.Message}");

            // Verify compound was added
            var compounds = _context.GetCompounds();
            _output.WriteLine($"Context compounds after AddCompound: {string.Join(", ", compounds)}");

            // Set property package
            var propertyPackageAdapter = new PropertyPackageAdapter(_logger, _context);
            var ppResult = propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");
            _output.WriteLine($"SetPropertyPackage result: Success={ppResult.Success}, Message={ppResult.Message}");

            // Verify property package was set
            var pp = _context.GetPropertyPackage();
            _output.WriteLine($"Context property package: {(pp != null ? pp.GetType().Name : "NULL")}");

            _thermodynamicsAdapter = new ThermodynamicsAdapter(_logger, _context);

            return true;
        }

        [Fact]
        public void FlashTP_SingleComponent_ShouldConverge()
        {
            if (!SetupContext())
            {
                return; // Skip - DWSIM not available
            }

            _output.WriteLine("=== Starting FlashTP Test ===");
            _output.WriteLine($"Compounds: Methane");
            _output.WriteLine($"Composition: 1.0");
            _output.WriteLine($"Temperature: 200 K");
            _output.WriteLine($"Pressure: 101325 Pa");

            var result = _thermodynamicsAdapter.FlashTP(
                compounds: new[] { "Methane" },
                composition: new[] { 1.0 },
                temperature: 200.0,
                pressure: 101325.0);

            _output.WriteLine($"=== FlashTP Result ===");
            _output.WriteLine($"Converged: {result.Converged}");
            _output.WriteLine($"CalculationType: {result.CalculationType}");
            _output.WriteLine($"TemperatureK: {result.TemperatureK}");
            _output.WriteLine($"PressurePa: {result.PressurePa}");
            _output.WriteLine($"Phase Count: {result.Phases?.Count ?? 0}");

            if (result.Phases != null)
            {
                foreach (var phase in result.Phases)
                {
                    _output.WriteLine($"  Phase: {phase.PhaseLabel}, Fraction: {phase.PhaseFraction}");
                }
            }

            Assert.True(result.Converged, "Flash calculation should converge for single-component system");
            Assert.NotNull(result.Phases);
            Assert.True(result.Phases.Count >= 1, "At least one phase should be present");
        }

        [Fact]
        public void FlashTP_WithoutPropertyPackage_ShouldFail()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                _output.WriteLine("DWSIM assemblies not found, skipping test");
                return;
            }

            // Create context without setting property package
            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .WithFlowsheetName("ThermodynamicsAdapter No PP Test")
                .Build();

            using var context = new FlowsheetContext(_logger, config);
            context.Initialize();

            // Add compound but NOT property package
            var compoundAdapter = new CompoundAdapter(_logger, context);
            compoundAdapter.AddCompound("Methane");

            var adapter = new ThermodynamicsAdapter(_logger, context);

            var result = adapter.FlashTP(
                compounds: new[] { "Methane" },
                composition: new[] { 1.0 },
                temperature: 200.0,
                pressure: 101325.0);

            _output.WriteLine($"Converged: {result.Converged}");
            _output.WriteLine($"Phase Count: {result.Phases?.Count ?? 0}");

            // Should fail because no property package is set
            Assert.False(result.Converged, "Flash should fail without property package");
        }

        /// <summary>
        /// Tests flash calculation at VLE conditions to verify two-phase behavior.
        /// Uses a methane/ethane mixture at conditions within the two-phase envelope.
        /// At 180 K and 20 bar (2 MPa), a 50/50 methane/ethane mixture should show VLE.
        /// </summary>
        [Fact]
        public void FlashTP_BinaryMixture_ShouldShowTwoPhases()
        {
            if (!TestConfiguration.ValidateDwsimPath())
            {
                _output.WriteLine("DWSIM assemblies not found, skipping test");
                return;
            }

            var config = new FlowsheetContextConfigBuilder()
                .WithAssemblyPath(TestConfiguration.DwsimAssemblyPath)
                .WithValidation(false)
                .WithFlowsheetName("VLE Two-Phase Test")
                .Build();

            using var context = new FlowsheetContext(_logger, config);
            context.Initialize();

            // Add compounds
            var compoundAdapter = new CompoundAdapter(_logger, context);
            var addResult1 = compoundAdapter.AddCompound("Methane");
            var addResult2 = compoundAdapter.AddCompound("Ethane");
            _output.WriteLine($"AddCompound Methane: Success={addResult1.Success}");
            _output.WriteLine($"AddCompound Ethane: Success={addResult2.Success}");

            // Set property package
            var propertyPackageAdapter = new PropertyPackageAdapter(_logger, context);
            var ppResult = propertyPackageAdapter.SetPropertyPackage("Peng-Robinson");
            _output.WriteLine($"SetPropertyPackage: Success={ppResult.Success}");

            var adapter = new ThermodynamicsAdapter(_logger, context);

            // VLE conditions for methane/ethane:
            // At 200 K and 5 bar (500,000 Pa), a 50/50 methane/ethane mixture
            // should show vapor-liquid equilibrium since:
            // - Methane boiling point ~111 K at 1 atm (will be mostly vapor at 200K)
            // - Ethane boiling point ~184 K at 1 atm (will be partially condensed)
            // - 5 bar pressure ensures some liquid phase exists
            var temperature = 200.0; // K (above ethane boiling point but with pressure ensures VLE)
            var pressure = 500000.0; // Pa (5 bar)
            var compounds = new[] { "Methane", "Ethane" };
            var composition = new[] { 0.5, 0.5 };

            _output.WriteLine("=== Starting VLE Flash Test ===");
            _output.WriteLine($"Compounds: {string.Join(", ", compounds)}");
            _output.WriteLine($"Composition: {string.Join(", ", composition)}");
            _output.WriteLine($"Temperature: {temperature} K");
            _output.WriteLine($"Pressure: {pressure} Pa ({pressure / 100000.0} bar)");

            var result = adapter.FlashTP(
                compounds: compounds,
                composition: composition,
                temperature: temperature,
                pressure: pressure);

            _output.WriteLine($"=== VLE Flash Result ===");
            _output.WriteLine($"Converged: {result.Converged}");
            _output.WriteLine($"CalculationType: {result.CalculationType}");
            _output.WriteLine($"TemperatureK: {result.TemperatureK}");
            _output.WriteLine($"PressurePa: {result.PressurePa}");
            _output.WriteLine($"Phase Count: {result.Phases?.Count ?? 0}");

            if (result.Phases != null)
            {
                foreach (var phase in result.Phases)
                {
                    _output.WriteLine($"  Phase: {phase.PhaseLabel}");
                    _output.WriteLine($"    Fraction: {phase.PhaseFraction:F4}");
                    if (phase.Composition != null)
                    {
                        foreach (var comp in phase.Composition)
                        {
                            _output.WriteLine($"    {comp.Compound}: x={comp.MoleFraction:F4}");
                        }
                    }
                    if (phase.Properties != null)
                    {
                        foreach (var prop in phase.Properties)
                        {
                            _output.WriteLine($"    {prop.Key}: {prop.Value:G4}");
                        }
                    }
                }
            }

            Assert.True(result.Converged, "Flash calculation should converge for binary mixture");
            Assert.NotNull(result.Phases);
            Assert.True(result.Phases.Count >= 2, $"Expected two phases (VLE), but got {result.Phases.Count} phase(s)");

            // Verify we have both vapor and liquid phases
            var hasVapor = result.Phases.Exists(p => p.PhaseLabel.Contains("Vapor"));
            var hasLiquid = result.Phases.Exists(p => p.PhaseLabel.Contains("Liquid"));
            Assert.True(hasVapor, "Should have a vapor phase");
            Assert.True(hasLiquid, "Should have a liquid phase");

            _output.WriteLine("=== VLE Test PASSED - Two phases detected ===");
        }
    }
}
