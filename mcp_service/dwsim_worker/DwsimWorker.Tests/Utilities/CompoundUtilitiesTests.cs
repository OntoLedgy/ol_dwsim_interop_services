using System;
using System.Linq;
using Moq;
using Serilog;
using Xunit;
using DwsimWorker.Adapters;
using DwsimWorker.Engine;
using DwsimWorker.Utilities;

namespace DwsimWorker.Tests.Utilities
{
    public class CompoundUtilitiesTests
    {
        public class CompoundAliasMapperTests
        {
            [Theory]
            [InlineData("isobutane", "Isobutane")]
            [InlineData("ISO-PENTANE", "Isopentane")]
            [InlineData("CO2", "Carbon Dioxide")]
            [InlineData("n-c6", "n-Hexane")]
            public void TryResolveAlias_KnownAliases_ReturnsCanonicalName(string alias, string expectedCanonical)
            {
                var resolved = CompoundAliasMapper.TryResolveAlias(alias, out var canonical);

                Assert.True(resolved);
                Assert.Equal(expectedCanonical, canonical);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            public void TryResolveAlias_NullOrWhiteSpace_ReturnsFalse(string input)
            {
                var resolved = CompoundAliasMapper.TryResolveAlias(input, out var canonical);

                Assert.False(resolved);
                Assert.Equal(string.Empty, canonical);
            }

            [Fact]
            public void GetAliasesFor_KnownCompound_ReturnsSortedAliases()
            {
                var aliases = CompoundAliasMapper.GetAliasesFor("Water");

                Assert.NotEmpty(aliases);
                Assert.Contains(aliases, alias => alias.Equals("h2o", StringComparison.OrdinalIgnoreCase));

                var sorted = aliases.OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase).ToList();
                Assert.Equal(sorted, aliases);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            public void GetAliasesFor_NullOrWhiteSpace_ReturnsEmpty(string input)
            {
                var aliases = CompoundAliasMapper.GetAliasesFor(input);

                Assert.Empty(aliases);
            }
        }

        public class FuzzyMatcherTests
        {
            private const int MaxResults = 2;
            private const int MaxDistance = 2;

            [Fact]
            public void FindSimilar_WithTypo_ReturnsExpectedSuggestion()
            {
                var suggestions = FuzzyMatcher.FindSimilar("methne", SampleCandidates(), MaxResults, MaxDistance);

                Assert.Contains("Methane", suggestions);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            public void FindSimilar_NullOrWhiteSpaceInput_ReturnsEmpty(string input)
            {
                var suggestions = FuzzyMatcher.FindSimilar(input, SampleCandidates(), MaxResults, MaxDistance);

                Assert.Empty(suggestions);
            }

            [Fact]
            public void FindSimilar_NullCandidates_ReturnsEmpty()
            {
                var suggestions = FuzzyMatcher.FindSimilar("methane", null, MaxResults, MaxDistance);

                Assert.Empty(suggestions);
            }

            [Theory]
            [InlineData(0, 1)]
            [InlineData(1, -1)]
            public void FindSimilar_InvalidLimits_ReturnsEmpty(int maxResults, int maxDistance)
            {
                var suggestions = FuzzyMatcher.FindSimilar("methane", SampleCandidates(), maxResults, maxDistance);

                Assert.Empty(suggestions);
            }

            [Fact]
            public void FindSimilar_DeduplicatesCandidates_IgnoringCase()
            {
                var candidates = new[] { "Methane", "methane", "Ethane" };

                var suggestions = FuzzyMatcher.FindSimilar("methne", candidates, MaxResults, MaxDistance);

                Assert.Single(suggestions.Where(name => name.Equals("Methane", StringComparison.OrdinalIgnoreCase)));
            }

            private static string[] SampleCandidates()
            {
                return new[] { "Methane", "Ethane", "Propane" };
            }
        }

        public class CompoundAdapterValidationTests
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("   ")]
            public void ValidateCompound_NullOrWhiteSpace_ReturnsInvalid(string input)
            {
                var adapter = CreateAdapter(out var context);
                using (context)
                {
                    var result = adapter.ValidateCompound(input);

                    Assert.False(result.Valid);
                    Assert.Equal(string.Empty, result.InputName);
                    Assert.Empty(result.Suggestions);
                    Assert.Equal(string.Empty, result.CanonicalName);
                    Assert.False(result.AliasUsed);
                }
            }

            [Fact]
            public void ValidateCompound_Alias_ReturnsCanonicalAndAliasUsed()
            {
                var adapter = CreateAdapter(out var context);
                using (context)
                {
                    var result = adapter.ValidateCompound("co2");

                    Assert.True(result.Valid);
                    Assert.True(result.AliasUsed);
                    Assert.Equal("Carbon Dioxide", result.CanonicalName);
                }
            }

            [Fact]
            public void ValidateCompound_CaseInsensitiveCanonical_ReturnsTrimmedInput()
            {
                var adapter = CreateAdapter(out var context);
                using (context)
                {
                    var result = adapter.ValidateCompound("  methane  ");

                    Assert.True(result.Valid);
                    Assert.False(result.AliasUsed);
                    Assert.Equal("methane", result.InputName);
                    Assert.Equal("Methane", result.CanonicalName);
                }
            }

            [Fact]
            public void ValidateCompound_InvalidName_ReturnsSuggestions()
            {
                var adapter = CreateAdapter(out var context);
                using (context)
                {
                    var result = adapter.ValidateCompound("methne");

                    Assert.False(result.Valid);
                    Assert.Contains("Methane", result.Suggestions);
                    Assert.False(result.AliasUsed);
                }
            }
        }

        public class CompoundAdapterListingTests
        {
            private const int DefaultListLimit = 50;
            private const int MaxListLimit = 200;
            private const int DefaultOffset = 0;
            private const int DefaultLimitInput = 0;
            private const int NegativeOffset = -1;
            private const int OverMaxLimit = MaxListLimit + 10;
            private const int PageLimit = 2;
            private const int PageOffset = 1;
            private const int ZeroCount = 0;

            [Fact]
            public void ListCompounds_InvalidPaging_UsesDefaults()
            {
                var adapter = CreateAdapter(out var context);
                using (context)
                {
                    var result = adapter.ListCompounds(null, null, DefaultLimitInput, NegativeOffset);

                    Assert.Equal(DefaultListLimit, result.Limit);
                    Assert.Equal(DefaultOffset, result.Offset);
                }
            }

            [Fact]
            public void ListCompounds_LimitAboveMax_IsClamped()
            {
                var adapter = CreateAdapter(out var context);
                using (context)
                {
                    var result = adapter.ListCompounds(null, null, OverMaxLimit, DefaultOffset);

                    Assert.Equal(MaxListLimit, result.Limit);
                }
            }

            [Fact]
            public void ListCompounds_CategoryFilter_ReturnsCategoryAndAliases()
            {
                var adapter = CreateAdapter(out var context);
                using (context)
                {
                    var result = adapter.ListCompounds(null, "Inorganics", MaxListLimit, DefaultOffset);

                    Assert.NotEmpty(result.Compounds);
                    Assert.All(result.Compounds, info => Assert.Equal("Inorganics", info.Category));

                    var water = result.Compounds.FirstOrDefault(info => info.Name == "Water");
                    Assert.NotNull(water);
                    Assert.Contains(water.Aliases, alias => alias.Equals("h2o", StringComparison.OrdinalIgnoreCase));
                }
            }

            [Fact]
            public void ListCompounds_PatternFilter_IsCaseInsensitive()
            {
                var adapter = CreateAdapter(out var context);
                using (context)
                {
                    var result = adapter.ListCompounds("BUTANE", "Alkanes", MaxListLimit, DefaultOffset);

                    Assert.NotEmpty(result.Compounds);
                    Assert.All(result.Compounds, info => Assert.Contains("butane", info.Name, StringComparison.OrdinalIgnoreCase));
                    Assert.Contains(result.Compounds, info => info.Name == "n-Butane");
                    Assert.Contains(result.Compounds, info => info.Name == "Isobutane");
                    Assert.Equal(result.TotalCount, result.Compounds.Count);
                }
            }

            [Fact]
            public void ListCompounds_Paging_ReturnsSubset()
            {
                var adapter = CreateAdapter(out var context);
                using (context)
                {
                    var result = adapter.ListCompounds("ane", "Alkanes", PageLimit, PageOffset);

                    Assert.Equal(PageLimit, result.Compounds.Count);
                    Assert.True(result.TotalCount >= result.Compounds.Count);
                    Assert.Equal(PageLimit, result.Limit);
                    Assert.Equal(PageOffset, result.Offset);
                }
            }

            [Fact]
            public void ListCompounds_UnknownCategory_ReturnsEmpty()
            {
                var adapter = CreateAdapter(out var context);
                using (context)
                {
                    var result = adapter.ListCompounds(null, "Unknown", DefaultListLimit, DefaultOffset);

                    Assert.Empty(result.Compounds);
                    Assert.Equal(ZeroCount, result.TotalCount);
                }
            }
        }

        private static CompoundAdapter CreateAdapter(out FlowsheetContext context)
        {
            var logger = new Mock<ILogger>();
            var config = new FlowsheetContextConfigBuilder()
                .WithValidation(false)
                .Build();

            context = new FlowsheetContext(logger.Object, config);
            return new CompoundAdapter(logger.Object, context);
        }
    }
}
