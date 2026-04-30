// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;

namespace DwsimWorker.Models
{
    public sealed class CompoundInfo
    {
        public string Name { get; }
        public string Category { get; }
        public IReadOnlyList<string> Aliases { get; }

        public CompoundInfo(string name, string category, IReadOnlyList<string> aliases)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name), "Compound name cannot be null or empty.");
            }

            Name = name;
            Category = category ?? string.Empty;
            Aliases = aliases ?? Array.Empty<string>();
        }
    }
}
