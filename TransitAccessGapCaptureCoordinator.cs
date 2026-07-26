using System;
using System.Collections.Generic;

namespace CS2DataExport;

public sealed class TransitAccessGapCaptureCoordinator : ITransitAccessGapCaptureCoordinator
{
    private readonly TransitAccessGapAnalyzer _analyzer;
    private readonly List<CapturedTransitTrip> _recordedTrips = new();
    private readonly List<TransitAccessGapStop> _stops = new();

    private DateTimeOffset? _captureStartedAtUtc;
    private ExportSettings? _activeSettings;
    private TransitAccessGapSemanticsSummary? _lastGoodSummary;
    private string? _passengerTripCarrierUnavailableNote;

    public TransitAccessGapCaptureCoordinator(TransitAccessGapAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public bool IsCaptureActive => _captureStartedAtUtc.HasValue;

    public void StartCaptureWindow(DateTimeOffset startedAtUtc, ExportSettings settings)
    {
        _captureStartedAtUtc = startedAtUtc;
        _activeSettings = settings;
        _recordedTrips.Clear();
        _stops.Clear();
        // Keep last-good hotspots visible during the new window / cooldown.
        _passengerTripCarrierUnavailableNote = null;
    }

    public void FinalizeCaptureWindow(DateTimeOffset finalizedAtUtc, ExportSettings settings)
    {
        if (!_captureStartedAtUtc.HasValue)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_passengerTripCarrierUnavailableNote))
        {
            // Do not overwrite a previous ok last-good with an unavailable finalize.
            if (_lastGoodSummary == null || _lastGoodSummary.Status != MetricStatus.Ok)
            {
                _lastGoodSummary = new TransitAccessGapSemanticsSummary
                {
                    Status = MetricStatus.Unavailable,
                    Notes = new[] { _passengerTripCarrierUnavailableNote! }
                };
            }

            _captureStartedAtUtc = null;
            _activeSettings = null;
            return;
        }

        var completedCapture = new CompletedTransitAccessGapCapture
        {
            CaptureMode = settings.TransitTripCaptureMode == TransitTripCaptureMode.NextExportWindow
                ? "next_export_window"
                : "off",
            CaptureDurationSeconds = Math.Max(0, (int)(finalizedAtUtc - _captureStartedAtUtc.Value).TotalSeconds),
            IncludeOutsideTrips = settings.TransitTripCaptureIncludeOutsideTrips,
            ClusterRadiusM = settings.EffectiveTransitTripCaptureClusterRadiusMeters,
            StopCoverageRadiusM = 250,
            MaxSampleRoutesPerHotspot = settings.EffectiveTransitTripCaptureMaxSampleRoutesPerHotspot,
            MaxHotspots = settings.EffectiveTransitTripCaptureMaxHotspots
        };

        for (int index = 0; index < _recordedTrips.Count; index++)
        {
            completedCapture.RecordedTrips.Add(_recordedTrips[index]);
        }

        for (int index = 0; index < _stops.Count; index++)
        {
            completedCapture.Stops.Add(_stops[index]);
        }

        TransitAccessGapSemanticsSummary built = _analyzer.BuildSummary(completedCapture);
        if (built.Status == MetricStatus.Ok)
        {
            _lastGoodSummary = built;
        }
        else if (_lastGoodSummary == null)
        {
            _lastGoodSummary = built;
        }

        _captureStartedAtUtc = null;
        _activeSettings = null;
    }

    public void ClearCompletedCapture()
    {
        // Last-good survives cooldown and the post-finalize tick so Insights stay useful.
        // Working trip buffers are already cleared when the window ends / next Start.
        _passengerTripCarrierUnavailableNote = null;
    }

    public void ResetForWorldUnload()
    {
        _captureStartedAtUtc = null;
        _activeSettings = null;
        _recordedTrips.Clear();
        _stops.Clear();
        _lastGoodSummary = null;
        _passengerTripCarrierUnavailableNote = null;
    }

    public void RecordTrip(CapturedTransitTrip trip)
    {
        if (!IsCaptureActive)
        {
            return;
        }

        bool shouldIncludeTrip = _activeSettings?.TransitTripCaptureIncludeOutsideTrips == true || !trip.IncludesOutsideConnection;
        if (shouldIncludeTrip)
        {
            _recordedTrips.Add(trip);
        }
    }

    public void ReplaceStops(IEnumerable<TransitAccessGapStop> stops)
    {
        _stops.Clear();
        foreach (TransitAccessGapStop stop in stops)
        {
            _stops.Add(stop);
        }
    }

    public bool TryGetCompletedSummary(out TransitAccessGapSemanticsSummary summary)
    {
        if (_lastGoodSummary != null)
        {
            summary = _lastGoodSummary;
            return true;
        }

        summary = new TransitAccessGapSemanticsSummary();
        return false;
    }

    public void MarkPassengerTripCarrierUnavailable(string note)
    {
        _passengerTripCarrierUnavailableNote = note;
        if (_lastGoodSummary == null || _lastGoodSummary.Status != MetricStatus.Ok)
        {
            _lastGoodSummary = new TransitAccessGapSemanticsSummary
            {
                Status = MetricStatus.Unavailable,
                Notes = new[] { note }
            };
        }
    }
}
