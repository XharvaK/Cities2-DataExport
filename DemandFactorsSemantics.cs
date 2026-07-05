using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CS2DataExport;

public sealed class DemandFactorsSemanticsSummary : MetricGroup
{
    [JsonPropertyName("residential_demand")]
    public double? ResidentialDemand { get; init; }

    [JsonPropertyName("commercial_demand")]
    public double? CommercialDemand { get; init; }

    [JsonPropertyName("industrial_demand")]
    public double? IndustrialDemand { get; init; }

    [JsonPropertyName("residential_factors")]
    public SortedDictionary<string, int>? ResidentialFactors { get; init; }

    [JsonPropertyName("commercial_factors")]
    public SortedDictionary<string, int>? CommercialFactors { get; init; }

    [JsonPropertyName("industrial_factors")]
    public SortedDictionary<string, int>? IndustrialFactors { get; init; }

    [JsonPropertyName("source_component")]
    public string SourceComponent { get; init; } =
        "ecs.demand_factors:Game.Simulation.ResidentialDemandSystem|Game.Simulation.CommercialDemandSystem|Game.Simulation.IndustrialDemandSystem";

    [JsonPropertyName("metric_metadata")]
    public SortedDictionary<string, MetricDefinition> MetricMetadata { get; init; } =
        MetricMetadataDefaults.DemandFactorsSemantics();
}

public static class DemandFactorsSemanticsCalculator
{
    public static double? NormalizeDemand(int demand)
    {
        return System.Math.Clamp((demand + 100.0) / 200.0, 0.0, 1.0);
    }
}
