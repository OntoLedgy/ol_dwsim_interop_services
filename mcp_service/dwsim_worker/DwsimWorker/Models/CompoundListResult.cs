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

namespace DwsimWorker.Models
{
    public sealed class CompoundListResult
    {
        public IReadOnlyList<CompoundInfo> Compounds { get; }
        public int TotalCount { get; }
        public int Limit { get; }
        public int Offset { get; }
        public string Pattern { get; }
        public string Category { get; }

        public CompoundListResult(
            IReadOnlyList<CompoundInfo> compounds,
            int totalCount,
            int limit,
            int offset,
            string pattern,
            string category)
        {
            if (compounds == null)
            {
                throw new ArgumentNullException(nameof(compounds));
            }

            if (totalCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalCount), "Total count cannot be negative.");
            }

            if (limit < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "Limit cannot be negative.");
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");
            }

            Compounds = compounds;
            TotalCount = totalCount;
            Limit = limit;
            Offset = offset;
            Pattern = pattern ?? string.Empty;
            Category = category ?? string.Empty;
        }
    }
}
