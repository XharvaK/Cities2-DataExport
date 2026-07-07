using System;
using System.Diagnostics;
using System.Globalization;

namespace CS2DataExport;

public static class ExportProfiler
{
    private static readonly bool s_enabled = IsEnabled();

    public static bool Enabled => s_enabled;

    public static IDisposable Measure(string label, Action<string>? log = null)
    {
        if (!s_enabled)
        {
            return NoOpDisposable.Instance;
        }

        return new TimedScope(label, log);
    }

    public static void Log(string message, Action<string>? log = null)
    {
        if (!s_enabled)
        {
            return;
        }

        if (log != null)
        {
            log(message);
            return;
        }

        Console.WriteLine("[CS2DataExport] " + message);
    }

    private static bool IsEnabled()
    {
        string? value = Environment.GetEnvironmentVariable("CS2DATAEXPORT_PROFILE");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToLowerInvariant();
        return normalized is "1" or "true" or "yes" or "on";
    }

    private sealed class TimedScope : IDisposable
    {
        private readonly string _label;
        private readonly Action<string>? _log;
        private readonly Stopwatch _stopwatch;

        public TimedScope(string label, Action<string>? log)
        {
            _label = label;
            _log = log;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            Log(
                "profile " + _label + "=" + _stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms",
                _log);
        }
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
