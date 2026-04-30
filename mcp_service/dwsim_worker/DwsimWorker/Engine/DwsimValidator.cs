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
using System.Linq;
using System.Reflection;
using Serilog;

namespace DwsimWorker.Engine
{
    /// <summary>
    /// Validates that loaded DWSIM assemblies are functional by instantiating core types.
    /// This ensures assemblies are not just loaded, but actually usable.
    /// </summary>
    public sealed class DwsimValidator
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Type names to validate by instantiation.
        /// </summary>
        private static readonly string[] TypesToValidate = new[]
        {
            "DWSIM.FlowsheetBase.FlowsheetBase",  // Headless flowsheet base class
            "DWSIM.Thermodynamics.Streams.MaterialStream"
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="DwsimValidator"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for validation logging.</param>
        public DwsimValidator(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validates DWSIM assemblies by attempting to instantiate core types.
        /// Tests both Flowsheet and MaterialStream creation.
        /// </summary>
        /// <returns>A ValidationResult indicating success or failure with details.</returns>
        public ValidationResult ValidateInstantiation()
        {
            _logger.Information("Starting DWSIM assembly validation...");

            var validatedTypes = new List<string>();
            var errors = new List<Exception>();

            try
            {
                // Validate Flowsheet creation
                var flowsheetResult = ValidateFlowsheetCreation();
                if (flowsheetResult.Success)
                {
                    validatedTypes.AddRange(flowsheetResult.ValidatedTypes);
                    _logger.Information("Flowsheet validation succeeded");
                }
                else
                {
                    _logger.Warning("Flowsheet validation failed: {Message}", flowsheetResult.Message);
                    errors.Add(flowsheetResult.Error);
                }

                // Validate MaterialStream creation
                var streamResult = ValidateMaterialStreamCreation();
                if (streamResult.Success)
                {
                    validatedTypes.AddRange(streamResult.ValidatedTypes);
                    _logger.Information("MaterialStream validation succeeded");
                }
                else
                {
                    _logger.Warning("MaterialStream validation failed: {Message}", streamResult.Message);
                    errors.Add(streamResult.Error);
                }

                // Return overall result
                if (errors.Any())
                {
                    var firstError = errors.First();
                    var message = $"Validation partially failed. Validated {validatedTypes.Count} type(s), {errors.Count} error(s)";
                    _logger.Error("Validation completed with errors: {Message}", message);
                    return ValidationResult.PartialFailureResult(message, validatedTypes, firstError);
                }

                _logger.Information("All validations succeeded. Validated types: {ValidatedTypes}",
                    string.Join(", ", validatedTypes));
                return ValidationResult.SuccessResult(validatedTypes);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during validation");
                return ValidationResult.FailureResult("Validation failed with unexpected error", ex);
            }
        }

        /// <summary>
        /// Validates that DWSIM.SharedClasses.FOSSEEFlowsheet can be instantiated.
        /// </summary>
        /// <returns>A ValidationResult for Flowsheet instantiation.</returns>
        public ValidationResult ValidateFlowsheetCreation()
        {
            const string typeName = "DWSIM.FlowsheetBase.FlowsheetBase";
            const string fallbackTypeName = "DWSIM.SharedClasses.FOSSEEFlowsheet";

            try
            {
                _logger.Debug("Attempting to validate type: {TypeName}", typeName);

                // Find the type in loaded assemblies
                var flowsheetType = FindType(typeName);
                if (flowsheetType != null && flowsheetType.IsAbstract)
                {
                    flowsheetType = null;
                }

                if (flowsheetType == null)
                {
                    flowsheetType = FindType(fallbackTypeName);
                    if (flowsheetType != null && flowsheetType.IsAbstract)
                    {
                        flowsheetType = null;
                    }
                }
                if (flowsheetType == null)
                {
                    var message = $"Type '{typeName}' not found in loaded assemblies";
                    _logger.Error(message);
                    return ValidationResult.FailureResultForType(typeName,
                        new TypeLoadException(message));
                }

                // Attempt to create an instance
                var instance = Activator.CreateInstance(flowsheetType);
                if (instance == null)
                {
                    var message = $"Failed to create instance of '{typeName}' (returned null)";
                    _logger.Error(message);
                    return ValidationResult.FailureResultForType(typeName,
                        new InvalidOperationException(message));
                }

                _logger.Information("Successfully validated {TypeName}", typeName);
                return ValidationResult.SuccessResult(new[] { typeName });
            }
            catch (TypeLoadException ex)
            {
                _logger.Error(ex, "Type load exception for {TypeName}", typeName);
                return ValidationResult.FailureResultForType(typeName, ex);
            }
            catch (MissingMethodException ex)
            {
                _logger.Error(ex, "Missing constructor for {TypeName}", typeName);
                return ValidationResult.FailureResultForType(typeName, ex);
            }
            catch (TargetInvocationException ex)
            {
                _logger.Error(ex, "Exception during construction of {TypeName}", typeName);
                return ValidationResult.FailureResultForType(typeName, ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error validating {TypeName}", typeName);
                return ValidationResult.FailureResultForType(typeName, ex);
            }
        }

        /// <summary>
        /// Validates that DWSIM.Thermodynamics.Streams.MaterialStream can be instantiated.
        /// </summary>
        /// <returns>A ValidationResult for MaterialStream instantiation.</returns>
        public ValidationResult ValidateMaterialStreamCreation()
        {
            const string typeName = "DWSIM.Thermodynamics.Streams.MaterialStream";

            try
            {
                _logger.Debug("Attempting to validate type: {TypeName}", typeName);

                // Find the type in loaded assemblies
                var streamType = FindType(typeName);
                if (streamType == null)
                {
                    var message = $"Type '{typeName}' not found in loaded assemblies";
                    _logger.Error(message);
                    return ValidationResult.FailureResultForType(typeName,
                        new TypeLoadException(message));
                }

                // Attempt to create an instance
                // MaterialStream might require parameters, try default constructor first
                var instance = TryCreateInstance(streamType);
                if (instance == null)
                {
                    var message = $"Failed to create instance of '{typeName}' (returned null)";
                    _logger.Error(message);
                    return ValidationResult.FailureResultForType(typeName,
                        new InvalidOperationException(message));
                }

                _logger.Information("Successfully validated {TypeName}", typeName);
                return ValidationResult.SuccessResult(new[] { typeName });
            }
            catch (TypeLoadException ex)
            {
                _logger.Error(ex, "Type load exception for {TypeName}", typeName);
                return ValidationResult.FailureResultForType(typeName, ex);
            }
            catch (MissingMethodException ex)
            {
                _logger.Error(ex, "Missing constructor for {TypeName}", typeName);
                return ValidationResult.FailureResultForType(typeName, ex);
            }
            catch (TargetInvocationException ex)
            {
                _logger.Error(ex, "Exception during construction of {TypeName}", typeName);
                return ValidationResult.FailureResultForType(typeName, ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error validating {TypeName}", typeName);
                return ValidationResult.FailureResultForType(typeName, ex);
            }
        }

        /// <summary>
        /// Finds a type by its full name in all loaded assemblies.
        /// </summary>
        /// <param name="fullTypeName">The full type name (e.g., "DWSIM.SharedClasses.Flowsheet").</param>
        /// <returns>The Type if found; otherwise, null.</returns>
        private Type FindType(string fullTypeName)
        {
            // First try to get the type directly
            var type = Type.GetType(fullTypeName);
            if (type != null)
                return type;

            // Search through all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(fullTypeName);
                    if (type != null)
                    {
                        _logger.Debug("Found type {TypeName} in assembly {AssemblyName}",
                            fullTypeName, assembly.FullName);
                        return type;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Error searching for type in assembly {AssemblyName}", assembly.FullName);
                }
            }

            _logger.Warning("Type {TypeName} not found in any loaded assembly", fullTypeName);
            return null;
        }

        /// <summary>
        /// Attempts to create an instance of a type, trying default constructor first,
        /// then parameterless constructors.
        /// </summary>
        /// <param name="type">The type to instantiate.</param>
        /// <returns>An instance of the type, or null if instantiation fails.</returns>
        private object TryCreateInstance(Type type)
        {
            try
            {
                // Try default Activator.CreateInstance
                return Activator.CreateInstance(type);
            }
            catch (MissingMethodException)
            {
                // No parameterless constructor, try other constructors with default parameters
                var constructors = type.GetConstructors();
                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    if (parameters.All(p => p.HasDefaultValue))
                    {
                        try
                        {
                            var args = parameters.Select(p => p.DefaultValue).ToArray();
                            return constructor.Invoke(args);
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug(ex, "Failed to invoke constructor with default parameters");
                        }
                    }
                }

                // Last resort: try constructor with null parameters
                if (constructors.Length > 0)
                {
                    var firstConstructor = constructors.First();
                    var paramCount = firstConstructor.GetParameters().Length;
                    var nullArgs = new object[paramCount];
                    try
                    {
                        return firstConstructor.Invoke(nullArgs);
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(ex, "Failed to invoke constructor with null parameters");
                    }
                }

                return null;
            }
        }
    }
}
