using System;
using System.IO;
using System.Threading;
using Xunit;

namespace CS2DataExport.Tests;

public sealed class SnapshotWriterTests
{
    [Fact]
    public void WriteSnapshot_AlwaysWritesLatest_AndDatedEveryThirdExport()
    {
        string outputRoot = CreateTempOutputRoot();
        var settings = new ExportSettings { OutputRootOverride = outputRoot, RetentionCount = 500 };
        var writer = new SnapshotWriter();
        DateTimeOffset t0 = new(2026, 7, 16, 1, 0, 0, TimeSpan.Zero);

        for (int i = 1; i <= 6; i++)
        {
            var snapshot = MinimalSnapshot(modVersion: "v" + i);
            SnapshotWriteResult result = writer.WriteSnapshot(snapshot, t0.AddSeconds(i), settings);

            Assert.Equal(settings.ResolveLatestFilePath(), result.LatestPath);
            if (i % SnapshotWriter.DatedSnapshotEveryNExports == 0)
            {
                Assert.NotNull(result.SnapshotPath);
            }
            else
            {
                Assert.Null(result.SnapshotPath);
            }

            Assert.True(writer.WaitForIdle(TimeSpan.FromSeconds(10)));
        }

        string latestPath = settings.ResolveLatestFilePath();
        Assert.True(File.Exists(latestPath));
        string latestJson = File.ReadAllText(latestPath);
        Assert.Contains("\"mod_version\":\"v6\"", latestJson, StringComparison.Ordinal);

        string[] dated = Directory.GetFiles(settings.ResolveSnapshotsDirectory(), "*.json");
        Assert.Equal(2, dated.Length);
    }

    [Fact]
    public void WriteSnapshot_CoalesceKeepsLatestPendingDto()
    {
        string outputRoot = CreateTempOutputRoot();
        var settings = new ExportSettings { OutputRootOverride = outputRoot };
        var writeStarted = new ManualResetEventSlim(false);
        var allowWrite = new ManualResetEventSlim(false);

        var writer = new SnapshotWriter(() =>
        {
            writeStarted.Set();
            Assert.True(allowWrite.Wait(TimeSpan.FromSeconds(10)));
        });

        DateTimeOffset t0 = new(2026, 7, 16, 2, 0, 0, TimeSpan.Zero);

        writer.WriteSnapshot(MinimalSnapshot("first"), t0, settings);
        Assert.True(writeStarted.Wait(TimeSpan.FromSeconds(10)));

        writer.WriteSnapshot(MinimalSnapshot("middle"), t0.AddSeconds(1), settings);
        writer.WriteSnapshot(MinimalSnapshot("latest-wins"), t0.AddSeconds(2), settings);

        allowWrite.Set();
        Assert.True(writer.WaitForIdle(TimeSpan.FromSeconds(10)));

        string latestJson = File.ReadAllText(settings.ResolveLatestFilePath());
        Assert.Contains("\"mod_version\":\"latest-wins\"", latestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"mod_version\":\"middle\"", latestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"mod_version\":\"first\"", latestJson, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSnapshot_RetentionRunsAfterEnoughQueuedExports()
    {
        string outputRoot = CreateTempOutputRoot();
        var settings = new ExportSettings
        {
            OutputRootOverride = outputRoot,
            RetentionCount = 2
        };
        var writer = new SnapshotWriter();
        DateTimeOffset t0 = new(2026, 7, 16, 3, 0, 0, TimeSpan.Zero);

        // Seed more dated files than retention allows.
        string snapshotsDir = settings.ResolveSnapshotsDirectory();
        Directory.CreateDirectory(snapshotsDir);
        for (int i = 0; i < 5; i++)
        {
            string path = Path.Combine(snapshotsDir, $"2026010{i}-000000.json");
            File.WriteAllText(path, "{}");
        }

        for (int i = 0; i < SnapshotWriter.RetentionEveryNExports; i++)
        {
            writer.WriteSnapshot(MinimalSnapshot("r" + i), t0.AddSeconds(i), settings);
        }

        Assert.True(writer.WaitForIdle(TimeSpan.FromSeconds(10)));

        int remaining = Directory.GetFiles(snapshotsDir, "*.json").Length;
        Assert.True(remaining <= settings.EffectiveRetentionCount);
    }

    private static CitySnapshotV1 MinimalSnapshot(string modVersion)
    {
        return new CitySnapshotV1
        {
            SchemaVersion = "2.12.0",
            ExportedAtUtc = "2026-07-16T00:00:00Z",
            ModVersion = modVersion,
            City = new CitySummary { Status = MetricStatus.Ok }
        };
    }

    private static string CreateTempOutputRoot()
    {
        return Path.Combine(Path.GetTempPath(), "CS2DataExport.Tests", Guid.NewGuid().ToString("N"));
    }
}
