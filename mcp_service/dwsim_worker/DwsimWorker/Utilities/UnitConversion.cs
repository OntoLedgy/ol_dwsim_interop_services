using System;

namespace DwsimWorker.Utilities
{
    /// <summary>
    /// Provides unit conversion helpers to normalize values to SI units.
    /// </summary>
    public static class UnitConversion
    {
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
