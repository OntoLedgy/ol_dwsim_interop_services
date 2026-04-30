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
using DwsimWorker.Contracts.CapeOpen;

namespace DwsimWorker.Utilities
{
    /// <summary>
    /// Provides validation helpers for CAPE-OPEN DTOs.
    /// </summary>
    public static class ValidationHelper
    {
        private const double CompositionTolerance = 1e-6;

        /// <summary>
        /// Validates a material stream DTO for required fields and composition rules.
        /// </summary>
        /// <param name="dto">Material stream DTO to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when dto is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        public static void ValidateMaterialStreamDto(MaterialStreamDto dto)
        {
            var errors = new List<string>();

            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add("Material stream name is required.");
            }

            if (dto.TemperatureK < 0)
            {
                errors.Add("Temperature must be non-negative.");
            }

            if (dto.PressurePa < 0)
            {
                errors.Add("Pressure must be non-negative.");
            }

            if (dto.TotalMolarFlowMolPerS < 0)
            {
                errors.Add("Total molar flow must be non-negative.");
            }

            if (dto.Phases == null || dto.Phases.Count == 0)
            {
                errors.Add("At least one phase is required.");
            }
            else
            {
                foreach (var phase in dto.Phases)
                {
                    ValidatePhaseDto(phase, errors);
                }
            }

            ThrowIfErrors(errors);
        }

        /// <summary>
        /// Validates a property package DTO for required fields.
        /// </summary>
        /// <param name="dto">Property package DTO to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when dto is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        public static void ValidatePropertyPackageDto(PropertyPackageDto dto)
        {
            var errors = new List<string>();

            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add("Property package name is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.PackageType))
            {
                errors.Add("Property package type is required.");
            }

            ThrowIfErrors(errors);
        }

        /// <summary>
        /// Validates a unit operation DTO for required fields.
        /// </summary>
        /// <param name="dto">Unit operation DTO to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when dto is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        public static void ValidateUnitOperationDto(UnitOperationDto dto)
        {
            var errors = new List<string>();

            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add("Unit operation name is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.UnitType))
            {
                errors.Add("Unit operation type is required.");
            }

            ThrowIfErrors(errors);
        }

        /// <summary>
        /// Validates flash calculation inputs.
        /// </summary>
        /// <param name="calculationType">Flash calculation type.</param>
        /// <param name="temperatureK">Temperature in Kelvin.</param>
        /// <param name="pressurePa">Pressure in Pascals.</param>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        public static void ValidateFlashInputs(string calculationType, double temperatureK, double pressurePa)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(calculationType))
            {
                errors.Add("Flash calculation type is required.");
            }

            if (temperatureK < 0)
            {
                errors.Add("Flash temperature must be non-negative.");
            }

            if (pressurePa < 0)
            {
                errors.Add("Flash pressure must be non-negative.");
            }

            ThrowIfErrors(errors);
        }

        private static void ValidatePhaseDto(PhaseDto phase, List<string> errors)
        {
            if (phase == null)
            {
                errors.Add("Phase entry cannot be null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(phase.PhaseLabel))
            {
                errors.Add("Phase label is required.");
            }

            if (phase.PhaseFraction < 0 || phase.PhaseFraction > 1)
            {
                errors.Add($"Phase fraction for '{phase.PhaseLabel}' must be between 0 and 1.");
            }

            if (phase.Composition == null || phase.Composition.Count == 0)
            {
                errors.Add($"Composition is required for phase '{phase.PhaseLabel}'.");
                return;
            }

            ValidateComposition(phase.Composition, phase.PhaseLabel, errors);
        }

        private static void ValidateComposition(IEnumerable<CompoundFractionDto> composition, string phaseLabel, List<string> errors)
        {
            var fractions = composition?.Select(c => c?.MoleFraction ?? 0.0).ToList();
            if (fractions == null || fractions.Count == 0)
            {
                errors.Add($"Composition must include at least one compound for phase '{phaseLabel}'.");
                return;
            }

            if (fractions.Any(value => value < 0))
            {
                errors.Add($"Composition contains negative mole fractions for phase '{phaseLabel}'.");
            }

            var sum = fractions.Sum();
            if (Math.Abs(sum - 1.0) > CompositionTolerance)
            {
                errors.Add($"Composition for phase '{phaseLabel}' must sum to 1.0 within {CompositionTolerance} (sum={sum}).");
            }
        }

        private static void ThrowIfErrors(List<string> errors)
        {
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" ", errors));
            }
        }
    }
}
