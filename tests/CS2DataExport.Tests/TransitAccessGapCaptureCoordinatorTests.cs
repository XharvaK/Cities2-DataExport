using System;
using Xunit;

namespace CS2DataExport.Tests;

public sealed class TransitAccessGapCaptureCoordinatorTests
{
    [Fact]
    public void FinalizeCaptureWindow_BuildsCompletedSummary_AndClearKeepsLastGood()
    {
        var coordinator = new TransitAccessGapCaptureCoordinator(new TransitAccessGapAnalyzer());
        var settings = new ExportSettings
        {
            TransitTripCaptureMode = TransitTripCaptureMode.NextExportWindow,
            TransitTripCaptureClusterRadiusMeters = 192,
            TransitTripCaptureMaxSampleRoutesPerHotspot = 5
        };

        coordinator.StartCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 0, 0, TimeSpan.Zero), settings);
        coordinator.ReplaceStops(new[]
        {
            new TransitAccessGapStop(0, 0, 0, 100)
        });
        coordinator.RecordTrip(new CapturedTransitTrip
        {
            Anchors =
            {
                new TransitAccessGapAnchor(400, 0, 400)
            }
        });

        coordinator.FinalizeCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 10, 0, TimeSpan.Zero), settings);

        Assert.True(coordinator.TryGetCompletedSummary(out TransitAccessGapSemanticsSummary summary));
        Assert.Equal(MetricStatus.Ok, summary.Status);
        Assert.Single(summary.Hotspots);

        coordinator.ClearCompletedCapture();

        Assert.True(coordinator.TryGetCompletedSummary(out TransitAccessGapSemanticsSummary retained));
        Assert.Equal(MetricStatus.Ok, retained.Status);
        Assert.Single(retained.Hotspots);
    }

    [Fact]
    public void StartCaptureWindow_KeepsPreviousOkLastGood()
    {
        var coordinator = new TransitAccessGapCaptureCoordinator(new TransitAccessGapAnalyzer());
        var settings = new ExportSettings
        {
            TransitTripCaptureMode = TransitTripCaptureMode.NextExportWindow,
            TransitTripCaptureClusterRadiusMeters = 192,
            TransitTripCaptureMaxSampleRoutesPerHotspot = 5
        };

        coordinator.StartCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 0, 0, TimeSpan.Zero), settings);
        coordinator.ReplaceStops(new[] { new TransitAccessGapStop(0, 0, 0, 100) });
        coordinator.RecordTrip(new CapturedTransitTrip
        {
            Anchors = { new TransitAccessGapAnchor(400, 0, 400) }
        });
        coordinator.FinalizeCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 10, 0, TimeSpan.Zero), settings);
        coordinator.ClearCompletedCapture();

        coordinator.StartCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 25, 0, TimeSpan.Zero), settings);

        Assert.True(coordinator.IsCaptureActive);
        Assert.True(coordinator.TryGetCompletedSummary(out TransitAccessGapSemanticsSummary lastGood));
        Assert.Equal(MetricStatus.Ok, lastGood.Status);
        Assert.Single(lastGood.Hotspots);
    }

    [Fact]
    public void ResetForWorldUnload_ClearsLastGood()
    {
        var coordinator = new TransitAccessGapCaptureCoordinator(new TransitAccessGapAnalyzer());
        var settings = new ExportSettings
        {
            TransitTripCaptureMode = TransitTripCaptureMode.NextExportWindow,
            TransitTripCaptureClusterRadiusMeters = 192,
            TransitTripCaptureMaxSampleRoutesPerHotspot = 5
        };

        coordinator.StartCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 0, 0, TimeSpan.Zero), settings);
        coordinator.ReplaceStops(new[] { new TransitAccessGapStop(0, 0, 0, 100) });
        coordinator.RecordTrip(new CapturedTransitTrip
        {
            Anchors = { new TransitAccessGapAnchor(400, 0, 400) }
        });
        coordinator.FinalizeCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 10, 0, TimeSpan.Zero), settings);

        coordinator.ResetForWorldUnload();

        Assert.False(coordinator.TryGetCompletedSummary(out _));
        Assert.False(coordinator.IsCaptureActive);
    }

    [Fact]
    public void MarkPassengerTripCarrierUnavailable_DoesNotOverwriteOkLastGood()
    {
        var coordinator = new TransitAccessGapCaptureCoordinator(new TransitAccessGapAnalyzer());
        var settings = new ExportSettings
        {
            TransitTripCaptureMode = TransitTripCaptureMode.NextExportWindow,
            TransitTripCaptureClusterRadiusMeters = 192,
            TransitTripCaptureMaxSampleRoutesPerHotspot = 5
        };

        coordinator.StartCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 0, 0, TimeSpan.Zero), settings);
        coordinator.ReplaceStops(new[] { new TransitAccessGapStop(0, 0, 0, 100) });
        coordinator.RecordTrip(new CapturedTransitTrip
        {
            Anchors = { new TransitAccessGapAnchor(400, 0, 400) }
        });
        coordinator.FinalizeCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 10, 0, TimeSpan.Zero), settings);

        coordinator.StartCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 25, 0, TimeSpan.Zero), settings);
        coordinator.MarkPassengerTripCarrierUnavailable("no proven passenger-trip runtime carrier");
        coordinator.FinalizeCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 35, 0, TimeSpan.Zero), settings);

        Assert.True(coordinator.TryGetCompletedSummary(out TransitAccessGapSemanticsSummary summary));
        Assert.Equal(MetricStatus.Ok, summary.Status);
        Assert.Single(summary.Hotspots);
    }

    [Fact]
    public void MarkPassengerTripCarrierUnavailable_WinsWhenNoOkLastGood()
    {
        var coordinator = new TransitAccessGapCaptureCoordinator(new TransitAccessGapAnalyzer());
        var settings = new ExportSettings
        {
            TransitTripCaptureMode = TransitTripCaptureMode.NextExportWindow
        };

        coordinator.StartCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 0, 0, TimeSpan.Zero), settings);
        coordinator.MarkPassengerTripCarrierUnavailable("no proven passenger-trip runtime carrier");
        coordinator.RecordTrip(new CapturedTransitTrip
        {
            Anchors =
            {
                new TransitAccessGapAnchor(10, 0, 10)
            }
        });

        coordinator.FinalizeCaptureWindow(new DateTimeOffset(2026, 4, 5, 18, 10, 0, TimeSpan.Zero), settings);

        Assert.True(coordinator.TryGetCompletedSummary(out TransitAccessGapSemanticsSummary summary));
        Assert.Equal(MetricStatus.Unavailable, summary.Status);
        Assert.Contains("no proven passenger-trip runtime carrier", summary.Notes[0]);
    }

    [Fact]
    public void StartCaptureWindow_ClearsPassengerCarrierUnavailableLatchForNewWindow()
    {
        var coordinator = new TransitAccessGapCaptureCoordinator(new TransitAccessGapAnalyzer());
        var settings = new ExportSettings
        {
            TransitTripCaptureMode = TransitTripCaptureMode.NextExportWindow
        };

        coordinator.MarkPassengerTripCarrierUnavailable("no proven passenger-trip runtime carrier");
        coordinator.StartCaptureWindow(new DateTimeOffset(2026, 4, 5, 19, 0, 0, TimeSpan.Zero), settings);
        coordinator.RecordTrip(new CapturedTransitTrip
        {
            Anchors =
            {
                new TransitAccessGapAnchor(10, 0, 10)
            }
        });

        coordinator.FinalizeCaptureWindow(new DateTimeOffset(2026, 4, 5, 19, 10, 0, TimeSpan.Zero), settings);

        Assert.True(coordinator.TryGetCompletedSummary(out TransitAccessGapSemanticsSummary summary));
        Assert.Equal(MetricStatus.Ok, summary.Status);
        Assert.Single(summary.Hotspots);
    }
}
