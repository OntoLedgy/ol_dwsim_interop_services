// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;

namespace DwsimWorker.Utilities
{
    public static class CompoundAliasMapper
    {
        private static readonly IReadOnlyDictionary<string, string> AliasToCanonical =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "isobutane", "Isobutane" },
                { "iso-butane", "Isobutane" },
                { "i-butane", "Isobutane" },
                { "isopentane", "Isopentane" },
                { "iso-pentane", "Isopentane" },
                { "i-pentane", "Isopentane" },
                { "co2", "Carbon Dioxide" },
                { "n2", "Nitrogen" },
                { "h2o", "Water" },
                { "h2s", "Hydrogen sulfide" },
                { "nc4", "n-Butane" },
                { "n-c4", "n-Butane" },
                { "n-butane", "n-Butane" },
                { "normal butane", "n-Butane" },
                { "nc5", "n-Pentane" },
                { "n-c5", "n-Pentane" },
                { "n-pentane", "n-Pentane" },
                { "normal pentane", "n-Pentane" },
                { "nc6", "n-Hexane" },
                { "n-c6", "n-Hexane" },
                { "n-hexane", "n-Hexane" },
                { "normal hexane", "n-Hexane" }
            };

        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CanonicalToAliases =
            BuildReverseMapping();

        public static bool TryResolveAlias(string input, out string canonicalName)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                canonicalName = string.Empty;
                return false;
            }

            return AliasToCanonical.TryGetValue(input.Trim(), out canonicalName);
        }

        public static IReadOnlyList<string> GetAliasesFor(string canonicalName)
        {
            if (string.IsNullOrWhiteSpace(canonicalName))
            {
                return Array.Empty<string>();
            }

            return CanonicalToAliases.TryGetValue(canonicalName.Trim(), out var aliases)
                ? aliases
                : Array.Empty<string>();
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildReverseMapping()
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in AliasToCanonical)
            {
                if (!map.TryGetValue(pair.Value, out var aliases))
                {
                    aliases = new List<string>();
                    map[pair.Value] = aliases;
                }

                aliases.Add(pair.Key);
            }

            var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in map)
            {
                pair.Value.Sort(StringComparer.OrdinalIgnoreCase);
                result[pair.Key] = pair.Value.AsReadOnly();
            }

            return result;
        }
    }
}
