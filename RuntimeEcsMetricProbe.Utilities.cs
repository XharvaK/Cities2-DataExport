using System;
using System.Collections.Generic;
using Game.City;
using Game.Simulation;
using Unity.Entities;

namespace CS2DataExport;

public sealed partial class RuntimeEcsMetricProbe
{
    public UtilityPressureSemanticsSummary CollectUtilityPressureSemanticsSummary()
    {
        if (!TryGetEntityManager(out _, out string entityManagerReason))
        {
            return new UtilityPressureSemanticsSummary
            {
                Status = MetricStatus.Unavailable,
                Notes = new[] { entityManagerReason }
            };
        }

        World? world = _getWorld();
        if (world == null || !world.IsCreated)
        {
            return new UtilityPressureSemanticsSummary
            {
                Status = MetricStatus.Unavailable,
                Notes = new[] { "runtime World is unavailable; utility systems cannot be resolved." }
            };
        }

        var notes = new List<string>
        {
            "utility pressure semantics combine water/sewage/electricity flow snapshots and city-service staffing."
        };

        WaterStatisticsSystem? waterStatistics = world.GetExistingSystemManaged<WaterStatisticsSystem>();
        WaterTradeSystem? waterTrade = world.GetExistingSystemManaged<WaterTradeSystem>();
        ElectricityStatisticsSystem? electricityStatistics = world.GetExistingSystemManaged<ElectricityStatisticsSystem>();
        CityStatisticsSystem? cityStatistics = world.GetExistingSystemManaged<CityStatisticsSystem>();

        UtilityServiceFlowSummary water = ReadWaterFlow(waterStatistics, waterTrade, notes);
        UtilityServiceFlowSummary sewage = ReadSewageFlow(waterStatistics, waterTrade, notes);
        UtilityServiceFlowSummary electricity = ReadElectricityFlow(electricityStatistics, notes);

        double? cityServiceFill = null;
        if (cityStatistics != null)
        {
            int? workers = GetOfficialStatistic(cityStatistics, StatisticType.CityServiceWorkers);
            int? maxWorkers = GetOfficialStatistic(cityStatistics, StatisticType.CityServiceMaxWorkers);
            if (workers is >= 0 && maxWorkers is > 0)
            {
                cityServiceFill = workers.Value * 100.0 / maxWorkers.Value;
            }
        }

        string waterPressure = UtilityPressureSemanticsCalculator.ClassifyWaterPressure(
            water,
            water.ImportPerMonth,
            cityServiceFill);
        string sewagePressure = UtilityPressureSemanticsCalculator.ClassifySewagePressure(sewage);

        int available = CountPresent(water.Capacity, water.Consumption)
                        + CountPresent(sewage.Capacity, sewage.Consumption)
                        + CountPresent(electricity.Capacity, electricity.Consumption)
                        + (cityServiceFill.HasValue ? 1 : 0);

        return new UtilityPressureSemanticsSummary
        {
            Status = available >= 2 ? MetricStatus.Ok : available == 1 ? MetricStatus.Partial : MetricStatus.Unavailable,
            Water = water,
            Sewage = sewage,
            Electricity = electricity,
            CityServiceFillPercent = cityServiceFill,
            WaterPressure = waterPressure,
            SewagePressure = sewagePressure,
            Notes = notes.ToArray()
        };
    }

    private static UtilityServiceFlowSummary ReadWaterFlow(
        WaterStatisticsSystem? statistics,
        WaterTradeSystem? trade,
        List<string> notes)
    {
        if (statistics == null)
        {
            notes.Add("WaterStatisticsSystem is unavailable.");
            return new UtilityServiceFlowSummary();
        }

        int? capacity = ReadIntMember(statistics, "freshCapacity", "m_FreshCapacity", "waterCapacity", "m_WaterCapacity");
        int? consumption = ReadIntMember(statistics, "freshConsumption", "m_FreshConsumption", "waterConsumption", "m_WaterConsumption");
        int? fulfilled = ReadIntMember(statistics, "freshFulfilledConsumption", "m_FreshFulfilledConsumption", "fulfilledConsumption", "m_FulfilledConsumption");
        int? import = trade == null ? null : ReadIntMember(trade, "freshImport", "m_FreshImport", "waterImport", "m_WaterImport");
        int? export = trade == null ? null : ReadIntMember(trade, "freshExport", "m_FreshExport", "waterExport", "m_WaterExport");

        return BuildUtilityFlow(capacity, consumption, fulfilled, import, export);
    }

    private static UtilityServiceFlowSummary ReadSewageFlow(
        WaterStatisticsSystem? statistics,
        WaterTradeSystem? trade,
        List<string> notes)
    {
        if (statistics == null)
        {
            notes.Add("WaterStatisticsSystem is unavailable for sewage metrics.");
            return new UtilityServiceFlowSummary();
        }

        int? capacity = ReadIntMember(statistics, "sewageCapacity", "m_SewageCapacity");
        int? consumption = ReadIntMember(statistics, "sewageConsumption", "m_SewageConsumption");
        int? fulfilled = ReadIntMember(statistics, "sewageFulfilledConsumption", "m_SewageFulfilledConsumption");
        int? import = trade == null ? null : ReadIntMember(trade, "sewageImport", "m_SewageImport");
        int? export = trade == null ? null : ReadIntMember(trade, "sewageExport", "m_SewageExport");

        return BuildUtilityFlow(capacity, consumption, fulfilled, import, export);
    }

    private static UtilityServiceFlowSummary ReadElectricityFlow(
        ElectricityStatisticsSystem? statistics,
        List<string> notes)
    {
        if (statistics == null)
        {
            notes.Add("ElectricityStatisticsSystem is unavailable.");
            return new UtilityServiceFlowSummary();
        }

        int? capacity = ReadIntMember(statistics, "production", "m_Production", "capacity", "m_Capacity");
        int? consumption = ReadIntMember(statistics, "consumption", "m_Consumption");
        int? fulfilled = ReadIntMember(statistics, "fulfilledConsumption", "m_FulfilledConsumption");
        int? import = ReadIntMember(statistics, "import", "m_Import", "importPerMonth", "m_ImportPerMonth");
        int? export = ReadIntMember(statistics, "export", "m_Export", "exportPerMonth", "m_ExportPerMonth");

        return BuildUtilityFlow(capacity, consumption, fulfilled, import, export);
    }

    private static UtilityServiceFlowSummary BuildUtilityFlow(
        int? capacity,
        int? consumption,
        int? fulfilled,
        int? import,
        int? export)
    {
        int? effectiveFulfilled = fulfilled ?? InferFulfilledConsumption(capacity, consumption);

        return new UtilityServiceFlowSummary
        {
            Capacity = capacity,
            Consumption = consumption,
            FulfilledConsumption = effectiveFulfilled,
            ImportPerMonth = import,
            ExportPerMonth = export,
            UnfulfilledConsumption = UtilityPressureSemanticsCalculator.Unfulfilled(consumption, effectiveFulfilled),
            FulfillmentPercent = UtilityPressureSemanticsCalculator.FulfillmentPercent(effectiveFulfilled, consumption)
        };
    }

    private static int? InferFulfilledConsumption(int? capacity, int? consumption)
    {
        if (!consumption.HasValue || consumption.Value <= 0)
        {
            return null;
        }

        if (!capacity.HasValue || capacity.Value <= 0)
        {
            return null;
        }

        return Math.Min(capacity.Value, consumption.Value);
    }

    private static int? ReadIntMember(object source, params string[] memberNames)
    {
        foreach (string memberName in memberNames)
        {
            if (TryExtractNamedDouble(source, memberName, out double value))
            {
                return (int)value;
            }
        }

        return null;
    }

    private static int CountPresent(params int?[] values)
    {
        int count = 0;
        foreach (int? value in values)
        {
            if (value.HasValue)
            {
                count++;
            }
        }

        return count;
    }
}
