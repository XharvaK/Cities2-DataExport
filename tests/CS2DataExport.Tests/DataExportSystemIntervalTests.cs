using System;
using System.IO;
using Xunit;

namespace CS2DataExport.Tests;

public sealed class DataExportSystemIntervalTests
{
    [Fact]
    public void Tick_ExportsAgainAfterTenSeconds()
    {
        var settings = new ExportSettings
        {
            ExportEnabled = true,
            IntervalSeconds = 10,
            OutputRootOverride = CreateTempOutputRoot(),
        };

        var collector = new MetricsCollector(new FakeMetricProbe());
        var system = new DataExportSystem(
            settings,
            collector,
            new SnapshotWriter(),
            "1.0.0",
            "test-build",
            log: _ => { });

        ExportTickResult first = system.Tick(new DateTimeOffset(2026, 7, 4, 7, 0, 0, TimeSpan.Zero));
        ExportTickResult early = system.Tick(new DateTimeOffset(2026, 7, 4, 7, 0, 5, TimeSpan.Zero));
        ExportTickResult second = system.Tick(new DateTimeOffset(2026, 7, 4, 7, 0, 10, TimeSpan.Zero));

        Assert.True(first.DidExport);
        Assert.False(early.DidExport);
        Assert.True(second.DidExport);
    }

    private static string CreateTempOutputRoot()
    {
        return Path.Combine(Path.GetTempPath(), "CS2DataExport.Tests", Guid.NewGuid().ToString("N"));
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
        public DemandFactorsSemanticsSummary CollectDemandFactorsSemanticsSummary() => new() { Status = MetricStatus.Unavailable };
        public UtilitiesServicesSemanticsSummary CollectUtilitiesServicesSemanticsSummary() => new() { Status = MetricStatus.Unavailable };
    }
}
