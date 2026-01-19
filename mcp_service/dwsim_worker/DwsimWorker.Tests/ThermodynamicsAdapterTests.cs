using System;
using System.Reflection;
using Xunit;
using DwsimWorker.Adapters;

namespace DwsimWorker.Tests
{
    public class ThermodynamicsAdapterTests
    {
        [Fact]
        public void ValidateInputs_WithNullCompounds_ReturnsFalse()
        {
            var (isValid, message) = InvokeValidateInputs(
                calculationType: "TP",
                compounds: null,
                composition: new[] { 1.0 },
                temperatureK: 300.0,
                pressurePa: 101325.0,
                enthalpy: null,
                entropy: null);

            Assert.False(isValid);
            Assert.Contains("Compounds", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateInputs_WithEmptyCompounds_ReturnsFalse()
        {
            var (isValid, message) = InvokeValidateInputs(
                calculationType: "TP",
                compounds: Array.Empty<string>(),
                composition: new[] { 1.0 },
                temperatureK: 300.0,
                pressurePa: 101325.0,
                enthalpy: null,
                entropy: null);

            Assert.False(isValid);
            Assert.Contains("Compounds", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateInputs_WithInvalidComposition_ReturnsFalse()
        {
            var (isValid, message) = InvokeValidateInputs(
                calculationType: "TP",
                compounds: new[] { "water", "ethanol" },
                composition: new[] { 0.6, 0.6 },
                temperatureK: 300.0,
                pressurePa: 101325.0,
                enthalpy: null,
                entropy: null);

            Assert.False(isValid);
            Assert.Contains("Mole fractions", message, StringComparison.OrdinalIgnoreCase);
        }

        private static (bool isValid, string message) InvokeValidateInputs(
            string calculationType,
            string[] compounds,
            double[] composition,
            double? temperatureK,
            double pressurePa,
            double? enthalpy,
            double? entropy)
        {
            var method = typeof(ThermodynamicsAdapter).GetMethod(
                "ValidateInputs",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                throw new InvalidOperationException("ValidateInputs method not found.");
            }

            var args = new object[]
            {
                calculationType,
                compounds,
                composition,
                temperatureK,
                pressurePa,
                enthalpy,
                entropy,
                null
            };

            var result = (bool)method.Invoke(null, args);
            var message = args[7] as string;

            return (result, message);
        }
    }
}
