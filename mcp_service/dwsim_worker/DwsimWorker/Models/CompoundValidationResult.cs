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
    public sealed class CompoundValidationResult
    {
        public string InputName { get; }
        public bool Valid { get; }
        public string CanonicalName { get; }
        public bool AliasUsed { get; }
        public IReadOnlyList<string> Suggestions { get; }

        public CompoundValidationResult(
            string inputName,
            bool valid,
            string canonicalName,
            bool aliasUsed,
            IReadOnlyList<string> suggestions)
        {
            InputName = inputName ?? string.Empty;
            Valid = valid;
            CanonicalName = canonicalName ?? string.Empty;
            AliasUsed = aliasUsed;
            Suggestions = suggestions ?? Array.Empty<string>();
        }
    }
}
