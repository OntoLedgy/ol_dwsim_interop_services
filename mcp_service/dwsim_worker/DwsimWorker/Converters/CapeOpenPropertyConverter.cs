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
using System.Collections.Generic;
using System.Linq;

namespace DwsimWorker.Converters
{
    /// <summary>
    /// Static utility class for converting between user-friendly property names and CAPE-OPEN standard names.
    /// Provides bidirectional mapping and validation for thermodynamic properties.
    /// </summary>
    /// <remarks>
    /// CAPE-OPEN (Computer Aided Process Engineering - Open Simulation Environment) defines standard
    /// property names for thermodynamic calculations. This converter maps user-friendly names to
    /// CAPE-OPEN compliant names for use with ICapeThermoMaterialObject interfaces.
    ///
    /// Supported Properties:
    /// - temperature: Absolute temperature in Kelvin (K)
    /// - pressure: Absolute pressure in Pascals (Pa)
    /// - molarFlow: Total molar flow rate in mol/s
    /// - composition: Mole fractions (dimensionless, sum = 1.0)
    /// </remarks>
    public static class CapeOpenPropertyConverter
    {
        /// <summary>
        /// Mapping from user-friendly property names to CAPE-OPEN standard names.
        /// Key: user-friendly name (lowercase for case-insensitive matching)
        /// Value: CAPE-OPEN standard name
        /// </summary>
        private static readonly Dictionary<string, string> UserFriendlyToCapeOpen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "temperature", "temperature" },
            { "pressure", "pressure" },
            { "molarflow", "totalFlow" },
            { "composition", "composition" }
        };

        /// <summary>
        /// Mapping from CAPE-OPEN standard names to user-friendly names.
        /// Key: CAPE-OPEN standard name (lowercase for case-insensitive matching)
        /// Value: user-friendly name
        /// </summary>
        private static readonly Dictionary<string, string> CapeOpenToUserFriendly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "temperature", "temperature" },
            { "pressure", "pressure" },
            { "totalflow", "molarFlow" },
            { "composition", "composition" }
        };

        /// <summary>
        /// Converts a user-friendly property name to its CAPE-OPEN standard name.
        /// Conversion is case-insensitive.
        /// </summary>
        /// <param name="userFriendlyName">The user-friendly property name (e.g., "Temperature", "molarFlow").</param>
        /// <returns>The CAPE-OPEN standard property name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when userFriendlyName is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the property name is not supported.</exception>
        /// <example>
        /// <code>
        /// string capeOpenName = CapeOpenPropertyConverter.ToCapeOpenName("temperature");
        /// // Returns: "temperature"
        ///
        /// string capeOpenName2 = CapeOpenPropertyConverter.ToCapeOpenName("molarFlow");
        /// // Returns: "totalFlow"
        /// </code>
        /// </example>
        public static string ToCapeOpenName(string userFriendlyName)
        {
            if (userFriendlyName == null)
                throw new ArgumentNullException(nameof(userFriendlyName), "Property name cannot be null");

            if (string.IsNullOrWhiteSpace(userFriendlyName))
                throw new ArgumentException("Property name cannot be empty or whitespace", nameof(userFriendlyName));

            if (UserFriendlyToCapeOpen.TryGetValue(userFriendlyName, out var capeOpenName))
            {
                return capeOpenName;
            }

            throw new ArgumentException(
                $"Unsupported property name: '{userFriendlyName}'. " +
                $"Supported properties: {string.Join(", ", GetSupportedProperties())}",
                nameof(userFriendlyName));
        }

        /// <summary>
        /// Converts a CAPE-OPEN standard property name to its user-friendly name.
        /// Conversion is case-insensitive.
        /// </summary>
        /// <param name="capeOpenName">The CAPE-OPEN standard property name (e.g., "temperature", "totalFlow").</param>
        /// <returns>The user-friendly property name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when capeOpenName is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the property name is not supported.</exception>
        /// <example>
        /// <code>
        /// string userFriendly = CapeOpenPropertyConverter.ToUserFriendlyName("totalFlow");
        /// // Returns: "molarFlow"
        ///
        /// string userFriendly2 = CapeOpenPropertyConverter.ToUserFriendlyName("temperature");
        /// // Returns: "temperature"
        /// </code>
        /// </example>
        public static string ToUserFriendlyName(string capeOpenName)
        {
            if (capeOpenName == null)
                throw new ArgumentNullException(nameof(capeOpenName), "Property name cannot be null");

            if (string.IsNullOrWhiteSpace(capeOpenName))
                throw new ArgumentException("Property name cannot be empty or whitespace", nameof(capeOpenName));

            if (CapeOpenToUserFriendly.TryGetValue(capeOpenName, out var userFriendlyName))
            {
                return userFriendlyName;
            }

            throw new ArgumentException(
                $"Unsupported CAPE-OPEN property name: '{capeOpenName}'. " +
                $"Supported CAPE-OPEN names: {string.Join(", ", CapeOpenToUserFriendly.Keys)}",
                nameof(capeOpenName));
        }

        /// <summary>
        /// Validates whether a property name is supported.
        /// Checks both user-friendly and CAPE-OPEN names.
        /// Validation is case-insensitive.
        /// </summary>
        /// <param name="name">The property name to validate (can be user-friendly or CAPE-OPEN format).</param>
        /// <returns>True if the property name is supported; otherwise, false.</returns>
        /// <remarks>
        /// This method accepts either user-friendly names (e.g., "molarFlow") or CAPE-OPEN names (e.g., "totalFlow").
        /// Returns true if the name matches any supported property in either format.
        /// </remarks>
        /// <example>
        /// <code>
        /// bool valid1 = CapeOpenPropertyConverter.IsValidPropertyName("temperature");  // true
        /// bool valid2 = CapeOpenPropertyConverter.IsValidPropertyName("Temperature");  // true (case-insensitive)
        /// bool valid3 = CapeOpenPropertyConverter.IsValidPropertyName("totalFlow");    // true (CAPE-OPEN name)
        /// bool valid4 = CapeOpenPropertyConverter.IsValidPropertyName("molarFlow");    // true (user-friendly name)
        /// bool valid5 = CapeOpenPropertyConverter.IsValidPropertyName("invalid");      // false
        /// </code>
        /// </example>
        public static bool IsValidPropertyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Check if it's a user-friendly name or CAPE-OPEN name
            return UserFriendlyToCapeOpen.ContainsKey(name) || CapeOpenToUserFriendly.ContainsKey(name);
        }

        /// <summary>
        /// Gets a read-only list of all supported user-friendly property names.
        /// </summary>
        /// <returns>A read-only list of supported user-friendly property names.</returns>
        /// <remarks>
        /// The returned list contains the canonical user-friendly names:
        /// - temperature
        /// - pressure
        /// - molarFlow
        /// - composition
        /// </remarks>
        /// <example>
        /// <code>
        /// var supportedProperties = CapeOpenPropertyConverter.GetSupportedProperties();
        /// foreach (var property in supportedProperties)
        /// {
        ///     Console.WriteLine($"Supported: {property}");
        /// }
        /// </code>
        /// </example>
        public static IReadOnlyList<string> GetSupportedProperties()
        {
            // Return distinct user-friendly names (the values from CapeOpenToUserFriendly)
            return CapeOpenToUserFriendly.Values.Distinct().ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets a read-only list of all supported CAPE-OPEN property names.
        /// </summary>
        /// <returns>A read-only list of supported CAPE-OPEN property names.</returns>
        /// <remarks>
        /// The returned list contains the CAPE-OPEN standard names:
        /// - temperature
        /// - pressure
        /// - totalFlow
        /// - composition
        /// </remarks>
        /// <example>
        /// <code>
        /// var capeOpenProperties = CapeOpenPropertyConverter.GetSupportedCapeOpenNames();
        /// foreach (var property in capeOpenProperties)
        /// {
        ///     Console.WriteLine($"CAPE-OPEN: {property}");
        /// }
        /// </code>
        /// </example>
        public static IReadOnlyList<string> GetSupportedCapeOpenNames()
        {
            // Return distinct CAPE-OPEN names (the keys from CapeOpenToUserFriendly)
            return CapeOpenToUserFriendly.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets the physical unit for a given property name.
        /// Accepts both user-friendly and CAPE-OPEN names.
        /// </summary>
        /// <param name="propertyName">The property name (user-friendly or CAPE-OPEN format).</param>
        /// <returns>The physical unit as a string.</returns>
        /// <exception cref="ArgumentException">Thrown when the property name is not supported.</exception>
        /// <remarks>
        /// Supported units:
        /// - temperature: K (Kelvin)
        /// - pressure: Pa (Pascal)
        /// - molarFlow/totalFlow: mol/s (moles per second)
        /// - composition: dimensionless (mole fractions)
        /// </remarks>
        /// <example>
        /// <code>
        /// string unit1 = CapeOpenPropertyConverter.GetUnit("temperature");  // "K"
        /// string unit2 = CapeOpenPropertyConverter.GetUnit("pressure");     // "Pa"
        /// string unit3 = CapeOpenPropertyConverter.GetUnit("molarFlow");    // "mol/s"
        /// string unit4 = CapeOpenPropertyConverter.GetUnit("totalFlow");    // "mol/s"
        /// </code>
        /// </example>
        public static string GetUnit(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Property name cannot be null or empty", nameof(propertyName));

            // Normalize to user-friendly name first
            string normalizedName;
            if (UserFriendlyToCapeOpen.ContainsKey(propertyName))
            {
                normalizedName = propertyName.ToLowerInvariant();
            }
            else if (CapeOpenToUserFriendly.TryGetValue(propertyName, out var userFriendly))
            {
                normalizedName = userFriendly.ToLowerInvariant();
            }
            else
            {
                throw new ArgumentException($"Unsupported property name: '{propertyName}'", nameof(propertyName));
            }

            // Map to units
            switch (normalizedName)
            {
                case "temperature":
                    return "K";
                case "pressure":
                    return "Pa";
                case "molarflow":
                    return "mol/s";
                case "composition":
                    return "mole fractions";
                default:
                    throw new ArgumentException($"Unknown property name: '{propertyName}'", nameof(propertyName));
            }
        }
    }
}
