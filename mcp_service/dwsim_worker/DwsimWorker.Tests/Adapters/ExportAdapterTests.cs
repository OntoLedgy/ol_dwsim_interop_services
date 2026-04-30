// SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Moq;
using Newtonsoft.Json.Linq;
using Serilog;
using Xunit;
using DwsimWorker.Adapters;
using DwsimWorker.Engine;
using DwsimWorker.Models;

namespace DwsimWorker.Tests.Adapters
{
    public sealed class ExportAdapterTests : IDisposable
    {
        private readonly string _tempRoot;
        private readonly string _exportRoot;

        public ExportAdapterTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "ExportAdapterTests", Guid.NewGuid().ToString("N"));
            _exportRoot = Path.Combine(_tempRoot, "exports");
            Directory.CreateDirectory(_exportRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, true);
        }

        [Fact]
        public void ExportToCsv_WithStreamData_WritesRows()
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var result = adapter.ExportToCsv("streams.csv", new List<string> { "S1" });

                Assert.True(result.Success, result.Message);
                Assert.Equal(1, result.Data.RowCount);
                Assert.True(File.Exists(result.Data.FilePath));

                var lines = File.ReadAllLines(result.Data.FilePath);
                Assert.Equal(2, lines.Length);
                Assert.Contains("ObjectId,ObjectType,Temperature_K", lines[0]);
                Assert.Contains("S1,Stream", lines[1]);
            }
        }

        [Fact]
        public void ExportToCsv_EmptyObjectList_WritesHeadersOnly()
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var result = adapter.ExportToCsv("empty.csv", new List<string> { "   " });

                Assert.True(result.Success, result.Message);
                Assert.Equal(0, result.Data.RowCount);
                Assert.True(File.Exists(result.Data.FilePath));

                var lines = File.ReadAllLines(result.Data.FilePath);
                Assert.Single(lines);
                Assert.Equal("ObjectId,ObjectType,Temperature_K,Pressure_Pa,MolarFlow_mol_s,MassFlow_kg_s,VaporFraction,LiquidFraction", lines[0]);
            }
        }

        [Fact]
        public void ExportToCsv_InvalidFilePath_ReturnsFailure()
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var result = adapter.ExportToCsv("invalid.txt", new List<string> { "S1" });

                Assert.False(result.Success);
                Assert.Contains(".csv", result.Message);
            }
        }

        [Fact]
        public void ExportToJson_SummaryFormat_ReturnsSummaryPayload()
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var result = adapter.ExportToJson("summary");

                Assert.True(result.Success, result.Message);
                var payload = JObject.Parse(result.Data);
                Assert.NotNull(payload["generated_utc"]);

                var objects = payload["objects"] as JArray;
                Assert.NotNull(objects);
                Assert.Equal(2, objects.Count);

                var stream = objects.FirstOrDefault(obj => obj?["id"]?.Value<string>() == "S1");
                Assert.NotNull(stream);
                Assert.Equal(0, stream["type"]?.Value<int>());
                Assert.Equal("Stream A", stream["name"]?.Value<string>());
            }
        }

        [Fact]
        public void ExportToJson_FullFormat_IncludesPhaseData()
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var result = adapter.ExportToJson("full");

                Assert.True(result.Success, result.Message);
                var payload = JObject.Parse(result.Data);
                var streams = payload["streams"] as JArray;
                var units = payload["units"] as JArray;
                Assert.NotNull(streams);
                Assert.NotNull(units);
                Assert.Single(streams);
                Assert.Single(units);

                var phases = streams[0]["phases"];
                Assert.NotNull(phases);
                Assert.NotNull(phases["Vapor"]);
            }
        }

        [Theory]
        [InlineData("bad")]
        [InlineData("details")]
        public void ExportToJson_InvalidFormat_ReturnsFailure(string format)
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var result = adapter.ExportToJson(format);

                Assert.False(result.Success);
                Assert.Contains("summary", result.Message);
            }
        }

        [Fact]
        public void GenerateReport_SummaryTemplate_WritesSummarySections()
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var result = adapter.GenerateReport("summary", "summary.md");

                Assert.True(result.Success, result.Message);
                Assert.True(File.Exists(result.Data.FilePath));

                var content = File.ReadAllText(result.Data.FilePath);
                Assert.Contains("# Flowsheet Export Report", content);
                Assert.Contains("## Overview", content);
                Assert.Contains("## Key Metrics", content);
                Assert.DoesNotContain("## Streams", content);
            }
        }

        [Fact]
        public void GenerateReport_DetailedTemplate_IncludesStreamAndUnitDetails()
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var result = adapter.GenerateReport("detailed", "detailed.md");

                Assert.True(result.Success, result.Message);
                Assert.True(File.Exists(result.Data.FilePath));

                var content = File.ReadAllText(result.Data.FilePath);
                Assert.Contains("## Streams", content);
                Assert.Contains("### S1 - Stream A", content);
                Assert.Contains("## Units", content);
                Assert.Contains("Type: TestUnit", content);
            }
        }

        [Theory]
        [InlineData("brief")]
        [InlineData("template")]
        public void GenerateReport_InvalidTemplate_ReturnsFailure(string template)
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var result = adapter.GenerateReport(template, "report.md");

                Assert.False(result.Success);
                Assert.Contains("summary", result.Message);
            }
        }

        [Fact]
        public void SaveCase_Dwxmz_WhenSaveThrows_ReturnsFailure()
        {
            var flowsheet = new TestFlowsheet { ThrowOnSave = true };
            var adapter = CreateAdapterWithSampleData(flowsheet, out var context);
            using (context)
            {
                var filePath = Path.Combine(_tempRoot, "case.dwxmz");

                var result = adapter.SaveCase(filePath);

                Assert.False(result.Success);
                Assert.Contains("SaveToMXML", result.Message);
                Assert.NotNull(result.Error);
            }
        }

        [Fact]
        public void SaveCase_Dwxml_WritesFile()
        {
            var flowsheet = new TestFlowsheet();
            var adapter = CreateAdapterWithSampleData(flowsheet, out var context);
            using (context)
            {
                var filePath = Path.Combine(_tempRoot, "case.dwxml");

                var result = adapter.SaveCase(filePath);

                Assert.True(result.Success, result.Message);
                Assert.True(File.Exists(filePath));
                Assert.Contains("saved:XML", File.ReadAllText(filePath));
            }
        }

        [Fact]
        public void SaveCase_InvalidExtension_ReturnsFailure()
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var filePath = Path.Combine(_tempRoot, "case.txt");

                var result = adapter.SaveCase(filePath);

                Assert.False(result.Success);
                Assert.Contains(".dwxmz", result.Message);
            }
        }

        [Fact]
        public void SaveCase_PathTraversalAttempt_ReturnsFailure()
        {
            var flowsheet = new TestFlowsheet { AllowedRoot = _exportRoot };
            var adapter = CreateAdapterWithSampleData(flowsheet, out var context);
            using (context)
            {
                var filePath = Path.Combine(_exportRoot, "..", "outside.dwxmz");

                var result = adapter.SaveCase(filePath);

                Assert.False(result.Success);
                Assert.Contains("SaveToMXML", result.Message);
            }
        }

        [Fact]
        public void PathValidation_DirectoryTraversal_IsRejected()
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var result = adapter.ExportToCsv("../bad.csv", new List<string> { "S1" });

                Assert.False(result.Success);
                Assert.Contains("directory traversal", result.Message);
            }
        }

        [Fact]
        public void PathValidation_AbsolutePathOutsideExportRoot_IsRejected()
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var outsidePath = Path.Combine(_tempRoot, "outside.csv");

                var result = adapter.ExportToCsv(outsidePath, new List<string> { "S1" });

                Assert.False(result.Success);
                Assert.Contains("export directory", result.Message);
            }
        }

        [Fact]
        public void PathValidation_ValidRelativePath_IsAccepted()
        {
            var adapter = CreateAdapterWithSampleData(new TestFlowsheet(), out var context);
            using (context)
            {
                var result = adapter.ExportToCsv("valid.csv", new List<string> { "S1" });

                Assert.True(result.Success, result.Message);
                Assert.True(File.Exists(result.Data.FilePath));
            }
        }

        private ExportAdapter CreateAdapterWithSampleData(TestFlowsheet flowsheet, out FlowsheetContext context)
        {
            var logger = new Mock<ILogger>();
            var config = new FlowsheetContextConfigBuilder()
                .WithValidation(false)
                .Build();

            context = new FlowsheetContext(logger.Object, config);
            InitializeContextForTests(context, flowsheet);
            AddSampleObjects(context);
            CacheSampleResults(context);

            var adapter = new ExportAdapter(logger.Object, context);
            SetExportRoot(adapter, _exportRoot);
            return adapter;
        }

        private static void InitializeContextForTests(FlowsheetContext context, object flowsheet)
        {
            var type = typeof(FlowsheetContext);
            type.GetField("_flowsheet", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(context, flowsheet);
            type.GetField("_isInitialized", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(context, true);
        }

        private static void AddSampleObjects(FlowsheetContext context)
        {
            context.AddStream(new TestStream { Name = "Stream A" }, "S1");
            context.AddUnit(new TestUnit { Name = "Unit A" }, "U1");
        }

        private static void CacheSampleResults(FlowsheetContext context)
        {
            var composition = new Composition(new[] { 1.0 });
            var phases = new Dictionary<string, PhaseProperties>
            {
                { "Vapor", new PhaseProperties("Vapor", 1.0, 0.1, 0.5, composition) }
            };
            var streamResult = new StreamResult(
                "S1",
                "Stream A",
                300.0,
                101325.0,
                1.0,
                0.1,
                composition,
                0.5,
                0.5,
                phases);
            var timing = CalculationTiming.FromTimestamps(DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow);
            var convergence = ConvergenceStatus.Converged(5, 1e-5);
            var massBalance = MassBalanceResult.Valid(1.0, 1.0);

            var result = CalculationResult.SuccessResult(
                convergence,
                timing,
                new List<StreamResult> { streamResult },
                massBalance);

            context.CacheCalculationResult(result);
        }

        private static void SetExportRoot(ExportAdapter adapter, string exportRoot)
        {
            var field = typeof(ExportAdapter).GetField("_exportRootDirectory", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException("ExportAdapter export root field not found.");

            field.SetValue(adapter, exportRoot);
        }

        private sealed class TestStream
        {
            public string Name { get; set; }
        }

        private sealed class TestUnit
        {
            public string Name { get; set; }
        }

        private sealed class TestFlowsheet
        {
            public bool ThrowOnSave { get; set; }
            public string AllowedRoot { get; set; }

            public void SaveToMXML(string path)
            {
                HandleSave(path, "MXML");
            }

            public void SaveToXML(string path)
            {
                HandleSave(path, "XML");
            }

            private void HandleSave(string path, string marker)
            {
                if (!string.IsNullOrWhiteSpace(AllowedRoot))
                {
                    var root = EnsureTrailingSeparator(Path.GetFullPath(AllowedRoot));
                    var resolved = Path.GetFullPath(path);
                    if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Save path must be within allowed root.");
                }

                if (ThrowOnSave)
                    throw new InvalidOperationException("Simulated save failure.");

                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(path, $"saved:{marker}");
            }

            private static string EnsureTrailingSeparator(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return path;

                var separator = Path.DirectorySeparatorChar.ToString();
                return path.EndsWith(separator, StringComparison.Ordinal) ? path : path + separator;
            }
        }
    }
}
