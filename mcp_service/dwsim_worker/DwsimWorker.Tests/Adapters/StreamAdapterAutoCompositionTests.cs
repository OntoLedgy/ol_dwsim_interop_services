using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Moq;
using Serilog;
using Xunit;
using DwsimWorker.Adapters;
using DwsimWorker.Engine;
using DwsimWorker.Exceptions;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Adapters
{
    public class StreamAdapterAutoCompositionTests
    {
        private const double SumTolerance = 1e-9;

        public class OutletAutoCompositionTests
        {
            [Theory]
            [InlineData(1)]
            [InlineData(3)]
            [InlineData(10)]
            public void CreateStream_Outlet_NoComposition_AutoGeneratesEqualFractions(int compoundCount)
            {
                var adapter = CreateAdapter(compoundCount, out var context);
                using (context)
                {
                    var properties = CreateStreamPropertiesWithEmptyComposition();

                    var result = adapter.CreateStream("Outlet", properties);

                    Assert.True(result.Success, $"Expected success but got: {result.Message}");
                    Assert.True(context.TryGetStreamProperties(result.ObjectId, out var cached));
                    var fractions = cached.Composition.MoleFractions;

                    Assert.Equal(compoundCount, fractions.Count);
                    AssertCompositionSumsToOne(fractions);
                    AssertEqualFractions(fractions, compoundCount);
                }
            }
        }

        public class OutletExplicitCompositionTests
        {
            [Fact]
            public void CreateStream_Outlet_WithExplicitComposition_UsesProvidedFractions()
            {
                var adapter = CreateAdapter(3, out var context);
                using (context)
                {
                    var properties = CreateStreamProperties(new[] { 0.2, 0.3, 0.5 });

                    var result = adapter.CreateStream("Outlet", properties);

                    Assert.True(result.Success, $"Expected success but got: {result.Message}");
                    Assert.True(context.TryGetStreamProperties(result.ObjectId, out var cached));
                    var fractions = cached.Composition.MoleFractions;

                    Assert.Equal(3, fractions.Count);
                    Assert.True(Math.Abs(fractions[0] - 0.2) < SumTolerance);
                    Assert.True(Math.Abs(fractions[1] - 0.3) < SumTolerance);
                    Assert.True(Math.Abs(fractions[2] - 0.5) < SumTolerance);
                    AssertCompositionSumsToOne(fractions);
                }
            }
        }

        public class AutoCompositionErrorCases
        {
            [Fact]
            public void CreateStream_Outlet_NoCompounds_ReturnsFailure()
            {
                var adapter = CreateAdapter(0, out var context);
                using (context)
                {
                    var properties = CreateStreamPropertiesWithEmptyComposition();

                    var result = adapter.CreateStream("Outlet", properties);

                    Assert.False(result.Success);
                    Assert.Contains("Cannot auto-generate composition", result.Message);
                    Assert.IsType<InvalidOperationException>(result.Error);
                }
            }

            [Fact]
            public void CreateStream_Feed_NoComposition_ReturnsFailure()
            {
                var adapter = CreateAdapter(3, out var context);
                using (context)
                {
                    var properties = CreateStreamPropertiesWithEmptyComposition();

                    var result = adapter.CreateStream("Feed", properties, isSource: true);

                    Assert.False(result.Success);
                    Assert.Contains("Invalid stream properties", result.Message);
                    Assert.IsType<InvalidPropertyValueException>(result.Error);
                }
            }
        }

        private static StreamAdapter CreateAdapter(int compoundCount, out FlowsheetContext context)
        {
            var logger = new Mock<ILogger>();
            var config = new FlowsheetContextConfigBuilder()
                .WithValidation(false)
                .Build();

            context = new FlowsheetContext(logger.Object, config);
            InitializeContextForTests(context);
            AddCompounds(context, compoundCount);

            return new StreamAdapter(logger.Object, context);
        }

        private static void InitializeContextForTests(FlowsheetContext context)
        {
            var type = typeof(FlowsheetContext);
            type.GetField("_flowsheet", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(context, new object());
            type.GetField("_isInitialized", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(context, true);
        }

        private static void AddCompounds(FlowsheetContext context, int compoundCount)
        {
            for (var i = 0; i < compoundCount; i++)
            {
                context.AddCompound($"Compound{i + 1}");
            }
        }

        private static StreamProperties CreateStreamProperties(IReadOnlyList<double> fractions)
        {
            var kelvinUnit = new UnitsOfMeasure("K", typeof(Temperature),
                new Ranges(0, double.PositiveInfinity));
            var pascalUnit = new UnitsOfMeasure("Pa", typeof(Pressure),
                new Ranges(0, double.PositiveInfinity));
            var molPerSecUnit = new UnitsOfMeasure("mol/s", typeof(MolarFlowRate),
                new Ranges(0, double.PositiveInfinity));

            var tempMeasurement = new Measurements(new Temperature(), 298.15, kelvinUnit);
            var pressMeasurement = new Measurements(new Pressure(), 101325, pascalUnit);
            var flowMeasurement = new Measurements(new MolarFlowRate(), 1.0, molPerSecUnit);

            var tempProperty = new PhysicalProperties("Temperature", tempMeasurement);
            var pressProperty = new PhysicalProperties("Pressure", pressMeasurement);
            var flowProperty = new PhysicalProperties("MolarFlow", flowMeasurement);

            var composition = new Composition(fractions);

            return new StreamProperties(tempProperty, pressProperty, flowProperty, composition);
        }

        private static StreamProperties CreateStreamPropertiesWithEmptyComposition()
        {
            var properties = CreateStreamProperties(new[] { 1.0 });
            var fractions = properties.Composition.MoleFractions as List<double>;
            if (fractions == null)
            {
                throw new InvalidOperationException("Expected mutable composition list for test setup.");
            }

            // Clear fractions to simulate missing composition input before auto-generation.
            fractions.Clear();

            return properties;
        }

        private static void AssertCompositionSumsToOne(IReadOnlyList<double> fractions)
        {
            var sum = fractions.Sum();
            Assert.True(Math.Abs(sum - 1.0) < SumTolerance, $"Expected sum to be 1.0 but got {sum}");
        }

        private static void AssertEqualFractions(IReadOnlyList<double> fractions, int compoundCount)
        {
            var expected = 1.0 / compoundCount;
            var lastIndex = compoundCount - 1;

            for (var i = 0; i < compoundCount; i++)
            {
                var expectedValue = i == lastIndex
                    ? 1.0 - (expected * lastIndex)
                    : expected;

                Assert.True(
                    Math.Abs(fractions[i] - expectedValue) < SumTolerance,
                    $"Expected fraction {expectedValue} at index {i} but got {fractions[i]}");
            }
        }
    }
}
