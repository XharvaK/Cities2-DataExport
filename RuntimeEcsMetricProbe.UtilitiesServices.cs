using System.Collections.Generic;
using Game.City;
using Game.Simulation;
using Unity.Entities;

namespace CS2DataExport;

public sealed partial class RuntimeEcsMetricProbe
{
    public UtilitiesServicesSemanticsSummary CollectUtilitiesServicesSemanticsSummary()
    {
        UtilityPressureSemanticsSummary utilityPressure = CollectUtilityPressureSemanticsSummary();
        UtilityServiceFlowSummary electricity = utilityPressure.Electricity;

        World? world = _getWorld();
        CityStatisticsSystem? cityStatistics = world?.GetExistingSystemManaged<CityStatisticsSystem>();

        var notes = new List<string>
        {
            "utilities/services semantics combine electricity pressure, garbage flow, healthcare beds, and emergency coverage."
        };

        if (utilityPressure.Status != MetricStatus.Unavailable)
        {
            notes.Add("electricity metrics are derived from UtilityPressureSemantics electricity flow.");
        }

        int? production = electricity.Capacity;
        int? consumption = electricity.Consumption;
        int? fulfilled = electricity.FulfilledConsumption;
        double? fulfillmentPercent = electricity.FulfillmentPercent;
        string electricityPressure = UtilitiesServicesSemanticsCalculator.ClassifyElectricityPressure(
            production,
            consumption,
            fulfilled);

        long? garbageAccumulation = ReadGarbageAccumulation(world, cityStatistics, notes);
        long? garbageProcessing = ReadGarbageProcessing(world, cityStatistics, notes);
        int? healthcareBedsTotal = ReadHealthcareBedsTotal(world, notes);
        int? healthcareBedsUsed = ReadHealthcareBedsUsed(world, cityStatistics, notes);
        double? fireCoverage = ReadCoveragePercent(world, notes, "fire", "FireCoverageSystem");
        double? policeCoverage = ReadCoveragePercent(world, notes, "police", "PoliceCoverageSystem");

        int available = CountPresentUtilities(
            production,
            consumption,
            garbageAccumulation,
            garbageProcessing,
            healthcareBedsTotal,
            fireCoverage,
            policeCoverage);

        return new UtilitiesServicesSemanticsSummary
        {
            Status = available >= 3
                ? MetricStatus.Ok
                : available >= 1
                    ? MetricStatus.Partial
                    : MetricStatus.Unavailable,
            ElectricityProduction = production,
            ElectricityConsumption = consumption,
            ElectricityCapacity = production,
            ElectricityFulfilledConsumption = fulfilled,
            ElectricityFulfillmentPercent = fulfillmentPercent,
            ElectricityPressure = electricityPressure,
            GarbageAccumulation = garbageAccumulation,
            GarbageProcessing = garbageProcessing,
            HealthcareBedsTotal = healthcareBedsTotal,
            HealthcareBedsUsed = healthcareBedsUsed,
            FireCoveragePercent = fireCoverage,
            PoliceCoveragePercent = policeCoverage,
            Notes = notes.ToArray()
        };
    }

    private static int CountPresentUtilities(params object?[] values)
    {
        int count = 0;
        foreach (object? value in values)
        {
            if (value != null)
            {
                count++;
            }
        }

        return count;
    }

    private long? ReadGarbageAccumulation(
        World? world,
        CityStatisticsSystem? cityStatistics,
        List<string> notes)
    {
        if (TryReadStatisticByName(cityStatistics, "GarbageAccumulation", out int statisticValue))
        {
            return statisticValue;
        }

        object? system = TryGetSystemByName(world, "GarbageAccumulationSystem");
        if (system != null && ReadIntMember(system, "accumulation", "m_Accumulation", "dailyAccumulation", "m_DailyAccumulation") is int value)
        {
            return value;
        }

        notes.Add("garbage accumulation unavailable from statistics or GarbageAccumulationSystem.");
        return null;
    }

    private long? ReadGarbageProcessing(
        World? world,
        CityStatisticsSystem? cityStatistics,
        List<string> notes)
    {
        if (TryReadStatisticByName(cityStatistics, "GarbageProcessing", out int statisticValue))
        {
            return statisticValue;
        }

        object? system = TryGetSystemByName(world, "GarbageProcessingSystem");
        if (system != null && ReadIntMember(system, "processing", "m_Processing", "dailyProcessing", "m_DailyProcessing") is int value)
        {
            return value;
        }

        notes.Add("garbage processing unavailable from statistics or GarbageProcessingSystem.");
        return null;
    }

    private int? ReadHealthcareBedsTotal(World? world, List<string> notes)
    {
        object? system = TryGetSystemByName(world, "HealthcareSystem", "HospitalSystem");
        if (system != null && ReadIntMember(system, "bedsTotal", "m_BedsTotal", "totalBeds", "m_TotalBeds") is int value)
        {
            return value;
        }

        notes.Add("healthcare bed totals unavailable; hospital ECS mapping still being validated.");
        return null;
    }

    private int? ReadHealthcareBedsUsed(
        World? world,
        CityStatisticsSystem? cityStatistics,
        List<string> notes)
    {
        if (TryReadStatisticByName(cityStatistics, "HealthcareBedsUsed", out int statisticValue))
        {
            return statisticValue;
        }

        object? system = TryGetSystemByName(world, "HealthcareSystem", "HospitalSystem");
        if (system != null && ReadIntMember(system, "bedsUsed", "m_BedsUsed", "usedBeds", "m_UsedBeds") is int value)
        {
            return value;
        }

        notes.Add("healthcare bed usage unavailable from statistics or hospital systems.");
        return null;
    }

    private double? ReadCoveragePercent(
        World? world,
        List<string> notes,
        string label,
        params string[] systemNames)
    {
        foreach (string systemName in systemNames)
        {
            object? system = TryGetSystemByName(world, systemName);
            if (system == null)
            {
                continue;
            }

            if (ReadIntMember(system, "coverage", "m_Coverage", "coveragePercent", "m_CoveragePercent") is int intCoverage)
            {
                return intCoverage;
            }

            if (TryExtractNamedDouble(system, "coverage", out double coverage) ||
                TryExtractNamedDouble(system, "m_Coverage", out coverage) ||
                TryExtractNamedDouble(system, "coveragePercent", out coverage) ||
                TryExtractNamedDouble(system, "m_CoveragePercent", out coverage))
            {
                return coverage;
            }
        }

        notes.Add($"{label} coverage percent unavailable; coverage system mapping still being validated.");
        return null;
    }

    private static bool TryReadStatisticByName(
        CityStatisticsSystem? statistics,
        string statisticName,
        out int value)
    {
        value = 0;
        if (statistics == null)
        {
            return false;
        }

        if (!System.Enum.TryParse(typeof(StatisticType), statisticName, ignoreCase: true, out object? parsed) ||
            parsed is not StatisticType statisticType)
        {
            return false;
        }

        int? result = GetOfficialStatistic(statistics, statisticType);
        if (!result.HasValue)
        {
            return false;
        }

        value = result.Value;
        return true;
    }

    private static object? TryGetSystemByName(World? world, params string[] typeNames)
    {
        if (world == null || !world.IsCreated)
        {
            return null;
        }

        foreach (string typeName in typeNames)
        {
            System.Type? systemType = System.Type.GetType($"Game.Simulation.{typeName}, Game");
            if (systemType == null)
            {
                continue;
            }

            System.Reflection.MethodInfo? method = typeof(World).GetMethod(
                "GetExistingSystemManaged",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
                binder: null,
                types: new[] { systemType },
                modifiers: null);
            if (method == null)
            {
                continue;
            }

            object? system = method.Invoke(world, null);
            if (system != null)
            {
                return system;
            }
        }

        return null;
    }
}
