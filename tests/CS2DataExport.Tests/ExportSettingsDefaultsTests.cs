using Xunit;

namespace CS2DataExport.Tests;

public sealed class ExportSettingsDefaultsTests
{
    [Fact]
    public void Defaults_MatchLargeCityFriendlyCadence()
    {
        var settings = new ExportSettings();

        Assert.Equal(10, settings.EffectiveIntervalSeconds);
        Assert.Equal(500, settings.EffectiveRetentionCount);
        Assert.Equal(10, settings.EffectiveTransitCaptureCooldownMinutes);
        Assert.Equal(TransitTripCaptureMode.NextExportWindow, settings.TransitTripCaptureMode);
    }
}
