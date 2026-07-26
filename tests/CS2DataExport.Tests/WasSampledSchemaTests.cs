using System.Text.Json;
using Xunit;

namespace CS2DataExport.Tests;

public sealed class WasSampledSchemaTests
{
    [Fact]
    public void MetricGroup_SerializesWasSampledSnakeCaseTriState()
    {
        var exact = new PopulationSummary { Status = MetricStatus.Ok, WasSampled = false };
        var estimated = new PopulationSummary { Status = MetricStatus.Ok, WasSampled = true };
        var unknown = new PopulationSummary { Status = MetricStatus.Ok };

        string exactJson = JsonSerializer.Serialize(exact);
        string estimatedJson = JsonSerializer.Serialize(estimated);
        string unknownJson = JsonSerializer.Serialize(unknown);

        Assert.Contains("\"was_sampled\":false", exactJson);
        Assert.Contains("\"was_sampled\":true", estimatedJson);
        Assert.Contains("\"was_sampled\":null", unknownJson);
    }

    [Fact]
    public void CitySnapshotDefaultsToSchema2130()
    {
        Assert.Equal("2.13.0", new CitySnapshotV1().SchemaVersion);
    }
}
