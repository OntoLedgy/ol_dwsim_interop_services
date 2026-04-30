// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;

namespace DwsimWorker.Models
{
    /// <summary>
    /// Configuration for a unit operation (e.g., three-phase separator).
    /// Stores operating parameters as key-value pairs.
    /// </summary>
    public sealed class UnitOpConfig
    {
        /// <summary>
        /// Gets the operating parameters (key-value pairs).
        /// </summary>
        public IReadOnlyDictionary<string, object> Parameters { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitOpConfig"/> class.
        /// </summary>
        /// <param name="parameters">The operating parameters. Can be null or empty for default configuration.</param>
        public UnitOpConfig(IDictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                Parameters = new Dictionary<string, object>();
            }
            else
            {
                // Create defensive copy
                Parameters = new Dictionary<string, object>(parameters);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitOpConfig"/> class with an empty parameter set.
        /// </summary>
        public UnitOpConfig()
            : this(null)
        {
        }

        /// <summary>
        /// Tries to get a parameter value of the specified type.
        /// </summary>
        /// <typeparam name="T">The expected type of the parameter value.</typeparam>
        /// <param name="key">The parameter key.</param>
        /// <param name="value">If the parameter exists and is of type T, contains the value; otherwise, the default value of T.</param>
        /// <returns>True if the parameter exists and is of type T; otherwise, false.</returns>
        public bool TryGetParameter<T>(string key, out T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = default(T);
                return false;
            }

            if (Parameters.TryGetValue(key, out object objValue))
            {
                if (objValue is T typedValue)
                {
                    value = typedValue;
                    return true;
                }

                // Try to convert if types don't match exactly
                try
                {
                    value = (T)Convert.ChangeType(objValue, typeof(T));
                    return true;
                }
                catch
                {
                    value = default(T);
                    return false;
                }
            }

            value = default(T);
            return false;
        }

        /// <summary>
        /// Gets a parameter value of the specified type, or a default value if the parameter doesn't exist.
        /// </summary>
        /// <typeparam name="T">The expected type of the parameter value.</typeparam>
        /// <param name="key">The parameter key.</param>
        /// <param name="defaultValue">The default value to return if the parameter doesn't exist.</param>
        /// <returns>The parameter value if it exists and is of type T; otherwise, the default value.</returns>
        public T GetParameter<T>(string key, T defaultValue = default(T))
        {
            return TryGetParameter<T>(key, out T value) ? value : defaultValue;
        }

        /// <summary>
        /// Checks if a parameter with the specified key exists.
        /// </summary>
        /// <param name="key">The parameter key.</param>
        /// <returns>True if the parameter exists; otherwise, false.</returns>
        public bool HasParameter(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && Parameters.ContainsKey(key);
        }

        /// <summary>
        /// Returns a string representation of the configuration.
        /// </summary>
        /// <returns>A string containing the parameter count.</returns>
        public override string ToString()
        {
            return $"UnitOpConfig: {Parameters.Count} parameter(s)";
        }
    }
}
