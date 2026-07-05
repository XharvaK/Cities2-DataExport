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
        World? world = _getWorld();
        if (world == null || !world.IsCreated)
        {
            return new UtilityPressureSemanticsSummary
            {
                Status = MetricStatus.Unavailable,
                Notes = new[] { "runtime World is unavailable; utility pressure cannot be resolved." }
            };
        }

        var notes = new List<string>
        {
            "utility pressure combines WaterStatisticsSystem capacity/consumption with WaterTradeSystem outside trade snapshots."
        };

        WaterStatisticsSystem? waterStatistics = world.GetExistingSystemManaged<WaterStatisticsSystem>();
        WaterTradeSystem? waterTrade = world.GetExistingSystemManaged<WaterTradeSystem>();
        ElectricityStatisticsSystem? electricityStatistics = world.GetExistingSystemManaged<ElectricityStatisticsSystem>();
        CityStatisticsSystem? cityStatistics = world.GetExistingSystemManaged<CityStatisticsSystem>();

        if (waterStatistics == null)
        {
            notes.Add("WaterStatisticsSystem is unavailable.");
        }

        if (waterTrade == null)
        {
            notes.Add("WaterTradeSystem is unavailable.");
        }

        if (electricityStatistics == null)
        {
            notes.Add("ElectricityStatisticsSystem is unavailable.");
        }

        int? freshCapacity = waterStatistics?.freshCapacity;
        int? freshConsumption = waterStatistics?.freshConsumption;
        int? fulfilledFresh = waterStatistics?.fulfilledFreshConsumption;
        int? sewageCapacity = waterStatistics?.sewageCapacity;
        int? sewageConsumption = waterStatistics?.sewageConsumption;
        int? fulfilledSewage = waterStatistics?.fulfilledSewageConsumption;

        int? freshImport = waterTrade?.freshImport;
        int? freshExport = waterTrade?.freshExport;
        int? sewageExport = waterTrade?.sewageExport;

        var water = new UtilityServiceFlowSummary
        {
            Capacity = freshCapacity,
            Consumption = freshConsumption,
            FulfilledConsumption = fulfilledFresh,
            ImportPerMonth = freshImport,
            ExportPerMonth = freshExport,
            UnfulfilledConsumption = UtilityPressureSemanticsCalculator.Unfulfilled(freshConsumption, fulfilledFresh),
            FulfillmentPercent = UtilityPressureSemanticsCalculator.FulfillmentPercent(fulfilledFresh, freshConsumption)
        };

        var sewage = new UtilityServiceFlowSummary
        {
            Capacity = sewageCapacity,
            Consumption = sewageConsumption,
            FulfilledConsumption = fulfilledSewage,
            ExportPerMonth = sewageExport,
            UnfulfilledConsumption = UtilityPressureSemanticsCalculator.Unfulfilled(sewageConsumption, fulfilledSewage),
            FulfillmentPercent = UtilityPressureSemanticsCalculator.FulfillmentPercent(fulfilledSewage, sewageConsumption)
        };

        int? electricityProduction = electricityStatistics?.production;
        int? electricityConsumption = electricityStatistics?.consumption;
        int? electricityFulfilled = electricityStatistics?.fulfilledConsumption;
        int? electricityCapacity = electricityProduction;
        if (electricityStatistics?.batteryCapacity is > 0)
        {
            electricityCapacity = (electricityProduction ?? 0) + electricityStatistics.batteryCapacity;
        }

        var electricity = new UtilityServiceFlowSummary
        {
            Capacity = electricityCapacity,
            Consumption = electricityConsumption,
            FulfilledConsumption = electricityFulfilled,
            UnfulfilledConsumption = UtilityPressureSemanticsCalculator.Unfulfilled(
                electricityConsumption,
                electricityFulfilled),
            FulfillmentPercent = UtilityPressureSemanticsCalculator.FulfillmentPercent(
                electricityFulfilled,
                electricityConsumption)
        };

        double? cityServiceFillPercent = null;
        if (cityStatistics != null)
        {
            int? workers = GetOfficialStatistic(cityStatistics, StatisticType.CityServiceWorkers);
            int? maxWorkers = GetOfficialStatistic(cityStatistics, StatisticType.CityServiceMaxWorkers);
            if (workers.HasValue && maxWorkers is > 0)
            {
                cityServiceFillPercent = workers.Value * 100.0 / maxWorkers.Value;
            }
        }

        string waterPressure = UtilityPressureSemanticsCalculator.ClassifyWaterPressure(
            water,
            freshImport,
            cityServiceFillPercent);
        string sewagePressure = UtilityPressureSemanticsCalculator.ClassifySewagePressure(sewage);

        int availableMetrics = CountPresent(freshCapacity)
            + CountPresent(freshConsumption)
            + CountPresent(fulfilledFresh)
            + CountPresent(freshImport)
            + CountPresent(freshExport)
            + CountPresent(electricityProduction)
            + CountPresent(electricityConsumption);

        if (waterPressure is "shortage" or "import_dependent_shortage" or "pressure" or "capacity_shortage")
        {
            notes.Add("fresh water demand is not fully met; citizens may report contaminated or unreliable water.");
        }

        if (sewagePressure is "shortage" or "capacity_shortage")
        {
            notes.Add("sewage capacity or fulfillment is under pressure.");
        }

        if (electricity.Consumption is > 0
            && electricity.FulfilledConsumption is int fulfilledElectricity
            && fulfilledElectricity < electricity.Consumption)
        {
            notes.Add("electricity demand is not fully met; buildings may brown out.");
        }

        return new UtilityPressureSemanticsSummary
        {
            Status = ComputeStatus(availableMetrics, expectedMetrics: 4),
            Water = water,
            Sewage = sewage,
            Electricity = electricity,
            CityServiceFillPercent = cityServiceFillPercent,
            WaterPressure = waterPressure,
            SewagePressure = sewagePressure,
            Notes = notes.ToArray()
        };
    }
}
