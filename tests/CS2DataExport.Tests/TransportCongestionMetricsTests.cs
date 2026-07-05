using Xunit;

namespace CS2DataExport.Tests;

public sealed class TransportCongestionMetricsTests
{
    [Theory]
    [InlineData(0, 1000, 0.0)]
    [InlineData(42, 1000, 0.042)]
    [InlineData(500, 1000, 0.5)]
    [InlineData(1500, 1000, 1.0)]
    public void ComputeCongestionIndex_ClampsAndRounds(
        int slowBlockedVehicles,
        int roadVehicleEntities,
        double expected)
    {
        double? index = TransportCongestionMetrics.ComputeCongestionIndex(
            slowBlockedVehicles,
            roadVehicleEntities);

        Assert.NotNull(index);
        Assert.Equal(expected, index.Value, precision: 4);
    }

    [Fact]
    public void ComputeCongestionIndex_ReturnsNullWhenNoRoadVehicles()
    {
        Assert.Null(TransportCongestionMetrics.ComputeCongestionIndex(10, 0));
    }
}
