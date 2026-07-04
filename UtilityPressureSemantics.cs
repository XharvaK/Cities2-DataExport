using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CS2DataExport;

public sealed class UtilityServiceFlowSummary
{
    [JsonPropertyName("capacity")]
    public int? Capacity { get; init; }

    [JsonPropertyName("consumption")]
    public int? Consumption { get; init; }

    [JsonPropertyName("fulfilled_consumption")]
    public int? FulfilledConsumption { get; init; }

    [JsonPropertyName("import_per_month")]
    public int? ImportPerMonth { get; init; }

    [JsonPropertyName("export_per_month")]
    public int? ExportPerMonth { get; init; }

    [JsonPropertyName("unfulfilled_consumption")]
    public int? UnfulfilledConsumption { get; init; }

    [JsonPropertyName("fulfillment_percent")]
    public double? FulfillmentPercent { get; init; }
}

public sealed class UtilityPressureSemanticsSummary : MetricGroup
{
    [JsonPropertyName("water")]
    public UtilityServiceFlowSummary Water { get; init; } = new();

    [JsonPropertyName("sewage")]
    public UtilityServiceFlowSummary Sewage { get; init; } = new();

    [JsonPropertyName("electricity")]
    public UtilityServiceFlowSummary Electricity { get; init; } = new();

    [JsonPropertyName("city_service_fill_percent")]
    public double? CityServiceFillPercent { get; init; }

    [JsonPropertyName("water_pressure")]
    public string WaterPressure { get; init; } = "unknown";

    [JsonPropertyName("sewage_pressure")]
    public string SewagePressure { get; init; } = "unknown";

    [JsonPropertyName("source_component")]
    public string SourceComponent { get; init; } = "ecs.utility_pressure:Game.Simulation.WaterStatisticsSystem|Game.Simulation.WaterTradeSystem";

    [JsonPropertyName("metric_metadata")]
    public SortedDictionary<string, MetricDefinition> MetricMetadata { get; init; } =
        MetricMetadataDefaults.UtilityPressureSemantics();
}

public static class UtilityPressureSemanticsCalculator
{
    public static double? FulfillmentPercent(int? fulfilled, int? consumption)
    {
        if (!fulfilled.HasValue || !consumption.HasValue || consumption.Value <= 0)
        {
            return null;
        }

        return fulfilled.Value * 100.0 / consumption.Value;
    }

    public static int? Unfulfilled(int? consumption, int? fulfilled)
    {
        if (!consumption.HasValue || !fulfilled.HasValue)
        {
            return null;
        }

        return System.Math.Max(0, consumption.Value - fulfilled.Value);
    }

    public static string ClassifyWaterPressure(
        UtilityServiceFlowSummary water,
        int? freshImport,
        double? cityServiceFillPercent)
    {
        if (water.Consumption is > 0 && water.FulfilledConsumption is int fulfilled && fulfilled < water.Consumption)
        {
            int gap = water.Consumption.Value - fulfilled;
            if (gap > System.Math.Max(50, water.Consumption.Value / 20))
            {
                return freshImport is > 0 ? "import_dependent_shortage" : "shortage";
            }

            return "pressure";
        }

        if (water.Capacity is > 0 && water.Consumption is int consumption && consumption > water.Capacity)
        {
            return "capacity_shortage";
        }

        if (freshImport is > 0 && cityServiceFillPercent is < 85)
        {
            return "import_dependent";
        }

        if (water.Consumption is > 0)
        {
            return "ok";
        }

        return "unknown";
    }

    public static string ClassifySewagePressure(UtilityServiceFlowSummary sewage)
    {
        if (sewage.Consumption is > 0 && sewage.FulfilledConsumption is int fulfilled && fulfilled < sewage.Consumption)
        {
            return "shortage";
        }

        if (sewage.Capacity is > 0 && sewage.Consumption is int consumption && consumption > sewage.Capacity)
        {
            return "capacity_shortage";
        }

        if (sewage.Consumption is > 0)
        {
            return "ok";
        }

        return "unknown";
    }

    public static SortedDictionary<string, double?>? BuildServiceTrade(
        UtilityServiceFlowSummary water,
        UtilityServiceFlowSummary sewage,
        UtilityServiceFlowSummary electricity)
    {
        var trade = new SortedDictionary<string, double?>(System.StringComparer.Ordinal);
        if (water.ImportPerMonth.HasValue || water.ExportPerMonth.HasValue)
        {
            trade["water"] = water.ImportPerMonth ?? water.ExportPerMonth;
        }

        if (sewage.ImportPerMonth.HasValue || sewage.ExportPerMonth.HasValue)
        {
            trade["sewage"] = sewage.ImportPerMonth ?? sewage.ExportPerMonth;
        }

        if (electricity.ImportPerMonth.HasValue || electricity.ExportPerMonth.HasValue)
        {
            trade["electricity"] = electricity.ImportPerMonth ?? electricity.ExportPerMonth;
        }

        return trade.Count > 0 ? trade : null;
    }
}
