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

        public FlashResultDto CreateFlashResultDto(string calculationType, double temperatureK, double pressurePa, IList<PhaseDto> phases)
        {
            ValidationHelper.ValidateFlashInputs(calculationType, temperatureK, pressurePa);

            return new FlashResultDto
            {
                CalculationType = calculationType,
                TemperatureK = temperatureK,
                PressurePa = pressurePa,
                Phases = phases?.ToList() ?? new List<PhaseDto>()
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
    }
}
