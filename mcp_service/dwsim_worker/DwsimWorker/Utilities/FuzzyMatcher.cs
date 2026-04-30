// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;

namespace DwsimWorker.Utilities
{
    public static class FuzzyMatcher
    {
        public static IReadOnlyList<string> FindSimilar(string input, IEnumerable<string> candidates, int maxResults = 5, int maxDistance = 3)
        {
            if (string.IsNullOrWhiteSpace(input) || candidates == null || maxResults <= 0 || maxDistance < 0)
            {
                return Array.Empty<string>();
            }

            var normalizedInput = Normalize(input);
            var matches = new List<CandidateMatch>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in candidates)
            {
                if (!TryPrepareCandidate(candidate, seen, out var prepared))
                {
                    continue;
                }

                var distance = ComputeDistance(normalizedInput, Normalize(prepared), maxDistance);
                if (distance <= maxDistance)
                {
                    matches.Add(new CandidateMatch(prepared, distance));
                }
            }

            return SelectMatches(matches, maxResults);
        }

        private static IReadOnlyList<string> SelectMatches(List<CandidateMatch> matches, int maxResults)
        {
            if (matches.Count == 0)
            {
                return Array.Empty<string>();
            }

            return matches.OrderBy(match => match.Distance)
                .ThenBy(match => match.Value, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .Select(match => match.Value)
                .ToList()
                .AsReadOnly();
        }

        private static int ComputeDistance(string source, string target, int maxDistance)
        {
            if (Math.Abs(source.Length - target.Length) > maxDistance)
            {
                return maxDistance + 1;
            }

            if (source.Length == 0 || target.Length == 0)
            {
                return Math.Max(source.Length, target.Length);
            }

            var previous = BuildInitialRow(target.Length);
            var current = new int[target.Length + 1];

            for (var i = 1; i <= source.Length; i++)
            {
                var rowMin = UpdateRow(source, target, i, previous, current);
                if (rowMin > maxDistance)
                {
                    return maxDistance + 1;
                }

                SwapRows(ref previous, ref current);
            }

            return previous[target.Length];
        }

        private static int UpdateRow(string source, string target, int index, int[] previous, int[] current)
        {
            current[0] = index;
            var rowMin = current[0];

            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[index - 1] == target[j - 1] ? 0 : 1;
                current[j] = Min(previous[j] + 1, current[j - 1] + 1, previous[j - 1] + cost);
                rowMin = current[j] < rowMin ? current[j] : rowMin;
            }

            return rowMin;
        }

        private static int[] BuildInitialRow(int length)
        {
            var row = new int[length + 1];
            for (var i = 0; i <= length; i++)
            {
                row[i] = i;
            }

            return row;
        }

        private static void SwapRows(ref int[] previous, ref int[] current)
        {
            var temp = previous;
            previous = current;
            current = temp;
        }

        private static int Min(int first, int second, int third)
        {
            return Math.Min(Math.Min(first, second), third);
        }

        private static bool TryPrepareCandidate(string candidate, HashSet<string> seen, out string prepared)
        {
            prepared = string.Empty;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            prepared = candidate.Trim();
            return prepared.Length > 0 && seen.Add(prepared);
        }

        private static string Normalize(string value)
        {
            return value.Trim().ToUpperInvariant();
        }

        private readonly struct CandidateMatch
        {
            public CandidateMatch(string value, int distance)
            {
                Value = value;
                Distance = distance;
            }

            public string Value { get; }
            public int Distance { get; }
        }
    }
}
