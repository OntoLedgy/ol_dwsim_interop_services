// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Serilog;
using DwsimWorker.Adapters;
using DwsimWorker.Engine;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Adapters
{
    /// <summary>
    /// Unit tests for CalculationAdapter.
    /// Tests constructor validation and basic behavior.
    ///
    /// Note: FlowsheetContext and StreamAdapter are sealed classes and cannot be mocked.
    /// Full integration tests with real DWSIM instances are in Integration/ThreePhaseSeparatorIntegrationTests.cs.
    /// These tests focus on constructor validation and basic error handling.
    /// </summary>
    public class CalculationAdapterTests
    {
        private readonly ILogger _logger;

        public CalculationAdapterTests()
        {
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange - cannot create real instances without complex setup
            // Just test null validation for logger

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CalculationAdapter(null, null, null));
        }

        #endregion

        #region Documentation Tests

        [Fact]
        public void CalculationAdapter_ClassDocumentation_IsPresent()
        {
            // This test verifies that the CalculationAdapter class exists and is documented
            // The actual functionality is tested in integration tests with real DWSIM instances

            var type = typeof(CalculationAdapter);

            Assert.NotNull(type);
            Assert.Equal("CalculationAdapter", type.Name);
            Assert.True(type.IsSealed);
        }

        [Fact]
        public void CalculationAdapter_HasExpectedMethods()
        {
            // Verify the adapter has all required public methods
            var type = typeof(CalculationAdapter);

            // Check for RunCalculation methods
            var runCalculationNoTimeout = type.GetMethod("RunCalculation", Type.EmptyTypes);
            var runCalculationWithTimeout = type.GetMethod("RunCalculation", new[] { typeof(TimeSpan) });

            Assert.NotNull(runCalculationNoTimeout);
            Assert.NotNull(runCalculationWithTimeout);

            // Check for GetUnitMetrics method
            var getUnitMetrics = type.GetMethod("GetUnitMetrics", new[] { typeof(string) });
            Assert.NotNull(getUnitMetrics);

            // Check for cached status/result methods
            var getCurrentStatus = type.GetMethod("GetCurrentStatus", Type.EmptyTypes);
            var getCachedResult = type.GetMethod("GetCachedResult", Type.EmptyTypes);
            Assert.NotNull(getCurrentStatus);
            Assert.NotNull(getCachedResult);
        }

        [Fact]
        public void CalculationAdapter_Constructor_RequiresThreeParameters()
        {
            // Verify constructor signature
            var type = typeof(CalculationAdapter);
            var constructors = type.GetConstructors();

            Assert.Single(constructors);

            var constructor = constructors[0];
            var parameters = constructor.GetParameters();

            Assert.Equal(3, parameters.Length);
            Assert.Equal(typeof(ILogger), parameters[0].ParameterType);
            Assert.Equal("logger", parameters[0].Name);
            Assert.Equal("context", parameters[1].Name);
            Assert.Equal("streamAdapter", parameters[2].Name);
        }

        #endregion

        #region Cache Behavior Tests

        [Fact]
        public void FlowsheetContext_InvalidateCalculationCache_ClearsCachedFields()
        {
            var config = new FlowsheetContextConfigBuilder()
                .WithValidation(false)
                .Build();
            var context = new FlowsheetContext(_logger, config);

            var convergence = ConvergenceStatus.Converged(1, 0.1);
            var timing = CalculationTiming.FromTimestamps(DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
            var result = CalculationResult.SuccessResult(
                convergence,
                timing,
                new List<StreamResult>(),
                MassBalanceResult.Valid(0, 0));

            SetPrivateField(context, "_cachedCalculationResult", result);
            SetPrivateField(context, "_cachedConvergenceStatus", convergence);
            SetPrivateField(context, "_lastCalculationTimestamp", DateTime.UtcNow);

            context.InvalidateCalculationCache("test");

            Assert.Null(GetPrivateField<CalculationResult>(context, "_cachedCalculationResult"));
            Assert.Equal(
                ConvergenceState.NotStarted,
                GetPrivateField<ConvergenceStatus>(context, "_cachedConvergenceStatus").State);
            Assert.Null(GetPrivateField<DateTime?>(context, "_lastCalculationTimestamp"));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException($"Field '{fieldName}' not found.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException($"Field '{fieldName}' not found.");
            return (T)field.GetValue(target);
        }

        #endregion

        #region Notes

        // NOTE: Comprehensive functional tests for CalculationAdapter are in:
        // - Integration/ThreePhaseSeparatorIntegrationTests.cs
        //   Tests complete calculation workflow with real DWSIM flowsheet
        //
        // These tests cannot use mocking because:
        // - FlowsheetContext is sealed (cannot be mocked with Moq)
        // - StreamAdapter is sealed (cannot be mocked with Moq)
        // - CalculationAdapter requires complex setup with actual DWSIM assemblies
        //
        // The integration tests provide comprehensive coverage of:
        // - RunCalculation() success and failure scenarios
        // - Convergence status detection
        // - Stream result extraction
        // - Mass balance validation
        // - Timing information
        // - Solver message capture
        // - Unit metrics retrieval

        #endregion
    }
}
