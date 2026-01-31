using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using DwsimWorker.Engine;
using DwsimWorker.Exceptions;
using DwsimWorker.Models;
using DwsimWorker.Utilities;
using DwsimWorker.Observability;

namespace DwsimWorker.Adapters
{
    /// <summary>
    /// Adapter for compound database access and compound addition to flowsheet.
    /// Provides validation and error handling for compound operations.
    /// </summary>
    /// <remarks>
    /// This adapter wraps DWSIM's compound database functionality and provides a clean interface
    /// for adding compounds from the database to a flowsheet. It uses the Result pattern for
    /// error handling rather than throwing exceptions for expected failures.
    ///
    /// Supported compounds include common hydrocarbons (methane, ethane, propane, butane, etc.),
    /// water, and other chemicals available in DWSIM's ChemSep database.
    /// </remarks>
    public sealed class CompoundAdapter
    {
        private readonly ILogger _logger;
        private readonly FlowsheetContext _context;

        private const int DefaultSuggestionLimit = 5;
        private const int DefaultSuggestionDistance = 3;
        private const int DefaultListLimit = 50;
        private const int MaxListLimit = 200;
        private const string CategoryOther = "Other";

        private const string CategoryAlkanes = "Alkanes";
        private const string CategoryAlkenes = "Alkenes";
        private const string CategoryAromatics = "Aromatics";
        private const string CategoryInorganics = "Inorganics";
        private const string CategoryAlcohols = "Alcohols";
        private const string CategoryOtherOrganics = "Other Organics";

        private static readonly string[] AlkaneCompounds =
        {
            "Methane", "Ethane", "Propane", "n-Butane", "i-Butane", "Butane",
            "n-Pentane", "i-Pentane", "Pentane", "n-Hexane", "Hexane",
            "n-Heptane", "Heptane", "n-Octane", "Octane", "n-Nonane", "n-Decane"
        };

        private static readonly string[] AlkeneCompounds =
        {
            "Ethylene", "Ethene", "Propylene", "Propene", "1-Butene", "2-Butene"
        };

        private static readonly string[] AromaticCompounds =
        {
            "Benzene", "Toluene", "Xylene", "o-Xylene", "m-Xylene", "p-Xylene",
            "Ethylbenzene", "Styrene"
        };

        private static readonly string[] OtherHydrocarbons =
        {
            "Acetylene", "Cyclopentane", "Cyclohexane"
        };

        private static readonly string[] InorganicCompounds =
        {
            "Water", "H2O", "Oxygen", "O2", "Nitrogen", "N2",
            "Carbon Dioxide", "CO2", "Carbon Monoxide", "CO",
            "Hydrogen", "H2", "Hydrogen sulfide", "H2S",
            "Ammonia", "NH3", "Sulfur Dioxide", "SO2"
        };

        private static readonly string[] AlcoholCompounds =
        {
            "Methanol", "Ethanol", "Propanol", "1-Propanol", "2-Propanol",
            "Butanol", "1-Butanol"
        };

        private static readonly string[] OtherOrganicCompounds =
        {
            "Acetic Acid", "Acetone", "Formaldehyde", "Methyl Ethyl Ketone", "MEK"
        };

        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CompoundCategories =
            BuildCompoundCategories();

        private static readonly HashSet<string> KnownCompounds =
            BuildKnownCompounds(CompoundCategories);

        private static readonly IReadOnlyDictionary<string, string> CompoundCategoryLookup =
            BuildCategoryLookup(CompoundCategories);

        private static readonly IReadOnlyList<string> SortedCompounds =
            KnownCompounds.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();

        /// <summary>
        /// Initializes a new instance of the <see cref="CompoundAdapter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for compound operation logging.</param>
        /// <param name="context">The flowsheet context that manages compound state.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or context is null.</exception>
        public CompoundAdapter(ILogger logger, FlowsheetContext context)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Adds a compound from the DWSIM database to the flowsheet.
        /// </summary>
        /// <param name="compoundName">The name of the compound to add (case-insensitive).</param>
        /// <returns>
        /// A Result indicating success or failure. On success, Result.Success is true.
        /// On failure, Result.Success is false with an error message.
        /// </returns>
        /// <remarks>
        /// This method validates the compound name against the DWSIM compound database before
        /// attempting to add it to the flowsheet. If the compound name is not found in the
        /// database, the method returns a failure result rather than throwing an exception.
        ///
        /// Compound names are case-insensitive. For example, "water", "Water", and "WATER"
        /// are all valid and refer to the same compound.
        ///
        /// The method logs all compound additions using structured logging for traceability.
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new CompoundAdapter(logger, context);
        ///
        /// // Add a valid compound
        /// var result = adapter.AddCompound("Methane");
        /// if (result.Success)
        /// {
        ///     Console.WriteLine("Methane added successfully");
        /// }
        ///
        /// // Attempt to add an invalid compound
        /// var result2 = adapter.AddCompound("InvalidCompound");
        /// if (!result2.Success)
        /// {
        ///     Console.WriteLine($"Error: {result2.Message}");
        /// }
        /// </code>
        /// </example>
        public LoadResult AddCompound(string compoundName)
        {
            var attributes = new Dictionary<string, object>
            {
                ["operation"] = nameof(AddCompound),
                ["compound_name"] = compoundName
            };
            var scope = TracingAdapter.StartSpan("CompoundAdapter.AddCompound", attributes);
            try
            {
                if (string.IsNullOrWhiteSpace(compoundName))
                {
                    var message = "Compound name cannot be null or empty";
                    _logger.Warning(message);
                    return LoadResult.FailureResult(message, new ArgumentException(message, nameof(compoundName)));
                }

                _logger.Debug("Attempting to add compound: {CompoundName}", compoundName);

                var validation = ValidateCompound(compoundName);
                if (!validation.Valid)
                {
                    return BuildValidationFailure(compoundName, validation);
                }

                return TryAddCompound(validation.CanonicalName);
            }
            finally
            {
                scope.Dispose();
            }
        }

        /// <summary>
        /// Gets the list of compounds currently in the flowsheet.
        /// </summary>
        /// <returns>
        /// A Result containing the list of compound names on success.
        /// Returns a failure result if the flowsheet is not initialized.
        /// </returns>
        /// <remarks>
        /// The returned list contains the names of all compounds that have been added to the
        /// flowsheet. The list is read-only and reflects the current state of the flowsheet.
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new CompoundAdapter(logger, context);
        /// adapter.AddCompound("Methane");
        /// adapter.AddCompound("Ethane");
        ///
        /// var result = adapter.GetCompounds();
        /// if (result.Success)
        /// {
        ///     foreach (var compound in result.Data)
        ///     {
        ///         Console.WriteLine($"Compound: {compound}");
        ///     }
        /// }
        /// </code>
        /// </example>
        public ValidationResult GetCompounds()
        {
            try
            {
                _logger.Debug("Retrieving compound list from flowsheet");

                var compounds = _context.GetCompounds();

                _logger.Information("Retrieved {Count} compounds from flowsheet", compounds.Count);

                // Return success with compound list as validated types
                return ValidationResult.SuccessResult(compounds.ToList());
            }
            catch (InvalidOperationException ex)
            {
                var message = "Failed to get compounds: Flowsheet not initialized";
                _logger.Error(ex, message);
                return ValidationResult.FailureResult(message, ex);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error retrieving compounds: {ex.Message}";
                _logger.Error(ex, message);
                return ValidationResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Validates whether a compound name exists in the DWSIM compound database.
        /// </summary>
        /// <param name="compoundName">The compound name to validate (case-insensitive).</param>
        /// <returns>True if the compound exists in the database; otherwise, false.</returns>
        /// <remarks>
        /// This method performs a case-insensitive lookup against a known list of common
        /// DWSIM compounds. The list includes:
        /// - Alkanes (methane through decane)
        /// - Alkenes (ethylene, propylene, butenes)
        /// - Aromatics (benzene, toluene, xylenes)
        /// - Inorganics (water, oxygen, nitrogen, CO2, etc.)
        /// - Alcohols (methanol through butanol)
        /// - Other common organics
        ///
        /// Note: DWSIM's actual compound database contains hundreds of compounds. This
        /// validation is currently based on a subset of common compounds. Future versions
        /// may query the DWSIM database directly for comprehensive validation.
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new CompoundAdapter(logger, context);
        ///
        /// bool isValid1 = adapter.ValidateCompoundName("Methane");  // true
        /// bool isValid2 = adapter.ValidateCompoundName("water");    // true (case-insensitive)
        /// bool isValid3 = adapter.ValidateCompoundName("InvalidName");  // false
        /// </code>
        /// </example>
        public bool ValidateCompoundName(string compoundName)
        {
            if (string.IsNullOrWhiteSpace(compoundName))
            {
                _logger.Debug("Compound name validation failed: null or empty");
                return false;
            }

            var trimmed = compoundName.Trim();
            var isValid = TryResolveCanonicalName(trimmed, out var canonicalName, out _);
            LogValidationOutcome(trimmed, isValid, canonicalName);
            return isValid;
        }

        /// <summary>
        /// Gets a list of available compound names from the known compounds list.
        /// </summary>
        /// <returns>A read-only list of compound names available for adding to flowsheet.</returns>
        /// <remarks>
        /// This method returns the list of compounds that are known to the adapter and can
        /// be successfully validated. This is a subset of the full DWSIM compound database
        /// and includes the most common compounds used in chemical process simulation.
        ///
        /// The list is sorted alphabetically for easy reference.
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new CompoundAdapter(logger, context);
        /// var available = adapter.GetAvailableCompounds();
        ///
        /// Console.WriteLine($"Total available compounds: {available.Count}");
        /// foreach (var compound in available)
        /// {
        ///     Console.WriteLine($"  - {compound}");
        /// }
        /// </code>
        /// </example>
        public IReadOnlyList<string> GetAvailableCompounds()
        {
            _logger.Debug("Retrieving list of available compounds");
            return SortedCompounds;
        }

        public CompoundValidationResult ValidateCompound(string compoundName)
        {
            var input = NormalizeInput(compoundName);
            if (string.IsNullOrWhiteSpace(input))
            {
                return new CompoundValidationResult(input, false, string.Empty, false, Array.Empty<string>());
            }

            var valid = TryResolveCanonicalName(input, out var canonicalName, out var aliasUsed);
            var suggestions = valid ? Array.Empty<string>() : GetSuggestions(input);
            var resolvedCanonical = valid ? canonicalName : string.Empty;
            return new CompoundValidationResult(input, valid, resolvedCanonical, aliasUsed, suggestions);
        }

        public CompoundListResult ListCompounds(string pattern, string category, int limit, int offset)
        {
            var resolvedLimit = NormalizeLimit(limit);
            var resolvedOffset = NormalizeOffset(offset);
            var compounds = GetCompoundsByCategory(category);
            var filtered = ApplyPattern(compounds, pattern).ToList();
            var total = filtered.Count;
            var page = filtered.Skip(resolvedOffset).Take(resolvedLimit).Select(BuildCompoundInfo).ToList();
            return new CompoundListResult(page.AsReadOnly(), total, resolvedLimit, resolvedOffset, pattern, category);
        }

        private static string GetCanonicalCompoundName(string compoundName)
        {
            return KnownCompounds.FirstOrDefault(c =>
                       string.Equals(c, compoundName, StringComparison.OrdinalIgnoreCase))
                   ?? compoundName;
        }

        private LoadResult TryAddCompound(string canonicalName)
        {
            try
            {
                EnsureCompoundAdded(canonicalName);
                return LoadResult.SuccessResult(new List<AssemblyInfo>());
            }
            catch (Exception ex)
            {
                TracingAdapter.RecordException(ex);
                return HandleAddCompoundException(canonicalName, ex);
            }
        }

        private void EnsureCompoundAdded(string canonicalName)
        {
            if (IsCompoundAlreadyPresent(canonicalName))
            {
                _logger.Information("Compound already present in flowsheet: {CompoundName}", canonicalName);
                return;
            }

            var flowsheet = _context.GetFlowsheet();
            var addCompound = GetAddCompoundMethod(flowsheet);
            if (addCompound == null)
            {
                throw new MissingMethodException("Flowsheet does not expose AddCompound");
            }

            addCompound.Invoke(flowsheet, new object[] { canonicalName });
            _context.AddCompound(canonicalName);
            UpdateStreamsWithCompounds(flowsheet);
            _logger.Information("Compound added successfully: {CompoundName}", canonicalName);
        }

        private LoadResult HandleAddCompoundException(string compoundName, Exception ex)
        {
            if (ex is MissingMethodException)
            {
                return MissingAddCompoundResult();
            }

            if (ex is InvalidOperationException)
            {
                return BuildAddCompoundFailure(compoundName, "Flowsheet not initialized", ex);
            }

            if (ex is System.Reflection.TargetInvocationException tie)
            {
                var innerEx = tie.InnerException ?? tie;
                return BuildAddCompoundFailure(compoundName, innerEx.Message, innerEx);
            }

            return BuildAddCompoundFailure(compoundName, ex.Message, ex);
        }

        private LoadResult BuildValidationFailure(string compoundName, CompoundValidationResult validation)
        {
            var message = BuildInvalidCompoundMessage(compoundName, validation.Suggestions);
            _logger.Warning("Compound validation failed: {CompoundName}", compoundName);
            return LoadResult.FailureResult(message, new CompoundNotFoundException(compoundName));
        }

        private static string BuildInvalidCompoundMessage(string compoundName, IReadOnlyList<string> suggestions)
        {
            var baseMessage = $"Compound '{compoundName}' not found in DWSIM database.";
            if (suggestions == null || suggestions.Count == 0)
            {
                return baseMessage + " Check spelling or use GetAvailableCompounds() to see valid names.";
            }

            var suggestionList = string.Join(", ", suggestions);
            return baseMessage + $" Did you mean: {suggestionList}?";
        }

        private bool IsCompoundAlreadyPresent(string canonicalName)
        {
            var existingCompounds = _context.GetCompounds();
            return existingCompounds.Any(c => string.Equals(c, canonicalName, StringComparison.OrdinalIgnoreCase));
        }

        private static System.Reflection.MethodInfo GetAddCompoundMethod(object flowsheet)
        {
            return flowsheet?.GetType().GetMethod("AddCompound", new[] { typeof(string) });
        }

        private LoadResult MissingAddCompoundResult()
        {
            var message = "Flowsheet does not expose AddCompound";
            _logger.Warning(message);
            return LoadResult.FailureResult(message, new MissingMethodException(message));
        }

        private void UpdateStreamsWithCompounds(object flowsheet)
        {
            var addToStream = flowsheet.GetType().GetMethod("AddCompoundsToMaterialStream");
            if (addToStream == null)
            {
                return;
            }

            foreach (var streamId in _context.GetStreamIds())
            {
                var stream = _context.GetStream(streamId);
                if (stream != null)
                {
                    addToStream.Invoke(flowsheet, new[] { stream });
                }
            }
        }

        private LoadResult BuildAddCompoundFailure(string compoundName, string details, Exception ex)
        {
            var message = $"Failed to add compound '{compoundName}': {details}";
            _logger.Error(ex, message);
            return LoadResult.FailureResult(message, ex);
        }

        private static string NormalizeInput(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static int NormalizeLimit(int limit)
        {
            if (limit <= 0)
            {
                return DefaultListLimit;
            }

            return Math.Min(limit, MaxListLimit);
        }

        private static int NormalizeOffset(int offset)
        {
            return offset < 0 ? 0 : offset;
        }

        private static IEnumerable<string> ApplyPattern(IEnumerable<string> compounds, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return compounds;
            }

            var trimmed = pattern.Trim();
            return compounds.Where(name => name.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private IReadOnlyList<string> GetCompoundsByCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return SortedCompounds;
            }

            if (CompoundCategories.TryGetValue(category.Trim(), out var compounds))
            {
                return compounds;
            }

            _logger.Warning("Unknown compound category requested: {Category}", category);
            return Array.Empty<string>();
        }

        private CompoundInfo BuildCompoundInfo(string name)
        {
            var category = GetCategoryForCompound(name);
            var aliases = CompoundAliasMapper.GetAliasesFor(name);
            return new CompoundInfo(name, category, aliases);
        }

        private static string GetCategoryForCompound(string name)
        {
            return CompoundCategoryLookup.TryGetValue(name, out var category) ? category : CategoryOther;
        }

        private static IReadOnlyList<string> GetSuggestions(string input)
        {
            return FuzzyMatcher.FindSimilar(input, KnownCompounds, DefaultSuggestionLimit, DefaultSuggestionDistance);
        }

        private void LogValidationOutcome(string input, bool isValid, string canonicalName)
        {
            if (isValid)
            {
                _logger.Debug("Compound name validated: {CompoundName}", canonicalName);
                return;
            }

            _logger.Debug("Compound name not found in database: {CompoundName}", input);
        }

        private static bool TryResolveCanonicalName(string input, out string canonicalName, out bool aliasUsed)
        {
            aliasUsed = CompoundAliasMapper.TryResolveAlias(input, out canonicalName);
            if (aliasUsed)
            {
                return true;
            }

            canonicalName = GetCanonicalCompoundName(input);
            return KnownCompounds.Contains(canonicalName);
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildCompoundCategories()
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { CategoryAlkanes, ToSortedList(AlkaneCompounds) },
                { CategoryAlkenes, ToSortedList(AlkeneCompounds) },
                { CategoryAromatics, ToSortedList(AromaticCompounds) },
                { CategoryInorganics, ToSortedList(InorganicCompounds) },
                { CategoryAlcohols, ToSortedList(AlcoholCompounds) },
                { CategoryOtherOrganics, ToSortedList(OtherOrganicCompounds.Concat(OtherHydrocarbons).ToArray()) }
            };
        }

        private static HashSet<string> BuildKnownCompounds(IReadOnlyDictionary<string, IReadOnlyList<string>> categories)
        {
            var compounds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var list in categories.Values)
            {
                foreach (var compound in list)
                {
                    compounds.Add(compound);
                }
            }

            return compounds;
        }

        private static IReadOnlyDictionary<string, string> BuildCategoryLookup(
            IReadOnlyDictionary<string, IReadOnlyList<string>> categories)
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in categories)
            {
                foreach (var compound in pair.Value)
                {
                    lookup[compound] = pair.Key;
                }
            }

            return lookup;
        }

        private static IReadOnlyList<string> ToSortedList(IEnumerable<string> compounds)
        {
            return compounds.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
        }
    }
}
