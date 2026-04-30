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
using System.IO;
using System.Linq;
using System.Reflection;
using Serilog;

namespace DwsimWorker.Utilities
{
    /// <summary>
    /// Registers an AssemblyResolve handler to load dependencies from configured paths.
    /// </summary>
    public static class AssemblyResolver
    {
        private static bool _initialized;

        /// <summary>
        /// Register the resolver once for the current AppDomain.
        /// </summary>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <param name="basePath">Primary DWSIM assembly path.</param>
        /// <param name="dependencyPaths">Optional additional probe paths.</param>
        public static void Register(ILogger logger, string basePath, IEnumerable<string> dependencyPaths)
        {
            if (_initialized)
                return;

            _initialized = true;

            var paths = new List<string>();
            if (!string.IsNullOrWhiteSpace(basePath))
            {
                paths.Add(basePath);
            }

            if (dependencyPaths != null)
            {
                paths.AddRange(dependencyPaths.Where(path => !string.IsNullOrWhiteSpace(path)));
            }

            var distinctPaths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            logger.Information("Assembly resolver registered with paths: {Paths}", string.Join(";", distinctPaths));

            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                try
                {
                    var requestedName = new AssemblyName(args.Name).Name;
                    if (string.IsNullOrWhiteSpace(requestedName))
                        return null;

                    var fileName = requestedName + ".dll";
                    foreach (var path in distinctPaths)
                    {
                        var candidate = Path.Combine(path, fileName);
                        if (File.Exists(candidate))
                        {
                            logger.Debug("Resolving assembly {Assembly} from {Path}", requestedName, candidate);
                            return Assembly.LoadFrom(candidate);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Assembly resolve failed for {Assembly}", args.Name);
                }

                return null;
            };
        }
    }
}
