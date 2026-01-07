using System;
using System.Collections.Generic;
using Xunit;
using DwsimWorker.Converters;
using DwsimWorker.Contracts.CapeOpen;

namespace DwsimWorker.Tests
{
    public class CapeOpenConverterTests
    {
        [Fact]
        public void ToMaterialStreamDto_ConvertsUnitsToSi()
        {
            var materialObject = new FakeMaterialObject
            {
                TemperatureValue = 25.0,
                TemperatureUnit = "C",
                PressureValue = 1.0,
                PressureUnit = "bar",
                TotalFlowValue = 3600.0,
                TotalFlowUnit = "mol/h",
                CompositionValues = new[] { 0.7, 0.3 }
            };

            var converter = new CapeOpenConverter();
            var dto = converter.ToMaterialStreamDto(materialObject);

            Assert.Equal(298.15, dto.TemperatureK, 2);
            Assert.Equal(100000, dto.PressurePa, 0);
            Assert.Equal(1.0, dto.TotalMolarFlowMolPerS, 6);
            Assert.Single(dto.Phases);
            Assert.Equal(2, dto.Phases[0].Composition.Count);
        }

        [Fact]
        public void ApplyMaterialStreamDto_SetsCanonicalProperties()
        {
            var materialObject = new FakeMaterialObject();
            var dto = new MaterialStreamDto
            {
                Name = "Feed",
                TemperatureK = 300.0,
                PressurePa = 150000.0,
                TotalMolarFlowMolPerS = 2.0,
                Phases = new List<PhaseDto>
                {
                    new PhaseDto
                    {
                        PhaseLabel = "Overall",
                        PhaseFraction = 1.0,
                        Composition = new List<CompoundFractionDto>
                        {
                            new CompoundFractionDto { Compound = "Methane", MoleFraction = 1.0 }
                        }
                    }
                }
            };

            var converter = new CapeOpenConverter();
            converter.ApplyMaterialStreamDto(materialObject, dto);

            Assert.Equal(300.0, materialObject.LastSet["temperature"]);
            Assert.Equal(150000.0, materialObject.LastSet["pressure"]);
            Assert.Equal(2.0, materialObject.LastSet["totalFlow"]);
        }

        public sealed class FakeMaterialObject
        {
            public double TemperatureValue { get; set; }

            public string TemperatureUnit { get; set; }

            public double PressureValue { get; set; }

            public string PressureUnit { get; set; }

            public double TotalFlowValue { get; set; }

            public string TotalFlowUnit { get; set; }

            public double[] CompositionValues { get; set; }

            public Dictionary<string, double> LastSet { get; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            public string[] GetCompoundIds()
            {
                return new[] { "Methane", "Ethane" };
            }

            public object GetProp(string propertyName)
            {
                return GetProp(propertyName, "Overall", "Mixture");
            }

            public object GetProp(string propertyName, string phaseName)
            {
                return GetProp(propertyName, phaseName, "Mixture");
            }

            public object GetProp(string propertyName, string phaseName, string basis)
            {
                switch (propertyName)
                {
                    case "temperature":
                        return TemperatureValue;
                    case "pressure":
                        return PressureValue;
                    case "totalFlow":
                        return TotalFlowValue;
                    case "composition":
                        return CompositionValues;
                    default:
                        throw new InvalidOperationException($"Unsupported property {propertyName}");
                }
            }

            public string GetPropUnit(string propertyName)
            {
                switch (propertyName)
                {
                    case "temperature":
                        return TemperatureUnit;
                    case "pressure":
                        return PressureUnit;
                    case "totalFlow":
                        return TotalFlowUnit;
                    default:
                        return null;
                }
            }

            public void SetProp(string propertyName, double value)
            {
                LastSet[propertyName] = value;
            }

            public void SetProp(string propertyName, string phaseName, string basis, double value)
            {
                LastSet[propertyName] = value;
            }

            public void SetProp(string propertyName, string phaseName, double value)
            {
                LastSet[propertyName] = value;
            }
        }
    }
}
