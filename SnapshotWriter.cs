using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Colossal.Json;

namespace CS2DataExport
{
    public sealed class SnapshotWriteResult
    {
        public SnapshotWriteResult(string? snapshotPath, string latestPath, int keptSnapshots, int deletedSnapshots)
        {
            SnapshotPath = snapshotPath;
            LatestPath = latestPath;
            KeptSnapshots = keptSnapshots;
            DeletedSnapshots = deletedSnapshots;
        }

        /// <summary>
        /// Intended dated snapshot path for this export, or null when this export skips a dated file.
        /// With async writes the file may still be in flight when this result is returned.
        /// </summary>
        public string? SnapshotPath { get; }

        public string LatestPath { get; }
        public int KeptSnapshots { get; }
        public int DeletedSnapshots { get; }
    }

    public sealed class SnapshotWriter
    {
        public const int DatedSnapshotEveryNExports = 3;
        public const int RetentionEveryNExports = 10;

        private readonly object _queueLock = new();
        private readonly Action? _beforeWriteHook;
        private readonly Action<string>? _log;
        private PendingWrite? _pending;
        private bool _workerRunning;
        private int _exportSequence;
        private int _exportsQueuedSinceRetention;
        private bool _retentionDue;
        private int _lastKeptSnapshots;
        private int _lastDeletedSnapshots;

        public SnapshotWriter(Action? beforeWriteHook = null, Action<string>? log = null)
        {
            _beforeWriteHook = beforeWriteHook;
            _log = log;
        }

        public SnapshotWriteResult WriteSnapshot(
            CitySnapshotV1 snapshot,
            DateTimeOffset exportedAtUtc,
            ExportSettings settings)
        {
            string outputRoot = settings.ResolveOutputRoot();
            string snapshotsDir = settings.ResolveSnapshotsDirectory();
            string latestPath = settings.ResolveLatestFilePath();

            Directory.CreateDirectory(outputRoot);
            Directory.CreateDirectory(snapshotsDir);

            string? snapshotPath;
            int kept;
            int deleted;
            bool startWorker;

            lock (_queueLock)
            {
                _exportSequence++;
                bool writeDated = (_exportSequence % DatedSnapshotEveryNExports) == 0;
                snapshotPath = null;
                if (writeDated)
                {
                    string timestamp = exportedAtUtc.UtcDateTime.ToString("yyyyMMdd-HHmmss");
                    snapshotPath = Path.Combine(snapshotsDir, timestamp + ".json");
                }

                _exportsQueuedSinceRetention++;
                if (_exportsQueuedSinceRetention >= RetentionEveryNExports)
                {
                    _exportsQueuedSinceRetention = 0;
                    _retentionDue = true;
                }

                _pending = new PendingWrite(
                    snapshot,
                    latestPath,
                    snapshotPath,
                    snapshotsDir,
                    settings.EffectiveRetentionCount);

                startWorker = !_workerRunning;
                if (startWorker)
                {
                    _workerRunning = true;
                }

                kept = _lastKeptSnapshots;
                deleted = _lastDeletedSnapshots;
            }

            if (startWorker)
            {
                ThreadPool.QueueUserWorkItem(_ => DrainQueue());
            }

            return new SnapshotWriteResult(snapshotPath, latestPath, kept, deleted);
        }

        /// <summary>
        /// Blocks until queued writes drain. Used by tests.
        /// </summary>
        public bool WaitForIdle(TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                lock (_queueLock)
                {
                    if (!_workerRunning && _pending == null)
                    {
                        return true;
                    }
                }

                Thread.Sleep(10);
            }

            return false;
        }

        private void DrainQueue()
        {
            while (true)
            {
                PendingWrite? work;
                lock (_queueLock)
                {
                    work = _pending;
                    _pending = null;
                    if (work == null)
                    {
                        _workerRunning = false;
                        return;
                    }
                }

                try
                {
                    ProcessWrite(work);
                }
                catch
                {
                    // Keep draining so a later coalesce still lands on disk.
                }
            }
        }

        private void ProcessWrite(PendingWrite work)
        {
            _beforeWriteHook?.Invoke();

            string payload;
            using (ExportProfiler.Measure("json_dump", _log))
            {
                payload = JSON.Dump(work.Snapshot);
            }

            using (ExportProfiler.Measure("write_latest", _log))
            {
                WriteTextAtomic(work.LatestPath, payload);
            }

            if (!string.IsNullOrWhiteSpace(work.DatedSnapshotPath))
            {
                using (ExportProfiler.Measure("write_snapshot", _log))
                {
                    WriteTextAtomic(work.DatedSnapshotPath!, payload);
                }
            }

            bool runRetention;
            lock (_queueLock)
            {
                runRetention = _retentionDue;
                if (runRetention)
                {
                    _retentionDue = false;
                }
            }

            int deleted = 0;
            if (runRetention)
            {
                deleted = ApplyRetentionPolicy(work.SnapshotsDir, work.RetentionCount);
            }

            int kept = _lastKeptSnapshots;
            try
            {
                kept = Directory.GetFiles(work.SnapshotsDir, "*.json", SearchOption.TopDirectoryOnly).Length;
            }
            catch
            {
                // Keep previous kept count.
            }

            lock (_queueLock)
            {
                _lastKeptSnapshots = kept;
                _lastDeletedSnapshots = deleted;
            }
        }

        private static void WriteTextAtomic(string filePath, string payload)
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Could not resolve directory for '" + filePath + "'.");
            }

            Directory.CreateDirectory(directory);
            string tempPath = Path.Combine(directory, Path.GetFileName(filePath) + ".tmp");

            File.WriteAllText(tempPath, payload, new UTF8Encoding(false));

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            File.Move(tempPath, filePath);
        }

        private static int ApplyRetentionPolicy(string snapshotsDir, int retentionCount)
        {
            string[] snapshots = Directory
                .GetFiles(snapshotsDir, "*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .ToArray();

            if (snapshots.Length <= retentionCount)
            {
                return 0;
            }

            int deleted = 0;
            for (int i = retentionCount; i < snapshots.Length; i++)
            {
                File.Delete(snapshots[i]);
                deleted++;
            }

            return deleted;
        }

        private sealed class PendingWrite
        {
            public PendingWrite(
                CitySnapshotV1 snapshot,
                string latestPath,
                string? datedSnapshotPath,
                string snapshotsDir,
                int retentionCount)
            {
                Snapshot = snapshot;
                LatestPath = latestPath;
                DatedSnapshotPath = datedSnapshotPath;
                SnapshotsDir = snapshotsDir;
                RetentionCount = retentionCount;
            }

            public CitySnapshotV1 Snapshot { get; }
            public string LatestPath { get; }
            public string? DatedSnapshotPath { get; }
            public string SnapshotsDir { get; }
            public int RetentionCount { get; }
        }
    }
}
