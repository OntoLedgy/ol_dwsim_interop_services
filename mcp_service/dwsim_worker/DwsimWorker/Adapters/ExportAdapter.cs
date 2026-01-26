using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DwsimWorker.Engine;
using DwsimWorker.Models;
using Newtonsoft.Json;
using Serilog;

namespace DwsimWorker.Adapters
{
    /// <summary>
    /// Adapter for exporting flowsheet data to CSV, JSON, and Markdown reports.
    /// </summary>
    public sealed class ExportAdapter
    {
        private const string CsvExtension = ".csv";
        private const string MarkdownExtension = ".md";
        private const string DwsimCompressedExtension = ".dwxmz";
        private const string DwsimUncompressedExtension = ".dwxml";
        private const string SummaryTemplate = "summary";
        private const string DetailedTemplate = "detailed";
        private const string SummaryFormat = "summary";
        private const string FullFormat = "full";
        private readonly ILogger _logger;
        private readonly FlowsheetContext _context;
        private readonly string _exportRootDirectory;

        public ExportAdapter(ILogger logger, FlowsheetContext context)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _exportRootDirectory = Path.Combine(AppContext.BaseDirectory, "exports");
        }

        public OperationResult<ExportFileResult> ExportToCsv(string filePath, List<string> objectIds)
        {
            return ExportToFile(filePath, CsvExtension, objectIds, BuildCsv, "CSV");
        }

        public OperationResult<string> ExportToJson(string format)
        {
            if (!TryNormalizeFormat(format, out var normalized, out var error))
                return OperationResult<string>.FailureResult(error);

            var records = BuildExportRecords(ResolveObjectIds(null));
            var payload = normalized == SummaryFormat
                ? BuildSummaryPayload(records)
                : BuildFullPayload(records);

            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            _logger.Information("JSON export completed: format={Format} objects={Count}", normalized, records.Count);
            return OperationResult<string>.SuccessResult(json, "JSON export completed.");
        }

        public OperationResult<ExportFileResult> GenerateReport(string template, string filePath)
        {
            if (!TryNormalizeTemplate(template, out var normalized, out var error))
                return OperationResult<ExportFileResult>.FailureResult(error);

            var records = BuildExportRecords(ResolveObjectIds(null));
            var content = BuildReport(normalized, records);
            return WriteExportFile(filePath, MarkdownExtension, content, records.Count, "report");
        }

        public OperationResult<ExportFileResult> SaveCase(string filePath)
        {
            if (!TryResolveCasePath(filePath, out var resolvedPath, out var extension, out var error))
                return OperationResult<ExportFileResult>.FailureResult(error);

            var flowsheet = _context.GetFlowsheet();
            if (flowsheet == null)
                return OperationResult<ExportFileResult>.FailureResult("Flowsheet is not initialized.");

            EnsureParentDirectory(resolvedPath);
            _logger.Information("Saving flowsheet case: {Path} format={Format}", resolvedPath, extension);

            var saveMethod = extension == DwsimCompressedExtension ? "SaveToMXML" : "SaveToXML";
            if (!TryInvokeSaveMethod(flowsheet, saveMethod, resolvedPath, out var invokeError, out var invokeException))
                return OperationResult<ExportFileResult>.FailureResult(invokeError, invokeException);

            _logger.Information("Flowsheet case saved: {Path}", resolvedPath);
            return OperationResult<ExportFileResult>.SuccessResult(
                new ExportFileResult(resolvedPath, 0),
                "Case saved successfully.");
        }

        private OperationResult<ExportFileResult> ExportToFile(
            string filePath,
            string extension,
            List<string> objectIds,
            Func<IReadOnlyList<ExportRecord>, string> contentBuilder,
            string label)
        {
            var records = BuildExportRecords(ResolveObjectIds(objectIds));
            var content = contentBuilder(records);
            var result = WriteExportFile(filePath, extension, content, records.Count, label);
            if (result.Success)
                _logger.Information("{Label} export completed: {Path} rows={Count}", label, result.Data.FilePath, result.Data.RowCount);
            return result;
        }

        private OperationResult<ExportFileResult> WriteExportFile(
            string filePath,
            string extension,
            string content,
            int rowCount,
            string operationName)
        {
            var validation = ValidateFilePath(filePath, extension, out var resolvedPath);
            if (!validation.valid)
                return OperationResult<ExportFileResult>.FailureResult(validation.error);

            EnsureExportDirectory();
            File.WriteAllText(resolvedPath, content ?? string.Empty);
            return OperationResult<ExportFileResult>.SuccessResult(
                new ExportFileResult(resolvedPath, rowCount),
                $"{operationName} export completed.");
        }

        private string BuildCsv(IReadOnlyList<ExportRecord> records)
        {
            var builder = new StringBuilder();
            AppendCsvHeader(builder);
            foreach (var record in records)
                AppendCsvRow(builder, record);
            return builder.ToString();
        }

        private void AppendCsvHeader(StringBuilder builder)
        {
            builder.AppendLine("ObjectId,ObjectType,Temperature_K,Pressure_Pa,MolarFlow_mol_s,MassFlow_kg_s,VaporFraction,LiquidFraction");
        }

        private void AppendCsvRow(StringBuilder builder, ExportRecord record)
        {
            var values = new[]
            {
                record.ObjectId,
                record.ObjectType.ToString(),
                GetStreamValue(record, r => r.TemperatureK, p => p.Temperature.Measurement.Value),
                GetStreamValue(record, r => r.PressurePa, p => p.Pressure.Measurement.Value),
                GetStreamValue(record, r => r.MolarFlowMolPerSec, p => p.MolarFlow.Measurement.Value),
                GetStreamValue(record, r => r.MassFlowKgPerSec, null),
                GetStreamValue(record, r => r.VaporFraction, null),
                GetStreamValue(record, r => r.LiquidFraction, null)
            };

            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        private string GetStreamValue(
            ExportRecord record,
            Func<StreamResult, double> resultSelector,
            Func<StreamProperties, double> propertiesSelector)
        {
            if (record.StreamResult != null && resultSelector != null)
                return FormatNumber(resultSelector(record.StreamResult));

            if (record.StreamProperties != null && propertiesSelector != null)
                return FormatNumber(propertiesSelector(record.StreamProperties));

            return string.Empty;
        }

        private string BuildReport(string template, IReadOnlyList<ExportRecord> records)
        {
            var builder = new StringBuilder();
            AppendReportHeader(builder);
            AppendOverviewSection(builder, records);
            if (template == DetailedTemplate)
                AppendDetailedSections(builder, records);
            else
                AppendSummarySection(builder, records);
            return builder.ToString();
        }

        private void AppendReportHeader(StringBuilder builder)
        {
            builder.AppendLine("# Flowsheet Export Report");
            builder.AppendLine();
            builder.AppendLine($"Generated: {DateTime.UtcNow:O}");
            builder.AppendLine();
        }

        private void AppendOverviewSection(StringBuilder builder, IReadOnlyList<ExportRecord> records)
        {
            builder.AppendLine("## Overview");
            builder.AppendLine("| Metric | Value |");
            builder.AppendLine("| --- | --- |");
            builder.AppendLine($"| Streams | {records.Count(r => r.ObjectType == ExportObjectType.Stream)} |");
            builder.AppendLine($"| Units | {records.Count(r => r.ObjectType == ExportObjectType.Unit)} |");
            builder.AppendLine($"| Total Objects | {records.Count} |");
            builder.AppendLine();
        }

        private void AppendSummarySection(StringBuilder builder, IReadOnlyList<ExportRecord> records)
        {
            builder.AppendLine("## Key Metrics");
            builder.AppendLine("| Metric | Value |");
            builder.AppendLine("| --- | --- |");
            builder.AppendLine($"| Average Temperature (K) | {FormatNumber(CalculateAverage(records, r => r.TemperatureK))} |");
            builder.AppendLine($"| Average Pressure (Pa) | {FormatNumber(CalculateAverage(records, r => r.PressurePa))} |");
            builder.AppendLine($"| Total Molar Flow (mol/s) | {FormatNumber(CalculateSum(records, r => r.MolarFlowMolPerSec))} |");
            builder.AppendLine();
        }

        private void AppendDetailedSections(StringBuilder builder, IReadOnlyList<ExportRecord> records)
        {
            AppendStreamDetails(builder, records.Where(r => r.ObjectType == ExportObjectType.Stream).ToList());
            AppendUnitDetails(builder, records.Where(r => r.ObjectType == ExportObjectType.Unit).ToList());
        }

        private void AppendStreamDetails(StringBuilder builder, IReadOnlyList<ExportRecord> streams)
        {
            builder.AppendLine("## Streams");
            foreach (var stream in streams)
            {
                builder.AppendLine();
                builder.AppendLine($"### {stream.ObjectId} - {stream.ObjectName}");
                builder.AppendLine("| Property | Value |");
                builder.AppendLine("| --- | --- |");
                builder.AppendLine($"| Temperature (K) | {GetStreamValue(stream, r => r.TemperatureK, p => p.Temperature.Measurement.Value)} |");
                builder.AppendLine($"| Pressure (Pa) | {GetStreamValue(stream, r => r.PressurePa, p => p.Pressure.Measurement.Value)} |");
                builder.AppendLine($"| Molar Flow (mol/s) | {GetStreamValue(stream, r => r.MolarFlowMolPerSec, p => p.MolarFlow.Measurement.Value)} |");
                builder.AppendLine($"| Mass Flow (kg/s) | {GetStreamValue(stream, r => r.MassFlowKgPerSec, null)} |");
                builder.AppendLine($"| Vapor Fraction | {GetStreamValue(stream, r => r.VaporFraction, null)} |");
                builder.AppendLine($"| Liquid Fraction | {GetStreamValue(stream, r => r.LiquidFraction, null)} |");
            }
            builder.AppendLine();
        }

        private void AppendUnitDetails(StringBuilder builder, IReadOnlyList<ExportRecord> units)
        {
            builder.AppendLine("## Units");
            foreach (var unit in units)
            {
                builder.AppendLine();
                builder.AppendLine($"### {unit.ObjectId} - {unit.ObjectName}");
                builder.AppendLine($"Type: {unit.ObjectDetails}");
            }
            builder.AppendLine();
        }

        private object BuildSummaryPayload(IReadOnlyList<ExportRecord> records)
        {
            return new
            {
                generated_utc = DateTime.UtcNow,
                objects = records.Select(r => new
                {
                    id = r.ObjectId,
                    name = r.ObjectName,
                    type = r.ObjectType,
                    temperature_k = r.StreamResult?.TemperatureK ?? r.StreamProperties?.Temperature.Measurement.Value,
                    pressure_pa = r.StreamResult?.PressurePa ?? r.StreamProperties?.Pressure.Measurement.Value,
                    molar_flow_mol_s = r.StreamResult?.MolarFlowMolPerSec ?? r.StreamProperties?.MolarFlow.Measurement.Value,
                    mass_flow_kg_s = r.StreamResult?.MassFlowKgPerSec,
                    vapor_fraction = r.StreamResult?.VaporFraction,
                    liquid_fraction = r.StreamResult?.LiquidFraction
                }).ToList()
            };
        }

        private object BuildFullPayload(IReadOnlyList<ExportRecord> records)
        {
            return new
            {
                generated_utc = DateTime.UtcNow,
                streams = records.Where(r => r.ObjectType == ExportObjectType.Stream).Select(BuildFullStreamPayload).ToList(),
                units = records.Where(r => r.ObjectType == ExportObjectType.Unit).Select(BuildUnitPayload).ToList()
            };
        }

        private object BuildFullStreamPayload(ExportRecord record)
        {
            return new
            {
                id = record.ObjectId,
                name = record.ObjectName,
                type = record.ObjectDetails,
                result = record.StreamResult,
                properties = record.StreamProperties,
                phases = record.StreamResult?.Phases
            };
        }

        private object BuildUnitPayload(ExportRecord record)
        {
            return new
            {
                id = record.ObjectId,
                name = record.ObjectName,
                type = record.ObjectDetails
            };
        }

        private List<ExportRecord> BuildExportRecords(IReadOnlyList<string> objectIds)
        {
            var records = new List<ExportRecord>();
            foreach (var objectId in objectIds)
            {
                var record = TryBuildExportRecord(objectId);
                if (record != null)
                    records.Add(record);
            }
            return records;
        }

        private ExportRecord TryBuildExportRecord(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return null;

            var stream = _context.GetStream(objectId);
            if (stream != null)
                return BuildStreamRecord(objectId, stream);

            var unit = _context.GetUnit(objectId);
            if (unit != null)
                return BuildUnitRecord(objectId, unit);

            _logger.Warning("Export skipped: object not found {ObjectId}", objectId);
            return null;
        }

        private ExportRecord BuildStreamRecord(string streamId, object stream)
        {
            _context.TryGetStreamProperties(streamId, out var properties);
            var result = TryGetStreamResult(streamId);
            return new ExportRecord(
                streamId,
                ExportObjectType.Stream,
                ResolveObjectName(stream),
                stream.GetType().Name,
                result,
                properties);
        }

        private ExportRecord BuildUnitRecord(string unitId, object unit)
        {
            return new ExportRecord(
                unitId,
                ExportObjectType.Unit,
                ResolveObjectName(unit),
                unit.GetType().Name,
                null,
                null);
        }

        private StreamResult TryGetStreamResult(string streamId)
        {
            var cached = _context.GetCachedCalculationResult();
            return cached?.GetStream(streamId);
        }

        private IReadOnlyList<string> ResolveObjectIds(List<string> objectIds)
        {
            if (objectIds != null && objectIds.Count > 0)
                return objectIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();

            var ids = new List<string>();
            ids.AddRange(_context.GetStreamIds());
            ids.AddRange(_context.GetUnitIds());
            return ids.Distinct().ToList();
        }

        private string ResolveObjectName(object target)
        {
            var name = TryGetPropertyString(target, "Name");
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            var graphic = target?.GetType().GetProperty("GraphicObject")?.GetValue(target);
            var tag = TryGetPropertyString(graphic, "Tag");
            return string.IsNullOrWhiteSpace(tag) ? "Unknown" : tag;
        }

        private string TryGetPropertyString(object target, string propertyName)
        {
            if (target == null)
                return null;

            var property = target.GetType().GetProperty(propertyName);
            var value = property?.GetValue(target) as string;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private (bool valid, string error) ValidateFilePath(
            string filePath,
            string allowedExtension,
            out string resolvedPath)
        {
            resolvedPath = null;
            if (string.IsNullOrWhiteSpace(filePath))
                return (false, "File path cannot be null or empty.");
            if (filePath.Contains("..", StringComparison.Ordinal))
                return (false, "File path cannot contain directory traversal segments.");
            if (!filePath.EndsWith(allowedExtension, StringComparison.OrdinalIgnoreCase))
                return (false, $"File path must end with {allowedExtension}.");

            var candidate = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(_exportRootDirectory, filePath);
            resolvedPath = Path.GetFullPath(candidate);
            var root = Path.GetFullPath(_exportRootDirectory);
            return IsPathWithinRoot(resolvedPath, root)
                ? (true, null)
                : (false, "File path must be within the export directory.");
        }

        private bool TryResolveCasePath(string filePath, out string resolvedPath, out string extension, out string error)
        {
            resolvedPath = null;
            extension = null;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "File path cannot be null or empty.";
                return false;
            }

            extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (extension != DwsimCompressedExtension && extension != DwsimUncompressedExtension)
            {
                error = $"File path must end with {DwsimCompressedExtension} or {DwsimUncompressedExtension}.";
                return false;
            }

            resolvedPath = Path.GetFullPath(filePath);
            error = null;
            return true;
        }

        private void EnsureParentDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        private bool TryInvokeSaveMethod(
            object flowsheet,
            string methodName,
            string filePath,
            out string error,
            out Exception exception)
        {
            error = null;
            exception = null;
            var methods = flowsheet.GetType()
                .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .Where(method => method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var method = methods.FirstOrDefault(candidate =>
            {
                var parameters = candidate.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(string);
            });

            if (method == null)
            {
                error = $"Flowsheet does not expose a supported save method ({methodName}).";
                return false;
            }

            try
            {
                method.Invoke(flowsheet, new object[] { filePath });
                return true;
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                exception = ex.InnerException ?? ex;
                error = $"Failed to save flowsheet using {methodName}: {exception.Message}";
                return false;
            }
            catch (Exception ex)
            {
                exception = ex;
                error = $"Failed to save flowsheet using {methodName}: {ex.Message}";
                return false;
            }
        }

        private bool IsPathWithinRoot(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
                return false;

            var normalizedRoot = EnsureTrailingSeparator(root);
            return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureExportDirectory()
        {
            if (!Directory.Exists(_exportRootDirectory))
                Directory.CreateDirectory(_exportRootDirectory);
        }

        private string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            var separator = Path.DirectorySeparatorChar.ToString();
            return path.EndsWith(separator, StringComparison.Ordinal) ? path : path + separator;
        }

        private string FormatNumber(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("G", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (!value.Contains(",") && !value.Contains("\"") && !value.Contains("\n"))
                return value;

            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        private double? CalculateAverage(IReadOnlyList<ExportRecord> records, Func<StreamResult, double> selector)
        {
            var values = records.Select(r => r.StreamResult).Where(r => r != null).Select(selector).ToList();
            return values.Count == 0 ? (double?)null : values.Average();
        }

        private double? CalculateSum(IReadOnlyList<ExportRecord> records, Func<StreamResult, double> selector)
        {
            var values = records.Select(r => r.StreamResult).Where(r => r != null).Select(selector).ToList();
            return values.Count == 0 ? (double?)null : values.Sum();
        }

        private bool TryNormalizeFormat(string format, out string normalized, out string error)
        {
            normalized = (format ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized == SummaryFormat || normalized == FullFormat)
            {
                error = null;
                return true;
            }

            error = "Format must be 'summary' or 'full'.";
            return false;
        }

        private bool TryNormalizeTemplate(string template, out string normalized, out string error)
        {
            normalized = (template ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized == SummaryTemplate || normalized == DetailedTemplate)
            {
                error = null;
                return true;
            }

            error = "Template must be 'summary' or 'detailed'.";
            return false;
        }
    }

    public sealed class ExportFileResult
    {
        public string FilePath { get; }
        public int RowCount { get; }

        public ExportFileResult(string filePath, int rowCount)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            RowCount = rowCount;
        }
    }

    internal enum ExportObjectType
    {
        Stream,
        Unit
    }

    internal sealed class ExportRecord
    {
        public string ObjectId { get; }
        public ExportObjectType ObjectType { get; }
        public string ObjectName { get; }
        public string ObjectDetails { get; }
        public StreamResult StreamResult { get; }
        public StreamProperties StreamProperties { get; }

        public ExportRecord(
            string objectId,
            ExportObjectType objectType,
            string objectName,
            string objectDetails,
            StreamResult streamResult,
            StreamProperties streamProperties)
        {
            ObjectId = objectId;
            ObjectType = objectType;
            ObjectName = objectName;
            ObjectDetails = objectDetails;
            StreamResult = streamResult;
            StreamProperties = streamProperties;
        }
    }
}
