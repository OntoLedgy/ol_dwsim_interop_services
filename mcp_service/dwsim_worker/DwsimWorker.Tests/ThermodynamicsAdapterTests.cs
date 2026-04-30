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
