// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Xunit;
using DwsimWorker.Contracts.CapeOpen;
using DwsimWorker.Utilities;

namespace DwsimWorker.Tests
{
    public class DtoSerializationTests
    {
        [Fact]
        public void MaterialStreamDto_RoundTripSerialization_PreservesValues()
        {
            var dto = new MaterialStreamDto
            {
                Id = "S1",
                Name = "Feed",
                TemperatureK = 298.15,
                PressurePa = 101325,
                TotalMolarFlowMolPerS = 5.0,
                Phases = new List<PhaseDto>
                {
                    new PhaseDto
                    {
                        PhaseLabel = "Overall",
                        PhaseFraction = 1.0,
                        Composition = new List<CompoundFractionDto>
                        {
                            new CompoundFractionDto { Compound = "Methane", MoleFraction = 0.6 },
                            new CompoundFractionDto { Compound = "Ethane", MoleFraction = 0.4 }
                        },
                        Properties = new Dictionary<string, double>
                        {
                            { "density", 1.2 }
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(dto);
            var roundTrip = JsonConvert.DeserializeObject<MaterialStreamDto>(json);

            Assert.NotNull(roundTrip);
            Assert.Equal(dto.Id, roundTrip.Id);
            Assert.Equal(dto.Name, roundTrip.Name);
            Assert.Equal(dto.TemperatureK, roundTrip.TemperatureK);
            Assert.Equal(dto.PressurePa, roundTrip.PressurePa);
            Assert.Equal(dto.TotalMolarFlowMolPerS, roundTrip.TotalMolarFlowMolPerS);
            Assert.Single(roundTrip.Phases);
            Assert.Equal("Overall", roundTrip.Phases[0].PhaseLabel);
            Assert.Equal(2, roundTrip.Phases[0].Composition.Count);
        }

        [Fact]
        public void ValidationHelper_InvalidComposition_Throws()
        {
            var dto = new MaterialStreamDto
            {
                Name = "Feed",
                TemperatureK = 298.15,
                PressurePa = 101325,
                TotalMolarFlowMolPerS = 5.0,
                Phases = new List<PhaseDto>
                {
                    new PhaseDto
                    {
                        PhaseLabel = "Overall",
                        PhaseFraction = 1.0,
                        Composition = new List<CompoundFractionDto>
                        {
                            new CompoundFractionDto { Compound = "Methane", MoleFraction = 0.7 },
                            new CompoundFractionDto { Compound = "Ethane", MoleFraction = 0.7 }
                        }
                    }
                }
            };

            var ex = Assert.Throws<InvalidOperationException>(() => ValidationHelper.ValidateMaterialStreamDto(dto));
            Assert.Contains("sum to 1.0", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
