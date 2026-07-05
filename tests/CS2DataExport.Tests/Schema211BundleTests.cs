using System;
using System.Collections.Generic;
using Xunit;

namespace CS2DataExport.Tests;

public sealed class Schema211BundleTests
{
    [Fact]
    public void CollectSnapshot_IncludesDemandAndUtilitiesGroups_AndMetaStatus()
    {
        var collector = new MetricsCollector(new FakeMetricProbe());

        CitySnapshotV1 snapshot = collector.CollectSnapshot(
            exportedAtUtc: new DateTimeOffset(2026, 7, 5, 15, 0, 0, TimeSpan.Zero),
            modVersion: "1.0.0",
            gameBuild: "test-build");

        Assert.Equal("2.11.0", snapshot.SchemaVersion);
        Assert.Equal(MetricStatus.Ok, snapshot.DemandFactorsSemantics.Status);
        Assert.Equal(0.62, snapshot.DemandFactorsSemantics.CommercialDemand);
        Assert.Contains("taxes", snapshot.DemandFactorsSemantics.CommercialFactors!.Keys);
        Assert.Equal(MetricStatus.Ok, snapshot.UtilitiesServicesSemantics.Status);
        Assert.Equal(1200, snapshot.UtilitiesServicesSemantics.ElectricityProduction);
        Assert.Equal("shortage", snapshot.UtilitiesServicesSemantics.ElectricityPressure);
        Assert.Equal(MetricMeasurementKind.Observed, snapshot.DemandFactorsSemantics.MetricMetadata["residential_demand"].MeasurementKind);
        Assert.Equal(MetricStatus.Ok, snapshot.Meta.MetricStatus["demand_factors_semantics"]);
        Assert.Equal(MetricStatus.Ok, snapshot.Meta.MetricStatus["utilities_services_semantics"]);
    }

    [Fact]
    public void DemandFactorsCalculator_NormalizesSignedDemandToZeroToOne()
    {
        Assert.Equal(0.5, DemandFactorsSemanticsCalculator.NormalizeDemand(0));
        Assert.Equal(0.75, DemandFactorsSemanticsCalculator.NormalizeDemand(50));
        Assert.Equal(0.25, DemandFactorsSemanticsCalculator.NormalizeDemand(-50));
    }

    private sealed class FakeMetricProbe : IMetricProbe
    {
        public CitySummary CollectCitySummary() => new() { Status = MetricStatus.Ok };
        public PopulationSummary CollectPopulationSummary() => new() { Status = MetricStatus.Ok };
        public EducationSummary CollectEducationSummary() => new() { Status = MetricStatus.Ok };
        public TransportProxySummary CollectTransportProxySummary() => new() { Status = MetricStatus.Ok };
        public WorkforceSummary CollectWorkforceSummary() => new() { Status = MetricStatus.Ok };
        public WorkplacesSummary CollectWorkplacesSummary() => new() { Status = MetricStatus.Ok };
        public MobilitySummary CollectMobilitySummary() => new() { Status = MetricStatus.Ok };
        public EconomySignalsSummary CollectEconomySignalsSummary() => new() { Status = MetricStatus.Ok };
        public ExternalConnectionsSummary CollectExternalConnectionsSummary() => new() { Status = MetricStatus.Ok };
        public LaborMarketDetailSummary CollectLaborMarketDetailSummary() => new() { Status = MetricStatus.Ok };
        public FacilityIdentitySummary CollectFacilityIdentitySummary() => new() { Status = MetricStatus.Ok };
        public CompanyServiceSemanticsSummary CollectCompanyServiceSemanticsSummary() => new() { Status = MetricStatus.Ok };
        public HousingPressureSemanticsSummary CollectHousingPressureSemanticsSummary() => new() { Status = MetricStatus.Ok };
        public HouseholdPressureContextSummary CollectHouseholdPressureContextSummary() => new() { Status = MetricStatus.Ok };
        public LaborPressureContextSummary CollectLaborPressureContextSummary() => new() { Status = MetricStatus.Ok };
        public TransitLineDetailSemanticsSummary CollectTransitLineDetailSemanticsSummary() => new() { Status = MetricStatus.Unavailable };
        public TransitAccessGapSemanticsSummary CollectTransitAccessGapSemanticsSummary() => new() { Status = MetricStatus.Unavailable };
        public OfficialCityStatisticsSummary CollectOfficialCityStatisticsSummary() => new() { Status = MetricStatus.Unavailable };
        public UtilityPressureSemanticsSummary CollectUtilityPressureSemanticsSummary() => new() { Status = MetricStatus.Unavailable };

        public DemandFactorsSemanticsSummary CollectDemandFactorsSemanticsSummary() => new()
        {
            Status = MetricStatus.Ok,
            ResidentialDemand = 0.41,
            CommercialDemand = 0.62,
            IndustrialDemand = 0.28,
            CommercialFactors = new SortedDictionary<string, int>(StringComparer.Ordinal)
            {
                ["taxes"] = -8,
                ["happiness"] = 4
            }
        };

        public UtilitiesServicesSemanticsSummary CollectUtilitiesServicesSemanticsSummary() => new()
        {
            Status = MetricStatus.Ok,
            ElectricityProduction = 1200,
            ElectricityConsumption = 1500,
            ElectricityCapacity = 1200,
            ElectricityFulfilledConsumption = 1100,
            ElectricityFulfillmentPercent = 73.3,
            ElectricityPressure = "shortage",
            GarbageAccumulation = 42000
        };
    }
}
