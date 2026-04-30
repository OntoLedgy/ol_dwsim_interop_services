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

namespace DwsimWorker.Utilities
{
    /// <summary>
    /// Provides unit conversion helpers to normalize values to SI units.
    /// </summary>
    public static class UnitConversion
    {
        /// <summary>
        /// Converts a temperature value to Kelvin.
        /// </summary>
        /// <param name="value">Temperature value.</param>
        /// <param name="unit">Unit label (e.g., K, C, F).</param>
        /// <returns>Temperature in Kelvin.</returns>
        /// <exception cref="UnitConversionException">Thrown when the unit is unsupported.</exception>
        public static double TemperatureToKelvin(double value, string unit)
        {
            switch (NormalizeUnit(unit))
            {
                case "k":
                case "kelvin":
                    return value;
                case "c":
                case "celsius":
                case "degc":
                    return value + 273.15;
                case "f":
                case "fahrenheit":
                case "degf":
                    return (value - 32.0) * (5.0 / 9.0) + 273.15;
                default:
                    throw new UnitConversionException($"Unsupported temperature unit '{unit}'.");
            }
        }

        /// <summary>
        /// Converts a pressure value to Pascals.
        /// </summary>
        /// <param name="value">Pressure value.</param>
        /// <param name="unit">Unit label (e.g., Pa, kPa, bar).</param>
        /// <returns>Pressure in Pascals.</returns>
        /// <exception cref="UnitConversionException">Thrown when the unit is unsupported.</exception>
        public static double PressureToPascal(double value, string unit)
        {
            switch (NormalizeUnit(unit))
            {
                case "pa":
                case "pascal":
                    return value;
                case "kpa":
                    return value * 1_000.0;
                case "mpa":
                    return value * 1_000_000.0;
                case "bar":
                    return value * 100_000.0;
                case "atm":
                    return value * 101_325.0;
                case "psi":
                    return value * 6_894.757293;
                default:
                    throw new UnitConversionException($"Unsupported pressure unit '{unit}'.");
            }
        }

        /// <summary>
        /// Converts a molar flow value to mol/s.
        /// </summary>
        /// <param name="value">Molar flow value.</param>
        /// <param name="unit">Unit label (e.g., mol/s, mol/h, kmol/h).</param>
        /// <returns>Molar flow in mol/s.</returns>
        /// <exception cref="UnitConversionException">Thrown when the unit is unsupported.</exception>
        public static double MolarFlowToMolPerSecond(double value, string unit)
        {
            switch (NormalizeUnit(unit))
            {
                case "mol/s":
                case "mol/sec":
                case "mole/s":
                    return value;
                case "mol/min":
                    return value / 60.0;
                case "mol/h":
                    return value / 3_600.0;
                case "kmol/s":
                    return value * 1_000.0;
                case "kmol/h":
                    return value * 1_000.0 / 3_600.0;
                default:
                    throw new UnitConversionException($"Unsupported molar flow unit '{unit}'.");
            }
        }

        private static string NormalizeUnit(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                throw new UnitConversionException("Unit cannot be null or empty.");
            }

            return unit.Trim().ToLowerInvariant();
        }
    }
}
