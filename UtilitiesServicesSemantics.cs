using System.Text.Json.Serialization;

namespace CS2DataExport;

public sealed class UtilitiesServicesSemanticsSummary : MetricGroup
{
    [JsonPropertyName("electricity_production")]
    public int? ElectricityProduction { get; init; }

    [JsonPropertyName("electricity_consumption")]
    public int? ElectricityConsumption { get; init; }

    [JsonPropertyName("electricity_capacity")]
    public int? ElectricityCapacity { get; init; }

    [JsonPropertyName("electricity_fulfilled_consumption")]
    public int? ElectricityFulfilledConsumption { get; init; }

    [JsonPropertyName("electricity_fulfillment_percent")]
    public double? ElectricityFulfillmentPercent { get; init; }

    [JsonPropertyName("electricity_pressure")]
    public string ElectricityPressure { get; init; } = "unknown";

    [JsonPropertyName("garbage_accumulation")]
    public long? GarbageAccumulation { get; init; }

    [JsonPropertyName("garbage_processing")]
    public long? GarbageProcessing { get; init; }

    [JsonPropertyName("healthcare_beds_total")]
    public int? HealthcareBedsTotal { get; init; }

    [JsonPropertyName("healthcare_beds_used")]
    public int? HealthcareBedsUsed { get; init; }

    [JsonPropertyName("fire_coverage_percent")]
    public double? FireCoveragePercent { get; init; }

    [JsonPropertyName("police_coverage_percent")]
    public double? PoliceCoveragePercent { get; init; }

    [JsonPropertyName("source_component")]
    public string SourceComponent { get; init; } =
        "ecs.utilities_services:Game.Simulation.ElectricityStatisticsSystem|Game.Simulation.GarbageAccumulationSystem";

    [JsonPropertyName("metric_metadata")]
    public System.Collections.Generic.SortedDictionary<string, MetricDefinition> MetricMetadata { get; init; } =
        MetricMetadataDefaults.UtilitiesServicesSemantics();
}

public static class UtilitiesServicesSemanticsCalculator
{
    public static string ClassifyElectricityPressure(
        int? production,
        int? consumption,
        int? fulfilledConsumption)
    {
        if (consumption is > 0 && fulfilledConsumption is int fulfilled && fulfilled < consumption)
        {
            int gap = consumption.Value - fulfilled;
            if (gap > System.Math.Max(50, consumption.Value / 20))
            {
                return "shortage";
            }

            return "pressure";
        }

        if (production is > 0 && consumption is int demand && demand > production)
        {
            return "capacity_shortage";
        }

        if (consumption is > 0)
        {
            return "ok";
        }

        return "unknown";
    }
}
