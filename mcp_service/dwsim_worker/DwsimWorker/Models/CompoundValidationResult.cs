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
