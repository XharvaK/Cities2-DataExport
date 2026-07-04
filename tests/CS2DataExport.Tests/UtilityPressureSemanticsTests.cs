using Xunit;

namespace CS2DataExport.Tests;

public sealed class UtilityPressureSemanticsTests
{
    [Fact]
    public void ClassifyWaterPressure_FlagsImportDependentShortage()
    {
        var water = new UtilityServiceFlowSummary
        {
            Consumption = 1000,
            FulfilledConsumption = 800,
            ImportPerMonth = 250
        };

        string pressure = UtilityPressureSemanticsCalculator.ClassifyWaterPressure(water, 250, 70);

        Assert.Equal("import_dependent_shortage", pressure);
    }

    [Fact]
    public void ClassifySewagePressure_FlagsShortageWhenUnfulfilled()
    {
        var sewage = new UtilityServiceFlowSummary
        {
            Consumption = 500,
            FulfilledConsumption = 420
        };

        Assert.Equal("shortage", UtilityPressureSemanticsCalculator.ClassifySewagePressure(sewage));
    }

    [Fact]
    public void BuildServiceTrade_IncludesWaterAndSewageImports()
    {
        var water = new UtilityServiceFlowSummary { ImportPerMonth = 1200, ExportPerMonth = 0 };
        var sewage = new UtilityServiceFlowSummary { ExportPerMonth = 300 };
        var electricity = new UtilityServiceFlowSummary();

        var trade = UtilityPressureSemanticsCalculator.BuildServiceTrade(water, sewage, electricity);

        Assert.NotNull(trade);
        Assert.Equal(1200, trade!["water"]);
        Assert.Equal(300, trade["sewage"]);
        Assert.False(trade.ContainsKey("electricity"));
    }

    [Fact]
    public void CollectSnapshot_IncludesUtilityPressureSemanticsGroup()
    {
        var collector = new MetricsCollector(new FakeMetricProbe());
        CitySnapshotV1 snapshot = collector.CollectSnapshot(
            new System.DateTimeOffset(2026, 7, 4, 12, 0, 0, System.TimeSpan.Zero),
            "test-mod",
            "test-build");

        Assert.Equal("2.8.0", snapshot.SchemaVersion);
        Assert.NotNull(snapshot.UtilityPressureSemantics);
        Assert.Equal(MetricStatus.Ok, snapshot.UtilityPressureSemantics.Status);
        Assert.Equal("pressure", snapshot.UtilityPressureSemantics.WaterPressure);
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
        public UtilityPressureSemanticsSummary CollectUtilityPressureSemanticsSummary() => new()
        {
            Status = MetricStatus.Ok,
            WaterPressure = "pressure",
            Water = new UtilityServiceFlowSummary
            {
                Consumption = 1000,
                FulfilledConsumption = 900,
                ImportPerMonth = 100
            }
        };
    }
}
