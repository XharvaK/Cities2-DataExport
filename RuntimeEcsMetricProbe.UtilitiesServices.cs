using System.Collections.Generic;
using Game.Simulation;
using Unity.Entities;

namespace CS2DataExport;

public sealed partial class RuntimeEcsMetricProbe
{
    public UtilitiesServicesSemanticsSummary CollectUtilitiesServicesSemanticsSummary()
    {
        World? world = _getWorld();
        if (world == null || !world.IsCreated)
        {
            return new UtilitiesServicesSemanticsSummary
            {
                Status = MetricStatus.Unavailable,
                Notes = new[] { "runtime World is unavailable; utilities/services cannot be resolved." }
            };
        }

        var notes = new List<string>
        {
            "electricity metrics read from ElectricityStatisticsSystem monthly production/consumption snapshots.",
            "garbage_accumulation uses GarbageAccumulationSystem.garbageAccumulation (estimated daily scale).",
            "healthcare/fire/police coverage fields remain optional until ECS coverage probes are added."
        };

        ElectricityStatisticsSystem? electricitySystem = world.GetExistingSystemManaged<ElectricityStatisticsSystem>();
        GarbageAccumulationSystem? garbageSystem = world.GetExistingSystemManaged<GarbageAccumulationSystem>();

        int? production = electricitySystem?.production;
        int? consumption = electricitySystem?.consumption;
        int? fulfilled = electricitySystem?.fulfilledConsumption;
        int? batteryCapacity = electricitySystem?.batteryCapacity;
        int? capacity = production;
        if (batteryCapacity is > 0)
        {
            capacity = (production ?? 0) + batteryCapacity.Value;
        }

        long? garbageAccumulation = garbageSystem?.garbageAccumulation;
        double? fulfillmentPercent = UtilityPressureSemanticsCalculator.FulfillmentPercent(fulfilled, consumption);
        string electricityPressure = UtilitiesServicesSemanticsCalculator.ClassifyElectricityPressure(
            production,
            consumption,
            fulfilled);

        if (electricitySystem == null)
        {
            notes.Add("ElectricityStatisticsSystem is unavailable.");
        }

        if (garbageSystem == null)
        {
            notes.Add("GarbageAccumulationSystem is unavailable.");
        }

        if (electricityPressure is "shortage" or "capacity_shortage" or "pressure")
        {
            notes.Add("electricity demand is not fully met; buildings may brown out.");
        }

        int availableMetrics = CountPresent(production)
            + CountPresent(consumption)
            + CountPresent(garbageAccumulation);

        return new UtilitiesServicesSemanticsSummary
        {
            Status = ComputeStatus(availableMetrics, expectedMetrics: 2),
            ElectricityProduction = production,
            ElectricityConsumption = consumption,
            ElectricityCapacity = capacity,
            ElectricityFulfilledConsumption = fulfilled,
            ElectricityFulfillmentPercent = fulfillmentPercent,
            ElectricityPressure = electricityPressure,
            GarbageAccumulation = garbageAccumulation,
            Notes = notes.ToArray()
        };
    }
}
