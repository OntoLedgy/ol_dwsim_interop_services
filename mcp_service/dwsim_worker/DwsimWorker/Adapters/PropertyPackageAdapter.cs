using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using DwsimWorker.Engine;
using Tomlyn;

namespace DwsimWorker.Adapters
{
    /// <summary>
    /// Adapter for configuring and managing thermodynamic property packages in DWSIM flowsheets.
    /// </summary>
    /// <remarks>
    /// Property packages define the thermodynamic models used for calculating physical properties
    /// and phase equilibria. DWSIM supports various equation-of-state and activity coefficient
    /// models for different types of systems.
    ///
    /// Common property packages:
    /// - Peng-Robinson: Cubic EOS, good for gas/liquid systems, hydrocarbons
    /// - SRK (Soave-Redlich-Kwong): Cubic EOS, similar to Peng-Robinson
    /// - NRTL: Activity coefficient model for liquid-liquid systems
    /// - UNIFAC: Predictive activity coefficient model
    /// </remarks>
    public sealed class PropertyPackageAdapter
    {
        private const string PropertyPackageInventoryFileName = "property_packages.toml";
        private readonly ILogger _logger;
        private readonly FlowsheetContext _context;
        private string _currentPackageName;

        private static readonly HashSet<string> SupportedPackages;
        private static readonly Dictionary<string, string> CanonicalNames;

        static PropertyPackageAdapter()
        {
            var inventory = LoadPropertyPackageInventory();
            SupportedPackages = inventory.supportedPackages;
            CanonicalNames = inventory.canonicalNames;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyPackageAdapter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for property package operation logging.</param>
        /// <param name="context">The flowsheet context that manages the flowsheet state.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or context is null.</exception>
        public PropertyPackageAdapter(ILogger logger, FlowsheetContext context)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _currentPackageName = null;
        }

        /// <summary>
        /// Sets the thermodynamic property package for the flowsheet.
        /// </summary>
        /// <param name="packageName">
        /// The name of the property package to use. Supported values: "Peng-Robinson", "SRK", "NRTL", "UNIFAC".
        /// Case-insensitive. Aliases are supported (e.g., "PR" for "Peng-Robinson").
        /// </param>
        /// <returns>
        /// A Result indicating success or failure. On success, Result.Success is true.
        /// On failure, Result.Success is false with an error message.
        /// </returns>
        /// <remarks>
        /// The property package must be set before performing thermodynamic calculations.
        /// Changing the property package after streams and units are configured may require
        /// recalculation of properties.
        ///
        /// Property package selection guidelines:
        /// - Peng-Robinson/SRK: Best for hydrocarbon systems (oil, gas)
        /// - NRTL: Best for polar liquid mixtures, liquid-liquid extraction
        /// - UNIFAC: Useful when experimental data is lacking, predictive model
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new PropertyPackageAdapter(logger, context);
        ///
        /// // Set Peng-Robinson property package
        /// var result = adapter.SetPropertyPackage("Peng-Robinson");
        /// if (result.Success)
        /// {
        ///     Console.WriteLine("Property package set successfully");
        /// }
        ///
        /// // Try using an alias
        /// var result2 = adapter.SetPropertyPackage("PR");  // Same as "Peng-Robinson"
        ///
        /// // Invalid package name
        /// var result3 = adapter.SetPropertyPackage("InvalidPackage");
        /// if (!result3.Success)
        /// {
        ///     Console.WriteLine($"Error: {result3.Message}");
        /// }
        /// </code>
        /// </example>
        public LoadResult SetPropertyPackage(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                var message = "Property package name cannot be null or empty";
                _logger.Warning(message);
                return LoadResult.FailureResult(message, new ArgumentException(message, nameof(packageName)));
            }

            _logger.Debug("Attempting to set property package: {PackageName}", packageName);

            // Step 1: Validate package name
            if (!SupportedPackages.Contains(packageName))
            {
                var message = $"Unsupported property package: '{packageName}'. " +
                             $"Supported packages: {string.Join(", ", GetAvailablePackages().ValidatedTypes)}";
                _logger.Warning("Property package validation failed: {PackageName}", packageName);
                return LoadResult.FailureResult(message, new ArgumentException(message, nameof(packageName)));
            }

            try
            {
                // Step 2: Normalize to canonical name
                var canonicalName = GetCanonicalName(packageName);

                // Step 3: Create and add property package in flowsheet
                var flowsheet = _context.GetFlowsheet();
                var flowsheetType = flowsheet.GetType();
                var method = flowsheetType.GetMethod("CreateAndAddPropertyPackage", new[] { typeof(string) });
                if (method == null)
                {
                    var message = "Flowsheet does not expose CreateAndAddPropertyPackage";
                    _logger.Warning(message);
                    return LoadResult.FailureResult(message, new MissingMethodException(message));
                }

                var propertyPackage = method.Invoke(flowsheet, new object[] { canonicalName });
                if (propertyPackage == null)
                {
                    var message = $"Property package '{canonicalName}' could not be created.";
                    _logger.Warning(message);
                    return LoadResult.FailureResult(message, new InvalidOperationException(message));
                }

                _context.SetPropertyPackage(propertyPackage, canonicalName);
                _currentPackageName = canonicalName;

                ApplyPropertyPackageToObjects(propertyPackage);

                // Step 4: Log success with structured logging
                _logger.Information("Property package set: {PackageName}", canonicalName);

                return LoadResult.SuccessResult(new List<AssemblyInfo>());
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                var innerEx = ex.InnerException ?? ex;
                var message = $"Unexpected error setting property package '{packageName}': {innerEx.Message}";
                _logger.Error(innerEx, message);
                return LoadResult.FailureResult(message, innerEx);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error setting property package '{packageName}': {ex.Message}";
                _logger.Error(ex, message);
                return LoadResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Gets the name of the currently configured property package.
        /// </summary>
        /// <returns>
        /// A Result containing the property package name on success.
        /// Returns a failure result if no property package has been configured.
        /// </returns>
        /// <remarks>
        /// The returned name is the canonical name (e.g., "Peng-Robinson" rather than "PR" alias).
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new PropertyPackageAdapter(logger, context);
        /// adapter.SetPropertyPackage("PR");
        ///
        /// var result = adapter.GetPropertyPackageName();
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Current package: {result.Data}");  // Output: "Peng-Robinson"
        /// }
        /// </code>
        /// </example>
        public ValidationResult GetPropertyPackageName()
        {
            try
            {
                _logger.Debug("Retrieving current property package name");

                if (string.IsNullOrEmpty(_currentPackageName))
                {
                    var message = "No property package has been configured";
                    _logger.Warning(message);
                    return ValidationResult.FailureResult(message, new InvalidOperationException(message));
                }

                _logger.Information("Current property package: {PackageName}", _currentPackageName);

                // Return success with package name as a validated type
                return ValidationResult.SuccessResult(new List<string> { _currentPackageName });
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error retrieving property package name: {ex.Message}";
                _logger.Error(ex, message);
                return ValidationResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Gets a list of available property package names.
        /// </summary>
        /// <returns>
        /// A Result containing the list of available property package names on success.
        /// Always succeeds and returns the list of supported packages.
        /// </returns>
        /// <remarks>
        /// The returned list contains canonical names only (not aliases). For example,
        /// "Peng-Robinson" is returned but not "PR".
        ///
        /// Supported property packages:
        /// - Peng-Robinson: Cubic equation of state for gas/liquid systems
        /// - SRK: Soave-Redlich-Kwong equation of state
        /// - NRTL: Non-Random Two-Liquid activity coefficient model
        /// - UNIFAC: Universal Functional Activity Coefficient model
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new PropertyPackageAdapter(logger, context);
        /// var result = adapter.GetAvailablePackages();
        ///
        /// if (result.Success)
        /// {
        ///     Console.WriteLine("Available property packages:");
        ///     foreach (var package in result.Data)
        ///     {
        ///         Console.WriteLine($"  - {package}");
        ///     }
        /// }
        /// </code>
        /// </example>
        public ValidationResult GetAvailablePackages()
        {
            try
            {
                _logger.Debug("Retrieving list of available property packages");

                // Get unique canonical names
                var availablePackages = CanonicalNames.Values
                    .Distinct()
                    .OrderBy(p => p)
                    .ToList();

                _logger.Information("Retrieved {Count} available property packages", availablePackages.Count);

                return ValidationResult.SuccessResult(availablePackages);
            }
            catch (Exception ex)
            {
                var message = $"Unexpected error retrieving available packages: {ex.Message}";
                _logger.Error(ex, message);
                return ValidationResult.FailureResult(message, ex);
            }
        }

        /// <summary>
        /// Gets the canonical name for a property package, handling aliases.
        /// </summary>
        /// <param name="packageName">The property package name (may be an alias).</param>
        /// <returns>The canonical property package name.</returns>
        /// <remarks>
        /// This method normalizes property package names to their canonical forms.
        /// For example, "PR" is normalized to "Peng-Robinson".
        /// </remarks>
        private string GetCanonicalName(string packageName)
        {
            if (CanonicalNames.TryGetValue(packageName, out var canonical))
            {
                return canonical;
            }

            // If not found in canonical names, return as-is (should not happen due to validation)
            return packageName;
        }

        private void ApplyPropertyPackageToObjects(object propertyPackage)
        {
            if (propertyPackage == null)
            {
                return;
            }

            foreach (var streamId in _context.GetStreamIds())
            {
                var stream = _context.GetStream(streamId);
                TrySetPropertyPackage(stream, propertyPackage);
            }

            foreach (var unitId in _context.GetUnitIds())
            {
                var unit = _context.GetUnit(unitId);
                TrySetPropertyPackage(unit, propertyPackage);
            }
        }

        private void TrySetPropertyPackage(object target, object propertyPackage)
        {
            if (target == null || propertyPackage == null)
            {
                return;
            }

            var property = target.GetType().GetProperty("PropertyPackage");
            if (property == null || !property.CanWrite)
            {
                return;
            }

            if (!property.PropertyType.IsInstanceOfType(propertyPackage))
            {
                return;
            }

            property.SetValue(target, propertyPackage);
        }

        /// <summary>
        /// Sets a binary interaction parameter (kij) for a pair of compounds in the current property package.
        /// </summary>
        /// <param name="compound1">Name of the first compound</param>
        /// <param name="compound2">Name of the second compound</param>
        /// <param name="value">The interaction parameter value (typically between 0 and 1)</param>
        /// <remarks>
        /// Binary Interaction Parameters (BIPs) are used in cubic equations of state (like Peng-Robinson)
        /// to improve the accuracy of mixture property predictions. They correct for non-ideal mixing behavior.
        /// The interaction parameter is symmetric: kij = kji.
        /// </remarks>
        public void SetBinaryInteractionParameter(string compound1, string compound2, double value)
        {
            try
            {
                var propertyPackage = _context.GetPropertyPackage();
                if (propertyPackage == null)
                {
                    _logger.Warning("Cannot set BIP: Property package not initialized");
                    return;
                }

                // Access the m_pr field (PengRobinson EOS model) from the property package
                // For Peng-Robinson: propertyPackage.m_pr.InteractionParameters
                var m_prField = propertyPackage.GetType().GetField("m_pr");
                if (m_prField == null)
                {
                    _logger.Warning("Property package does not have m_pr field (not a Peng-Robinson package?)");
                    return;
                }

                var m_pr = m_prField.GetValue(propertyPackage);
                if (m_pr == null)
                {
                    _logger.Warning("m_pr field is null");
                    return;
                }

                // Get the InteractionParameters from m_pr
                // Structure: Dictionary<string, Dictionary<string, PR_IPData>>
                // InteractionParameters is a ReadOnly Property backed by _ip private field
                var interactionParamsProperty = m_pr.GetType().GetProperty("InteractionParameters");
                if (interactionParamsProperty == null)
                {
                    _logger.Warning("m_pr does not have InteractionParameters property");
                    return;
                }

                var interactionParams = interactionParamsProperty.GetValue(m_pr);
                if (interactionParams == null)
                {
                    _logger.Warning("InteractionParameters is null");
                    return;
                }

                // InteractionParameters is a nested dictionary: Dictionary<string, Dictionary<string, InteractionParameter>>
                // We need to create the nested structure: interactionParams[compound1][compound2] = new InteractionParameter { kij = value }

                // Get or create the first-level dictionary for compound1
                var dictType = interactionParams.GetType();
                var containsKeyMethod = dictType.GetMethod("ContainsKey");
                var getItemMethod = dictType.GetMethod("get_Item");
                var setItemMethod = dictType.GetMethod("set_Item");

                bool hasCompound1 = (bool)containsKeyMethod.Invoke(interactionParams, new object[] { compound1 });
                object innerDict;

                if (!hasCompound1)
                {
                    // Create new inner dictionary for compound1
                    var innerDictType = dictType.GetGenericArguments()[1]; // Dictionary<string, InteractionParameter>
                    innerDict = Activator.CreateInstance(innerDictType);
                    setItemMethod.Invoke(interactionParams, new[] { compound1, innerDict });
                }
                else
                {
                    innerDict = getItemMethod.Invoke(interactionParams, new object[] { compound1 });
                }

                // Now set the interaction parameter in the inner dictionary
                var innerSetItemMethod = innerDict.GetType().GetMethod("set_Item");

                // Create InteractionParameter object
                var interactionParamType = innerDict.GetType().GetGenericArguments()[1];
                var interactionParam = Activator.CreateInstance(interactionParamType);

                // Set the kij property (it's a property, not a field)
                var kijProperty = interactionParamType.GetProperty("kij");
                if (kijProperty != null && kijProperty.CanWrite)
                {
                    kijProperty.SetValue(interactionParam, value);
                    innerSetItemMethod.Invoke(innerDict, new[] { compound2, interactionParam });
                    _logger.Information("Set BIP {Compound1}-{Compound2} = {Value}", compound1, compound2, value);
                }
                else
                {
                    _logger.Warning("InteractionParameter does not have writable kij property");
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to set Binary Interaction Parameter for {Compound1}-{Compound2}",
                    compound1, compound2);
            }
        }

        private static (HashSet<string> supportedPackages, Dictionary<string, string> canonicalNames) LoadPropertyPackageInventory()
        {
            var inventoryPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                PropertyPackageInventoryFileName);

            if (!File.Exists(inventoryPath))
            {
                throw new InvalidOperationException(
                    $"DIS-37 property-package inventory missing at {inventoryPath}");
            }

            try
            {
                var inventoryText = File.ReadAllText(inventoryPath);
                var inventoryDocument = Toml.ToModel<PropertyPackageInventoryDocument>(inventoryText);
                if (inventoryDocument?.Packages == null)
                {
                    throw new InvalidOperationException("Expected top-level packages array");
                }

                var supportedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var canonicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (var packageIndex = 0; packageIndex < inventoryDocument.Packages.Count; packageIndex++)
                {
                    var propertyPackage = inventoryDocument.Packages[packageIndex];
                    if (propertyPackage == null)
                    {
                        throw new InvalidOperationException($"Package #{packageIndex} is null");
                    }

                    var canonicalDisplayName = RequireValue(
                        value: propertyPackage.DisplayName,
                        fieldName: "display_name",
                        packageIndex: packageIndex);

                    RequireValue(
                        value: propertyPackage.ModelId,
                        fieldName: "model_id",
                        packageIndex: packageIndex);
                    RequireValue(
                        value: propertyPackage.Description,
                        fieldName: "description",
                        packageIndex: packageIndex);
                    RequireFlashCalculations(
                        flashCalculations: propertyPackage.SupportedFlashCalculations,
                        packageIndex: packageIndex);

                    AddLookupKey(
                        supportedPackages: supportedPackages,
                        canonicalNames: canonicalNames,
                        lookupKey: canonicalDisplayName,
                        canonicalDisplayName: canonicalDisplayName,
                        fieldName: "display_name",
                        packageIndex: packageIndex);

                    if (propertyPackage.Aliases == null)
                    {
                        throw new InvalidOperationException(
                            $"Package #{packageIndex} field 'aliases' must be present");
                    }

                    for (var aliasIndex = 0; aliasIndex < propertyPackage.Aliases.Count; aliasIndex++)
                    {
                        var alias = RequireValue(
                            value: propertyPackage.Aliases[aliasIndex],
                            fieldName: $"aliases[{aliasIndex}]",
                            packageIndex: packageIndex);
                        AddLookupKey(
                            supportedPackages: supportedPackages,
                            canonicalNames: canonicalNames,
                            lookupKey: alias,
                            canonicalDisplayName: canonicalDisplayName,
                            fieldName: "aliases",
                            packageIndex: packageIndex);
                    }
                }

                return (supportedPackages, canonicalNames);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException && ex.Message.StartsWith("DIS-37", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"DIS-37 property-package inventory is malformed at {inventoryPath}: {ex.Message}",
                    ex);
            }
        }

        private static string RequireValue(string value, string fieldName, int packageIndex)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Package #{packageIndex} field '{fieldName}' must be a non-empty string");
            }

            return value;
        }

        private static void AddLookupKey(
            HashSet<string> supportedPackages,
            Dictionary<string, string> canonicalNames,
            string lookupKey,
            string canonicalDisplayName,
            string fieldName,
            int packageIndex)
        {
            if (!supportedPackages.Add(lookupKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate lookup key '{lookupKey}' in field '{fieldName}' for package #{packageIndex}");
            }

            canonicalNames.Add(lookupKey, canonicalDisplayName);
        }

        private static void RequireFlashCalculations(
            List<string> flashCalculations,
            int packageIndex)
        {
            if (flashCalculations == null)
            {
                throw new InvalidOperationException(
                    $"Package #{packageIndex} field 'supported_flash_calculations' must be present");
            }

            for (var flashCalculationIndex = 0; flashCalculationIndex < flashCalculations.Count; flashCalculationIndex++)
            {
                RequireValue(
                    value: flashCalculations[flashCalculationIndex],
                    fieldName: $"supported_flash_calculations[{flashCalculationIndex}]",
                    packageIndex: packageIndex);
            }
        }

        public sealed class PropertyPackageInventoryDocument
        {
            public List<PropertyPackageInventoryPackage> Packages { get; set; } =
                new List<PropertyPackageInventoryPackage>();
        }

        public sealed class PropertyPackageInventoryPackage
        {
            public string ModelId { get; set; }

            public string DisplayName { get; set; }

            public List<string> Aliases { get; set; } =
                new List<string>();

            public string Description { get; set; }

            public List<string> SupportedFlashCalculations { get; set; } =
                new List<string>();
        }
    }
}
