using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using DwsimWorker.Engine;

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
        private readonly ILogger _logger;
        private readonly FlowsheetContext _context;
        private string _currentPackageName;

        // Supported property package names
        private static readonly HashSet<string> SupportedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Peng-Robinson",
            "SRK",
            "NRTL",
            "UNIFAC",
            "PR",  // Alias for Peng-Robinson
            "Soave-Redlich-Kwong"  // Full name for SRK
        };

        // Canonical names mapping (for normalization)
        private static readonly Dictionary<string, string> CanonicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Peng-Robinson", "Peng-Robinson" },
            { "PR", "Peng-Robinson" },
            { "SRK", "SRK" },
            { "Soave-Redlich-Kwong", "SRK" },
            { "NRTL", "NRTL" },
            { "UNIFAC", "UNIFAC" }
        };

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
                             $"Supported packages: {string.Join(", ", GetAvailablePackages().Data)}";
                _logger.Warning("Property package validation failed: {PackageName}", packageName);
                return LoadResult.FailureResult(message, new ArgumentException(message, nameof(packageName)));
            }

            try
            {
                // Step 2: Normalize to canonical name
                var canonicalName = GetCanonicalName(packageName);

                // Step 3: Set property package in flowsheet context
                // TODO: In future, call DWSIM API to actually configure the property package
                // For now, we just store the name in memory
                _currentPackageName = canonicalName;

                // Step 4: Log success with structured logging
                _logger.Information("Property package set: {PackageName}", canonicalName);

                return LoadResult.SuccessResult(new List<AssemblyInfo>());
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
    }
}
