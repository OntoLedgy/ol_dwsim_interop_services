// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DwsimWorker.Converters
{
    /// <summary>
    /// Registry of canonical CAPE-OPEN property names and expected units.
    /// Provides consistent metadata for converter logic.
    /// </summary>
    public static class CapeOpenPropertyRegistry
    {
        private static readonly IReadOnlyList<PropertyDefinition> PropertyDefinitions = BuildProperties().AsReadOnly();

        private static readonly IReadOnlyDictionary<string, PropertyDefinition> PropertiesByName =
            new ReadOnlyDictionary<string, PropertyDefinition>(
                PropertyDefinitions.ToDictionary(
                    definition => definition.CanonicalName,
                    definition => definition,
                    StringComparer.OrdinalIgnoreCase));

        private static readonly IReadOnlyDictionary<string, PropertyDefinition> PropertiesByAlias =
            new ReadOnlyDictionary<string, PropertyDefinition>(
                PropertyDefinitions
                    .SelectMany(definition => GetDistinctNames(definition).Select(name => new { name, definition }))
                    .ToDictionary(
                        entry => entry.name,
                        entry => entry.definition,
                        StringComparer.OrdinalIgnoreCase));

        /// <summary>
        /// Attempts to resolve a property name (canonical or alias) to a definition.
        /// </summary>
        /// <param name="name">Property name (canonical or alias).</param>
        /// <param name="definition">Resolved definition if found.</param>
        /// <returns>True when resolved; otherwise false.</returns>
        public static bool TryGet(string name, out PropertyDefinition definition)
        {
            definition = null;

            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return PropertiesByAlias.TryGetValue(name, out definition);
        }

        /// <summary>
        /// Gets all canonical CAPE-OPEN property names supported by the registry.
        /// </summary>
        public static IReadOnlyList<string> GetCanonicalNames()
        {
            return PropertiesByName.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets all names (canonical + aliases) supported by the registry.
        /// </summary>
        public static IReadOnlyList<string> GetAllNames()
        {
            return PropertiesByAlias.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
        }

        private static List<PropertyDefinition> BuildProperties()
        {
            return new List<PropertyDefinition>
            {
                new PropertyDefinition(
                canonicalName: "temperature",
                expectedUnit: "K",
                description: "Absolute temperature in Kelvin.",
                aliases: new[] { "Temperature" }),

                new PropertyDefinition(
                canonicalName: "pressure",
                expectedUnit: "Pa",
                description: "Absolute pressure in Pascals.",
                aliases: new[] { "Pressure" }),

                new PropertyDefinition(
                canonicalName: "totalFlow",
                expectedUnit: "mol/s",
                description: "Total molar flow rate in moles per second.",
                aliases: new[] { "molarFlow", "MolarFlow" }),

                new PropertyDefinition(
                canonicalName: "composition",
                expectedUnit: "mole fraction",
                description: "Mole fraction composition (dimensionless, sum to 1.0).",
                aliases: new[] { "Composition" })
            };
        }

        private static IEnumerable<string> GetDistinctNames(PropertyDefinition definition)
        {
            return definition.AllNames.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Describes a CAPE-OPEN property name and its expected units.
        /// </summary>
        public sealed class PropertyDefinition
        {
            public PropertyDefinition(string canonicalName, string expectedUnit, string description, IEnumerable<string> aliases)
            {
                if (string.IsNullOrWhiteSpace(canonicalName))
                {
                    throw new ArgumentException("Canonical name cannot be null or empty.", nameof(canonicalName));
                }

                CanonicalName = canonicalName;
                ExpectedUnit = expectedUnit ?? string.Empty;
                Description = description ?? string.Empty;
                Aliases = aliases?.ToList().AsReadOnly() ?? new List<string>().AsReadOnly();
            }

            public string CanonicalName { get; }

            public string ExpectedUnit { get; }

            public string Description { get; }

            public IReadOnlyList<string> Aliases { get; }

            public IReadOnlyList<string> AllNames => new[] { CanonicalName }.Concat(Aliases).ToList().AsReadOnly();
        }
    }
}
