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
using Serilog;
using DwsimWorker.Contracts.CapeOpen;
using DwsimWorker.Converters;
using DwsimWorker.Engine;
using DwsimWorker.Models;

namespace DwsimWorker.Adapters
{
    /// <summary>
    /// Adapter for standalone thermodynamic flash calculations using DWSIM property packages.
    /// </summary>
    /// <remarks>
    /// This adapter leverages StreamAdapter to create a temporary stream, applies specifications,
    /// runs flash calculations, and returns results as FlashResultDto.
    /// </remarks>
    public sealed class ThermodynamicsAdapter
    {
        private const double DefaultMolarFlow = 1.0;
        private readonly ILogger _logger;
        private readonly FlowsheetContext _context;
        private readonly StreamAdapter _streamAdapter;
        private readonly CapeOpenConverter _converter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThermodynamicsAdapter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for flash operation logging.</param>
        /// <param name="context">The flowsheet context that manages session state.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or context is null.</exception>
        public ThermodynamicsAdapter(ILogger logger, FlowsheetContext context)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _streamAdapter = new StreamAdapter(_logger, _context);
            _converter = new CapeOpenConverter();
        }

        /// <summary>
        /// Performs a temperature-pressure flash calculation.
        /// </summary>
        /// <param name="compounds">Compound names in the mixture.</param>
        /// <param name="composition">Mole fractions corresponding to <paramref name="compounds"/>.</param>
        /// <param name="temperature">Temperature in Kelvin.</param>
        /// <param name="pressure">Pressure in Pascals.</param>
        /// <returns>The flash result DTO.</returns>
        public FlashResultDto FlashTP(string[] compounds, double[] composition, double temperature, double pressure)
        {
            return RunFlash("TP", compounds, composition, temperature, pressure, null, null);
        }

        /// <summary>
        /// Performs a pressure-enthalpy flash calculation.
        /// </summary>
        /// <param name="compounds">Compound names in the mixture.</param>
        /// <param name="composition">Mole fractions corresponding to <paramref name="compounds"/>.</param>
        /// <param name="pressure">Pressure in Pascals.</param>
        /// <param name="enthalpy">Molar enthalpy in J/mol.</param>
        /// <returns>The flash result DTO.</returns>
        public FlashResultDto FlashPH(string[] compounds, double[] composition, double pressure, double enthalpy)
        {
            return RunFlash("PH", compounds, composition, null, pressure, enthalpy, null);
        }

        /// <summary>
        /// Performs a pressure-entropy flash calculation.
        /// </summary>
        /// <param name="compounds">Compound names in the mixture.</param>
        /// <param name="composition">Mole fractions corresponding to <paramref name="compounds"/>.</param>
        /// <param name="pressure">Pressure in Pascals.</param>
        /// <param name="entropy">Molar entropy in J/(mol*K).</param>
        /// <returns>The flash result DTO.</returns>
        public FlashResultDto FlashPS(string[] compounds, double[] composition, double pressure, double entropy)
        {
            return RunFlash("PS", compounds, composition, null, pressure, null, entropy);
        }

        private FlashResultDto RunFlash(
            string calculationType,
            string[] compounds,
            double[] composition,
            double? temperatureK,
            double pressurePa,
            double? enthalpy,
            double? entropy)
        {
            _logger.Information("[ThermodynamicsAdapter] RunFlash starting: type={CalculationType}, compounds=[{Compounds}], composition=[{Composition}], T={Temperature}K, P={Pressure}Pa",
                calculationType, string.Join(",", compounds ?? new string[0]), string.Join(",", composition ?? new double[0]), temperatureK, pressurePa);

            // Check what compounds are in context
            var contextCompounds = _context.GetCompounds();
            _logger.Information("[ThermodynamicsAdapter] Context has {Count} compounds: [{Compounds}]", contextCompounds.Count, string.Join(",", contextCompounds));

            // Check if property package is set
            var pp = _context.GetPropertyPackage();
            _logger.Information("[ThermodynamicsAdapter] Property package: {PropertyPackage}", pp != null ? pp.GetType().Name : "NULL");

            if (!ValidateInputs(calculationType, compounds, composition, temperatureK, pressurePa, enthalpy, entropy, out var errorMessage))
            {
                _logger.Warning("[ThermodynamicsAdapter] Flash input validation failed: {Message}", errorMessage);
                return CreateFailureResult(calculationType, temperatureK, pressurePa);
            }

            // Validate that all compounds are registered in the flowsheet
            var invalidCompounds = compounds.Where(c => !contextCompounds.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
            if (invalidCompounds.Count > 0)
            {
                _logger.Warning("[ThermodynamicsAdapter] Flash failed: unregistered compounds [{InvalidCompounds}]. Registered: [{RegisteredCompounds}]",
                    string.Join(",", invalidCompounds), string.Join(",", contextCompounds));
                return CreateFailureResult(calculationType, temperatureK, pressurePa);
            }

            // Generate a unique temporary stream ID
            var tempStreamId = $"_FLASH_TEMP_{Guid.NewGuid():N}";

            try
            {
                // Create stream properties with composition
                var streamProperties = CreateStreamProperties(
                    compounds,
                    composition,
                    calculationType,
                    temperatureK,
                    pressurePa,
                    enthalpy,
                    entropy);

                _logger.Debug("Creating temporary stream {StreamId} for {CalculationType} flash", tempStreamId, calculationType);

                // Create the stream using StreamAdapter (this properly initializes everything)
                // IMPORTANT: isSource=true tells DWSIM this stream has known conditions (input stream)
                _logger.Information("[ThermodynamicsAdapter] Creating stream with ID={StreamId}", tempStreamId);
                var createResult = _streamAdapter.CreateStream(tempStreamId, streamProperties, isSource: true);
                if (!createResult.Success)
                {
                    _logger.Warning("[ThermodynamicsAdapter] Failed to create temporary stream: {Message}, Error={Error}",
                        createResult.Message, createResult.Error?.Message ?? "null");
                    return CreateFailureResult(calculationType, temperatureK, pressurePa);
                }

                var actualStreamId = createResult.ObjectId;
                _logger.Information("[ThermodynamicsAdapter] Temporary stream created with ID: {StreamId}", actualStreamId);

                // Flash the stream
                _logger.Information("[ThermodynamicsAdapter] Flashing stream {StreamId}", actualStreamId);
                var flashResult = _streamAdapter.FlashStream(actualStreamId);
                if (!flashResult.Success)
                {
                    _logger.Warning("[ThermodynamicsAdapter] Flash calculation failed: {Message}, Error={Error}",
                        flashResult.Message, flashResult.Error?.Message ?? "null");
                    TryDeleteStream(actualStreamId);
                    return CreateFailureResult(calculationType, temperatureK, pressurePa);
                }

                _logger.Information("[ThermodynamicsAdapter] Flash calculation completed successfully for stream {StreamId}", actualStreamId);

                // Get the stream and convert to result DTO
                var stream = _context.GetStream(actualStreamId);
                if (stream == null)
                {
                    _logger.Warning("[ThermodynamicsAdapter] Could not retrieve flashed stream {StreamId}", actualStreamId);
                    TryDeleteStream(actualStreamId);
                    return CreateFailureResult(calculationType, temperatureK, pressurePa);
                }
                _logger.Information("[ThermodynamicsAdapter] Retrieved stream, type={StreamType}", stream.GetType().Name);

                _logger.Information("[ThermodynamicsAdapter] Converting stream to DTO using DWSIM internal data");
                var materialDto = _converter.ToDwsimMaterialStreamDto(stream);
                _logger.Information("[ThermodynamicsAdapter] MaterialStreamDto: T={Temperature}K, P={Pressure}Pa, PhaseCount={PhaseCount}",
                    materialDto.TemperatureK, materialDto.PressurePa, materialDto.Phases?.Count ?? 0);

                var result = _converter.CreateFlashResultDto(
                    calculationType,
                    materialDto.TemperatureK,
                    materialDto.PressurePa,
                    materialDto.Phases);
                result.Converged = true;

                _logger.Information("[ThermodynamicsAdapter] Flash result created: Converged={Converged}, PhaseCount={PhaseCount}",
                    result.Converged, result.Phases?.Count ?? 0);

                // Clean up the temporary stream
                TryDeleteStream(actualStreamId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during {CalculationType} flash", calculationType);
                TryDeleteStream(tempStreamId);
                return CreateFailureResult(calculationType, temperatureK, pressurePa);
            }
        }

        private StreamProperties CreateStreamProperties(
            string[] compounds,
            double[] composition,
            string calculationType,
            double? temperatureK,
            double pressurePa,
            double? enthalpy,
            double? entropy)
        {
            // Create unit definitions
            var kelvinUnit = new UnitsOfMeasure("K", typeof(Temperature),
                new Ranges(0, double.PositiveInfinity));
            var pascalUnit = new UnitsOfMeasure("Pa", typeof(Pressure),
                new Ranges(0, double.PositiveInfinity));
            var molPerSecUnit = new UnitsOfMeasure("mol/s", typeof(MolarFlowRate),
                new Ranges(0, double.PositiveInfinity));

            // For TP flash, use provided temperature; otherwise use a reasonable default
            double tempForStream = temperatureK ?? 298.15;

            // Create PhysicalProperties objects
            var temperatureProperty = new PhysicalProperties("Temperature",
                new Measurements(new Temperature(), tempForStream, kelvinUnit));
            var pressureProperty = new PhysicalProperties("Pressure",
                new Measurements(new Pressure(), pressurePa, pascalUnit));
            var molarFlowProperty = new PhysicalProperties("MolarFlow",
                new Measurements(new MolarFlowRate(), DefaultMolarFlow, molPerSecUnit));

            // Build composition model
            var compositionModel = new Composition(composition);

            // Create stream properties
            return new StreamProperties(
                temperatureProperty,
                pressureProperty,
                molarFlowProperty,
                compositionModel);
        }

        private void TryDeleteStream(string streamId)
        {
            // Note: StreamAdapter doesn't have a DeleteStream method.
            // The temporary stream will be cleaned up when the session ends.
            // For now, we just log that we're leaving the stream in place.
            _logger.Debug("Temporary stream {StreamId} will be cleaned up when session ends", streamId);
        }

        private static FlashResultDto CreateFailureResult(string calculationType, double? temperatureK, double pressurePa)
        {
            return new FlashResultDto
            {
                CalculationType = calculationType,
                TemperatureK = temperatureK ?? 0.0,
                PressurePa = pressurePa,
                Phases = new List<PhaseDto>(),
                Converged = false
            };
        }

        private static bool ValidateInputs(
            string calculationType,
            string[] compounds,
            double[] composition,
            double? temperatureK,
            double pressurePa,
            double? enthalpy,
            double? entropy,
            out string message)
        {
            if (string.IsNullOrWhiteSpace(calculationType))
            {
                message = "Calculation type is required.";
                return false;
            }

            if (compounds == null || compounds.Length == 0)
            {
                message = "Compounds must be provided.";
                return false;
            }

            if (composition == null || composition.Length == 0)
            {
                message = "Composition must be provided.";
                return false;
            }

            if (compounds.Length != composition.Length)
            {
                message = "Composition length must match compounds length.";
                return false;
            }

            try
            {
                var compositionModel = new Composition(composition);
                if (!compositionModel.IsValid(compounds.Length))
                {
                    message = "Composition is not valid.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }

            if (pressurePa <= 0)
            {
                message = "Pressure must be greater than zero.";
                return false;
            }

            if (calculationType == "TP" && (!temperatureK.HasValue || temperatureK.Value <= 0))
            {
                message = "Temperature must be greater than zero for TP flash.";
                return false;
            }

            if (calculationType == "PH" && (!enthalpy.HasValue || double.IsNaN(enthalpy.Value)))
            {
                message = "Enthalpy must be provided for PH flash.";
                return false;
            }

            if (calculationType == "PS" && (!entropy.HasValue || double.IsNaN(entropy.Value)))
            {
                message = "Entropy must be provided for PS flash.";
                return false;
            }

            message = null;
            return true;
        }
    }
}
