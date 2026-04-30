// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
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
