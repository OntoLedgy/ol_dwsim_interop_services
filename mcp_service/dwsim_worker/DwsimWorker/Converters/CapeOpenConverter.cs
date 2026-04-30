// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using DwsimWorker.Contracts.CapeOpen;
using DwsimWorker.Utilities;

namespace DwsimWorker.Converters
{
    /// <summary>
    /// Maps CAPE-OPEN interface values to DTOs and applies DTO values back to CAPE-OPEN objects.
    /// </summary>
    public sealed class CapeOpenConverter
    {
        /// <summary>
        /// Converts a CAPE-OPEN material object into a DTO with SI-normalized values.
        /// </summary>
        /// <param name="materialObject">CAPE-OPEN material object instance (ICapeThermoMaterialObject).</param>
        /// <returns>Material stream DTO populated from CAPE-OPEN properties.</returns>
        /// <exception cref="ArgumentNullException">Thrown when materialObject is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when required CAPE-OPEN properties are unavailable.</exception>
        /// <example>
        /// <code>
        /// // Python (pythonnet)
        /// converter = DwsimWorker.Converters.CapeOpenConverter()
        /// dto = converter.ToMaterialStreamDto(materialObject)
        /// </code>
        /// </example>
        public MaterialStreamDto ToMaterialStreamDto(dynamic materialObject)
        {
            if (materialObject == null)
            {
                throw new ArgumentNullException(nameof(materialObject));
            }

            var temperature = GetScalarProperty(
                materialObject,
                "temperature",
                (Func<double, string, double>)UnitConversion.TemperatureToKelvin);
            var pressure = GetScalarProperty(
                materialObject,
                "pressure",
                (Func<double, string, double>)UnitConversion.PressureToPascal);
            var totalFlow = GetScalarProperty(
                materialObject,
                "totalFlow",
                (Func<double, string, double>)UnitConversion.MolarFlowToMolPerSecond);

            return new MaterialStreamDto
            {
                Id = TryGetString(materialObject, "ID") ?? TryGetString(materialObject, "Id"),
                Name = TryGetString(materialObject, "Name"),
                TemperatureK = temperature,
                PressurePa = pressure,
                TotalMolarFlowMolPerS = totalFlow,
                Phases = BuildPhaseDtos(materialObject)
            };
        }

        /// <summary>
        /// Applies DTO values to a CAPE-OPEN material object using CAPE-OPEN property setters.
        /// </summary>
        /// <param name="materialObject">CAPE-OPEN material object instance (ICapeThermoMaterialObject).</param>
        /// <param name="dto">Material stream DTO to apply.</param>
        /// <exception cref="ArgumentNullException">Thrown when materialObject or dto is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        /// <example>
        /// <code>
        /// // Python (pythonnet)
        /// converter = DwsimWorker.Converters.CapeOpenConverter()
        /// converter.ApplyMaterialStreamDto(materialObject, dto)
        /// </code>
        /// </example>
        public void ApplyMaterialStreamDto(dynamic materialObject, MaterialStreamDto dto)
        {
            if (materialObject == null)
            {
                throw new ArgumentNullException(nameof(materialObject));
            }

            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            ValidationHelper.ValidateMaterialStreamDto(dto);

            SetScalarProperty(materialObject, "temperature", dto.TemperatureK);
            SetScalarProperty(materialObject, "pressure", dto.PressurePa);
            SetScalarProperty(materialObject, "totalFlow", dto.TotalMolarFlowMolPerS);
        }

        /// <summary>
        /// Converts a CAPE-OPEN property package into a DTO.
        /// </summary>
        /// <param name="propertyPackage">CAPE-OPEN property package instance (ICapeThermoPropertyPackage).</param>
        /// <returns>Property package DTO.</returns>
        /// <exception cref="ArgumentNullException">Thrown when propertyPackage is null.</exception>
        public PropertyPackageDto ToPropertyPackageDto(dynamic propertyPackage)
        {
            if (propertyPackage == null)
            {
                throw new ArgumentNullException(nameof(propertyPackage));
            }

            return new PropertyPackageDto
            {
                Id = TryGetString(propertyPackage, "ID") ?? TryGetString(propertyPackage, "Id"),
                Name = TryGetString(propertyPackage, "Name"),
                PackageType = TryGetString(propertyPackage, "ComponentName") ?? TryGetString(propertyPackage, "PackageType"),
                Parameters = new Dictionary<string, string>()
            };
        }

        /// <summary>
        /// Validates and applies DTO settings to a CAPE-OPEN property package.
        /// </summary>
        /// <param name="propertyPackage">CAPE-OPEN property package instance (ICapeThermoPropertyPackage).</param>
        /// <param name="dto">Property package DTO to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when propertyPackage or dto is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        public void ApplyPropertyPackageDto(dynamic propertyPackage, PropertyPackageDto dto)
        {
            if (propertyPackage == null)
            {
                throw new ArgumentNullException(nameof(propertyPackage));
            }

            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            ValidationHelper.ValidatePropertyPackageDto(dto);
        }

        /// <summary>
        /// Converts a CAPE-OPEN unit operation into a DTO.
        /// </summary>
        /// <param name="unitOperation">CAPE-OPEN unit operation instance.</param>
        /// <returns>Unit operation DTO.</returns>
        /// <exception cref="ArgumentNullException">Thrown when unitOperation is null.</exception>
        public UnitOperationDto ToUnitOperationDto(dynamic unitOperation)
        {
            if (unitOperation == null)
            {
                throw new ArgumentNullException(nameof(unitOperation));
            }

            return new UnitOperationDto
            {
                Id = TryGetString(unitOperation, "ID") ?? TryGetString(unitOperation, "Id"),
                Name = TryGetString(unitOperation, "Name"),
                UnitType = TryGetString(unitOperation, "ComponentName") ?? TryGetString(unitOperation, "UnitType"),
                Parameters = new Dictionary<string, double>()
            };
        }

        /// <summary>
        /// Validates and applies DTO settings to a CAPE-OPEN unit operation.
        /// </summary>
        /// <param name="unitOperation">CAPE-OPEN unit operation instance.</param>
        /// <param name="dto">Unit operation DTO to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when unitOperation or dto is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        public void ApplyUnitOperationDto(dynamic unitOperation, UnitOperationDto dto)
        {
            if (unitOperation == null)
            {
                throw new ArgumentNullException(nameof(unitOperation));
            }

            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            ValidationHelper.ValidateUnitOperationDto(dto);
        }

        /// <summary>
        /// Creates a flash result DTO from calculated conditions and phase data.
        /// </summary>
        /// <param name="calculationType">Flash calculation type (e.g., TP, PH, PS).</param>
        /// <param name="temperatureK">Temperature in Kelvin.</param>
        /// <param name="pressurePa">Pressure in Pascals.</param>
        /// <param name="phases">Phase results for the flash calculation.</param>
        /// <returns>Flash result DTO.</returns>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        public FlashResultDto CreateFlashResultDto(string calculationType, double temperatureK, double pressurePa, IList<PhaseDto> phases)
        {
            ValidationHelper.ValidateFlashInputs(calculationType, temperatureK, pressurePa);

            return new FlashResultDto
            {
                CalculationType = calculationType,
                TemperatureK = temperatureK,
                PressurePa = pressurePa,
                Phases = phases?.ToList() ?? new List<PhaseDto>(),
                Converged = true
            };
        }

        private static List<PhaseDto> BuildPhaseDtos(dynamic materialObject)
        {
            var composition = GetComposition(materialObject, "composition");
            var phase = new PhaseDto
            {
                PhaseLabel = "Overall",
                PhaseFraction = 1.0,
                Composition = composition,
                Properties = new Dictionary<string, double>()
            };

            return new List<PhaseDto> { phase };
        }

        private static List<CompoundFractionDto> GetComposition(dynamic materialObject, string propertyName)
        {
            try
            {
                var values = ToDoubleArray(TryGetProp(materialObject, propertyName));
                var compoundIds = TryGetCompoundIds(materialObject);
                var composition = new List<CompoundFractionDto>();

                for (int i = 0; i < values.Length; i++)
                {
                    composition.Add(new CompoundFractionDto
                    {
                        Compound = compoundIds != null && i < compoundIds.Length ? compoundIds[i] : $"Component{i + 1}",
                        MoleFraction = values[i]
                    });
                }

                return composition;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read composition from CAPE-OPEN property '{propertyName}'.", ex);
            }
        }

        private static string[] TryGetCompoundIds(dynamic materialObject)
        {
            try
            {
                return materialObject.GetCompoundIds();
            }
            catch
            {
                return null;
            }
        }

        private static double GetScalarProperty(dynamic materialObject, string propertyName, Func<double, string, double> normalize)
        {
            if (!CapeOpenPropertyRegistry.TryGet(propertyName, out var definition))
            {
                throw new InvalidOperationException($"Unsupported CAPE-OPEN property '{propertyName}'.");
            }

            var unit = TryGetPropUnit(materialObject, definition.CanonicalName) ?? definition.ExpectedUnit;
            var value = Convert.ToDouble(TryGetProp(materialObject, definition.CanonicalName));
            return normalize(value, unit);
        }

        private static void SetScalarProperty(dynamic materialObject, string propertyName, double value)
        {
            if (!CapeOpenPropertyRegistry.TryGet(propertyName, out var definition))
            {
                throw new InvalidOperationException($"Unsupported CAPE-OPEN property '{propertyName}'.");
            }

            TrySetProp(materialObject, definition.CanonicalName, value);
        }

        private static object TryGetProp(dynamic target, string propertyName)
        {
            try
            {
                return target.GetProp(propertyName);
            }
            catch
            {
                try
                {
                    return target.GetProp(propertyName, "Overall", "Mixture");
                }
                catch
                {
                    return target.GetProp(propertyName, "Overall");
                }
            }
        }

        private static void TrySetProp(dynamic target, string propertyName, double value)
        {
            try
            {
                target.SetProp(propertyName, value);
            }
            catch
            {
                try
                {
                    target.SetProp(propertyName, "Overall", "Mixture", value);
                }
                catch
                {
                    target.SetProp(propertyName, "Overall", value);
                }
            }
        }

        private static string TryGetPropUnit(dynamic target, string propertyName)
        {
            try
            {
                return target.GetPropUnit(propertyName);
            }
            catch
            {
                try
                {
                    return target.GetPropUnits(propertyName);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static string TryGetString(dynamic target, string propertyName)
        {
            try
            {
                var value = target.GetType().GetProperty(propertyName)?.GetValue(target);
                return value?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static double[] ToDoubleArray(object value)
        {
            if (value is double[] doubles)
            {
                return doubles;
            }

            if (value is Array array)
            {
                var result = new double[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    result[i] = Convert.ToDouble(array.GetValue(i));
                }

                return result;
            }

            return new[] { Convert.ToDouble(value) };
        }

        /// <summary>
        /// Converts a DWSIM material stream to a DTO by reading directly from DWSIM's internal
        /// Phases dictionary. This bypasses CAPE-OPEN interfaces which may not be available
        /// for streams that have been flashed but not fully calculated by the flowsheet solver.
        /// </summary>
        /// <param name="materialStream">DWSIM MaterialStream object.</param>
        /// <returns>Material stream DTO populated from DWSIM internal data.</returns>
        public MaterialStreamDto ToDwsimMaterialStreamDto(dynamic materialStream)
        {
            if (materialStream == null)
            {
                throw new ArgumentNullException(nameof(materialStream));
            }

            var streamType = materialStream.GetType();

            // Get basic stream properties
            var name = TryGetString(materialStream, "GraphicObjectName") ?? TryGetString(materialStream, "Name");
            var id = TryGetString(materialStream, "Name");

            // Get Phases dictionary
            var phasesProperty = streamType.GetProperty("Phases");
            if (phasesProperty == null)
            {
                throw new InvalidOperationException("MaterialStream does not have a Phases property");
            }

            var phases = phasesProperty.GetValue(materialStream) as System.Collections.IDictionary;
            if (phases == null)
            {
                throw new InvalidOperationException("Could not get Phases dictionary from MaterialStream");
            }

            // Get overall phase (phase 0) for temperature, pressure, flow
            double temperature = 0, pressure = 0, molarFlow = 0;
            var phaseDtos = new List<PhaseDto>();

            foreach (System.Collections.DictionaryEntry entry in phases)
            {
                var phaseId = Convert.ToInt32(entry.Key);
                var phase = entry.Value;
                var phaseType = phase.GetType();

                // Get phase properties
                var propsProperty = phaseType.GetProperty("Properties");
                var props = propsProperty?.GetValue(phase);

                if (props != null)
                {
                    var propsType = props.GetType();
                    var molarFracProp = propsType.GetProperty("molarfraction");
                    var molarFraction = molarFracProp != null ? Convert.ToDouble(molarFracProp.GetValue(props) ?? 0) : 0;

                    if (phaseId == 0) // Overall phase
                    {
                        var tempProp = propsType.GetProperty("temperature");
                        var presProp = propsType.GetProperty("pressure");
                        var flowProp = propsType.GetProperty("molarflow");

                        temperature = tempProp != null ? Convert.ToDouble(tempProp.GetValue(props) ?? 0) : 0;
                        pressure = presProp != null ? Convert.ToDouble(presProp.GetValue(props) ?? 0) : 0;
                        molarFlow = flowProp != null ? Convert.ToDouble(flowProp.GetValue(props) ?? 0) : 0;
                    }

                    // Only add phases with non-zero molar fraction (excluding overall phase 0)
                    if (phaseId > 0 && molarFraction > 1e-10)
                    {
                        var phaseLabel = GetDwsimPhaseLabel(phaseId);
                        var compoundDtos = GetDwsimPhaseComposition(phase, phaseType);

                        phaseDtos.Add(new PhaseDto
                        {
                            PhaseLabel = phaseLabel,
                            PhaseFraction = molarFraction,
                            Composition = compoundDtos,
                            Properties = GetDwsimPhaseProperties(props, propsType)
                        });
                    }
                }
            }

            return new MaterialStreamDto
            {
                Id = id,
                Name = name,
                TemperatureK = temperature,
                PressurePa = pressure,
                TotalMolarFlowMolPerS = molarFlow,
                Phases = phaseDtos
            };
        }

        private static string GetDwsimPhaseLabel(int phaseId)
        {
            // DWSIM phase IDs: 0=Overall, 1=Liquid1, 2=Vapor, 3=Liquid2, 4=Liquid3, 5=Aqueous, 6=Solid, 7=Liquid
            return phaseId switch
            {
                0 => "Overall",
                1 => "Liquid1",
                2 => "Vapor",
                3 => "Liquid2",
                4 => "Liquid3",
                5 => "Aqueous",
                6 => "Solid",
                7 => "Liquid",
                _ => $"Phase{phaseId}"
            };
        }

        private static List<CompoundFractionDto> GetDwsimPhaseComposition(object phase, Type phaseType)
        {
            var result = new List<CompoundFractionDto>();

            var compoundsProperty = phaseType.GetProperty("Compounds");
            if (compoundsProperty == null)
            {
                return result;
            }

            var compounds = compoundsProperty.GetValue(phase) as System.Collections.IDictionary;
            if (compounds == null)
            {
                return result;
            }

            foreach (System.Collections.DictionaryEntry entry in compounds)
            {
                var compoundName = entry.Key?.ToString();
                var compound = entry.Value;

                if (compound != null)
                {
                    var compoundType = compound.GetType();
                    var moleFracProp = compoundType.GetProperty("MoleFraction");
                    var moleFraction = moleFracProp != null ? Convert.ToDouble(moleFracProp.GetValue(compound) ?? 0) : 0;

                    result.Add(new CompoundFractionDto
                    {
                        Compound = compoundName,
                        MoleFraction = moleFraction
                    });
                }
            }

            return result;
        }

        private static Dictionary<string, double> GetDwsimPhaseProperties(object props, Type propsType)
        {
            var result = new Dictionary<string, double>();

            // List of important thermodynamic properties to extract
            var propertyNames = new[]
            {
                "enthalpy", "entropy", "density", "molecularWeight",
                "compressibilityFactor", "heatCapacityCp", "heatCapacityCv",
                "thermalConductivity", "viscosity", "surfaceTension"
            };

            foreach (var propName in propertyNames)
            {
                try
                {
                    var propInfo = propsType.GetProperty(propName);
                    if (propInfo != null)
                    {
                        var value = propInfo.GetValue(props);
                        if (value != null)
                        {
                            var doubleValue = Convert.ToDouble(value);
                            if (!double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue))
                            {
                                result[propName] = doubleValue;
                            }
                        }
                    }
                }
                catch
                {
                    // Skip properties that can't be read
                }
            }

            return result;
        }
    }
}
