using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using DwsimWorker.Engine;
using DwsimWorker.Exceptions;

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

        // Common compound names for validation
        // This is a subset of available compounds - DWSIM has hundreds more
        private static readonly HashSet<string> KnownCompounds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Alkanes
            "Methane", "Ethane", "Propane", "n-Butane", "i-Butane", "Butane",
            "n-Pentane", "i-Pentane", "Pentane",
            "n-Hexane", "Hexane", "n-Heptane", "Heptane",
            "n-Octane", "Octane", "n-Nonane", "n-Decane",

            // Alkenes
            "Ethylene", "Ethene", "Propylene", "Propene",
            "1-Butene", "2-Butene",

            // Aromatics
            "Benzene", "Toluene", "Xylene", "o-Xylene", "m-Xylene", "p-Xylene",
            "Ethylbenzene", "Styrene",

            // Other hydrocarbons
            "Acetylene", "Cyclopentane", "Cyclohexane",

            // Inorganics
            "Water", "H2O", "Oxygen", "O2", "Nitrogen", "N2",
            "Carbon Dioxide", "CO2", "Carbon Monoxide", "CO",
            "Hydrogen", "H2", "Hydrogen Sulfide", "H2S",
            "Ammonia", "NH3", "Sulfur Dioxide", "SO2",

            // Alcohols
            "Methanol", "Ethanol", "Propanol", "1-Propanol",
            "2-Propanol", "Butanol", "1-Butanol",

            // Other organics
            "Acetic Acid", "Acetone", "Formaldehyde",
            "Methyl Ethyl Ketone", "MEK"
        };

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
            if (string.IsNullOrWhiteSpace(compoundName))
            {
                var message = "Compound name cannot be null or empty";
                _logger.Warning(message);
                return LoadResult.FailureResult(message, new ArgumentException(message, nameof(compoundName)));
            }

            _logger.Debug("Attempting to add compound: {CompoundName}", compoundName);

            // Step 1: Validate compound name exists in database
            if (!ValidateCompoundName(compoundName))
            {
                var message = $"Compound '{compoundName}' not found in DWSIM database. " +
                             "Check spelling or use GetAvailableCompounds() to see valid compound names.";
                _logger.Warning("Compound validation failed: {CompoundName}", compoundName);
                return LoadResult.FailureResult(message, new CompoundNotFoundException(compoundName));
            }

            try
            {
                // Step 2: Add compound to flowsheet
                var flowsheet = _context.GetFlowsheet();
                var flowsheetType = flowsheet.GetType();
                var addCompound = flowsheetType.GetMethod("AddCompound", new[] { typeof(string) });
                if (addCompound == null)
                {
                    var message = "Flowsheet does not expose AddCompound";
                    _logger.Warning(message);
                    return LoadResult.FailureResult(message, new MissingMethodException(message));
                }

                addCompound.Invoke(flowsheet, new object[] { compoundName });

                // Step 3: Track compound in flowsheet context
                _context.AddCompound(compoundName);

                // Step 4: Ensure existing streams have updated compound list
                var addToStream = flowsheetType.GetMethod("AddCompoundsToMaterialStream");
                if (addToStream != null)
                {
                    foreach (var streamId in _context.GetStreamIds())
                    {
                        var stream = _context.GetStream(streamId);
                        if (stream != null)
                        {
                            addToStream.Invoke(flowsheet, new[] { stream });
                        }
                    }
                }

                // Step 5: Log success with structured logging
                _logger.Information("Compound added successfully: {CompoundName}", compoundName);

                return LoadResult.SuccessResult(new List<AssemblyInfo>());
            }
            catch (InvalidOperationException ex)
            {
                var message = $"Failed to add compound '{compoundName}': Flowsheet not initialized";
                _logger.Error(ex, message);
                return LoadResult.FailureResult(message, ex);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error adding compound '{compoundName}': {ex.Message}";
                _logger.Error(ex, message);
                return LoadResult.FailureResult(message, ex);
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

            bool isValid = KnownCompounds.Contains(compoundName);

            if (isValid)
            {
                _logger.Debug("Compound name validated: {CompoundName}", compoundName);
            }
            else
            {
                _logger.Debug("Compound name not found in database: {CompoundName}", compoundName);
            }

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
            return KnownCompounds.OrderBy(c => c).ToList().AsReadOnly();
        }
    }
}
