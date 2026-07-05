using Xunit;

namespace CS2DataExport.Tests;

public sealed class ExportSettingsTests
{
    [Fact]
    public void DefaultSettings_EnableTransitTripCapture()
    {
        var settings = new ExportSettings();

        Assert.Equal(TransitTripCaptureMode.NextExportWindow, settings.TransitTripCaptureMode);
        Assert.Equal(3, settings.TransitTripCaptureWindowMinutes);
    }

    [Fact]
    public void FromEnvironment_DisablesTransitCaptureWhenRequested()
    {
        string? previous = Environment.GetEnvironmentVariable("CS2DATAEXPORT_TRANSIT_CAPTURE");
        try
        {
            Environment.SetEnvironmentVariable("CS2DATAEXPORT_TRANSIT_CAPTURE", "off");
            ExportSettings settings = ExportSettings.FromEnvironment();
            Assert.Equal(TransitTripCaptureMode.Off, settings.TransitTripCaptureMode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CS2DATAEXPORT_TRANSIT_CAPTURE", previous);
        }
    }
}
