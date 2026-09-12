using Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace GameInterface.Services.BugReporting;

/// <summary>Describes one client log collected for a diagnostic report.</summary>
public sealed class CollectedBugReportLog
{
    public int ClientNumber { get; }
    public byte[] CompressedData { get; }
    public int UncompressedLength { get; }

    public CollectedBugReportLog(int clientNumber, byte[] compressedData, int uncompressedLength)
    {
        ClientNumber = clientNumber;
        CompressedData = compressedData;
        UncompressedLength = uncompressedLength;
    }
}

/// <summary>Contains one player-written summary and description.</summary>
public sealed class BugReportSubmission
{
    public string ReportingClientNetworkId { get; }
    public string Summary { get; }
    public string Description { get; }

    public BugReportSubmission(
        string reportingClientNetworkId,
        string summary,
        string description)
    {
        ReportingClientNetworkId = reportingClientNetworkId;
        Summary = summary;
        Description = description;
    }
}

/// <summary>Contains the redacted server log captured for a diagnostic report.</summary>
public sealed class CollectedBugReportServerLog
{
    public byte[] CompressedData { get; }
    public int UncompressedLength { get; }

    public CollectedBugReportServerLog(byte[] compressedData, int uncompressedLength)
    {
        CompressedData = compressedData;
        UncompressedLength = uncompressedLength;
    }
}

/// <summary>Contains the server campaign save and paired co-op session data.</summary>
public sealed class CollectedBugReportServerSave
{
    public string FileName { get; }
    public byte[] Data { get; }
    public string SidecarFileName { get; }
    public byte[] SidecarData { get; }

    public CollectedBugReportServerSave(
        string fileName,
        byte[] data,
        string sidecarFileName = null,
        byte[] sidecarData = null)
    {
        FileName = fileName;
        Data = data;
        SidecarFileName = sidecarFileName;
        SidecarData = sidecarData;
    }
}

/// <summary>Contains the logs and metadata written to a diagnostic archive.</summary>
public sealed class BugReportArchiveContents
{
    public string RequestId { get; }
    public string ReportingClientNetworkId { get; }
    public IReadOnlyCollection<string> Triggers { get; }
    public IReadOnlyCollection<BugReportSubmission> Submissions { get; }
    public CollectedBugReportServerLog ServerLog { get; }
    public CollectedBugReportServerSave ServerSave { get; }
    public DateTimeOffset StartedAt { get; }
    public IReadOnlyCollection<CollectedBugReportLog> Logs { get; }
    public int ExpectedClients { get; }
    public int DeclinedClients { get; }
    public int FailedClients { get; }
    public int TimedOutClients { get; }

    public BugReportArchiveContents(
        string requestId,
        string reportingClientNetworkId,
        IReadOnlyCollection<string> triggers,
        IReadOnlyCollection<BugReportSubmission> submissions,
        CollectedBugReportServerLog serverLog,
        CollectedBugReportServerSave serverSave,
        DateTimeOffset startedAt,
        IReadOnlyCollection<CollectedBugReportLog> logs,
        int expectedClients,
        int declinedClients,
        int failedClients,
        int timedOutClients)
    {
        RequestId = requestId;
        ReportingClientNetworkId = reportingClientNetworkId;
        Triggers = triggers ?? Array.Empty<string>();
        Submissions = submissions ?? Array.Empty<BugReportSubmission>();
        ServerLog = serverLog;
        ServerSave = serverSave;
        StartedAt = startedAt;
        Logs = logs ?? Array.Empty<CollectedBugReportLog>();
        ExpectedClients = expectedClients;
        DeclinedClients = declinedClients;
        FailedClients = failedClients;
        TimedOutClients = timedOutClients;
    }

    public BugReportArchiveContents WithServerLog(CollectedBugReportServerLog serverLog)
    {
        return new BugReportArchiveContents(
            RequestId,
            ReportingClientNetworkId,
            Triggers,
            Submissions,
            serverLog,
            ServerSave,
            StartedAt,
            Logs,
            ExpectedClients,
            DeclinedClients,
            FailedClients,
            TimedOutClients);
    }

    public BugReportArchiveContents WithValidatedLogs(
        IReadOnlyCollection<CollectedBugReportLog> logs,
        int invalidCount)
    {
        return new BugReportArchiveContents(
            RequestId,
            ReportingClientNetworkId,
            Triggers,
            Submissions,
            ServerLog,
            ServerSave,
            StartedAt,
            logs,
            ExpectedClients,
            DeclinedClients,
            FailedClients + invalidCount,
            TimedOutClients);
    }
}

/// <summary>Creates and retains server-side diagnostic report archives.</summary>
public interface IBugReportArchiveBuilder : IDisposable
{
    string Create(BugReportArchiveContents contents);
}

/// <inheritdoc />
public class BugReportArchiveBuilder : IBugReportArchiveBuilder
{
    private const int MaximumPendingArchiveCount = 20;
    private const long MaximumPendingArchiveBytes = 256L * 1024 * 1024;
    private static readonly TimeSpan PendingArchiveRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private readonly string outputDirectory;
    private readonly int maximumPendingArchiveCount;
    private readonly long maximumPendingArchiveBytes;
    private readonly TimeSpan pendingArchiveRetention;
    private readonly Func<string, bool> deleteArchive;
    private readonly object archiveGate = new object();
    private readonly Timer cleanupTimer;
    private int disposed;

    public BugReportArchiveBuilder() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Mount and Blade II Bannerlord",
        "Coop Bug Reports",
        "Pending"))
    {
    }

    internal BugReportArchiveBuilder(
        string outputDirectory,
        int maximumPendingArchiveCount = MaximumPendingArchiveCount,
        long maximumPendingArchiveBytes = MaximumPendingArchiveBytes,
        TimeSpan? pendingArchiveRetention = null,
        TimeSpan? cleanupInterval = null,
        Func<string, bool> deleteArchive = null)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
        if (maximumPendingArchiveCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumPendingArchiveCount));
        if (maximumPendingArchiveBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumPendingArchiveBytes));
        if (pendingArchiveRetention.HasValue && pendingArchiveRetention.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(pendingArchiveRetention));
        if (cleanupInterval.HasValue && cleanupInterval.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cleanupInterval));

        this.outputDirectory = Path.GetFullPath(outputDirectory);
        this.maximumPendingArchiveCount = maximumPendingArchiveCount;
        this.maximumPendingArchiveBytes = maximumPendingArchiveBytes;
        this.pendingArchiveRetention = pendingArchiveRetention ?? PendingArchiveRetention;
        this.deleteArchive = deleteArchive ?? DeleteArchive;
        RunCleanup();
        var interval = cleanupInterval ?? CleanupInterval;
        cleanupTimer = new Timer(_ => RunCleanup(), null, interval, interval);
    }

    public string Create(BugReportArchiveContents contents)
    {
        lock (archiveGate)
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(nameof(BugReportArchiveBuilder));
            return CreateCore(contents);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        cleanupTimer.Dispose();
    }

    private string CreateCore(BugReportArchiveContents contents)
    {
        if (contents == null) throw new ArgumentNullException(nameof(contents));
        if (!Guid.TryParseExact(contents.RequestId, "N", out _))
            throw new ArgumentException("Request id must be a compact GUID.", nameof(contents));

        Directory.CreateDirectory(outputDirectory);
        PruneExpiredArchives();

        var archivePath = Path.Combine(outputDirectory, "bug_report_" + contents.RequestId + ".zip");
        try
        {
            using (var stream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteManifest(archive, contents);
                AddSubmissions(archive, contents.Submissions);
                if (contents.ServerLog != null) AddServerLog(archive, contents.ServerLog);
                if (contents.ServerSave != null) AddServerSave(archive, contents.ServerSave);
                foreach (var log in contents.Logs.OrderBy(item => item.ClientNumber))
                {
                    AddLog(archive, log);
                }
            }

            EnforcePendingQuota(archivePath);
            return archivePath;
        }
        catch
        {
            TryDeleteArchive(archivePath);
            throw;
        }
    }

    private bool TryDeleteArchive(string path)
    {
        try
        {
            return deleteArchive(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool DeleteArchive(string path)
    {
        File.Delete(path);
        return true;
    }

    private void RunCleanup()
    {
        if (Volatile.Read(ref disposed) != 0) return;

        lock (archiveGate)
        {
            if (Volatile.Read(ref disposed) != 0) return;
            PruneExpiredArchives();
        }
    }

    private void PruneExpiredArchives()
    {
        string[] paths;
        try
        {
            if (!Directory.Exists(outputDirectory)) return;
            paths = Directory.GetFiles(outputDirectory, "bug_report_*.zip");
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var cutoff = DateTime.UtcNow - pendingArchiveRetention;
        foreach (var path in paths)
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff) TryDeleteArchive(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void EnforcePendingQuota(string newArchivePath)
    {
        var archives = Directory
            .EnumerateFiles(outputDirectory, "bug_report_*.zip")
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.Name, StringComparer.Ordinal)
            .ToList();
        var newArchive = archives.First(file =>
            string.Equals(file.FullName, newArchivePath, StringComparison.OrdinalIgnoreCase));
        if (newArchive.Length > maximumPendingArchiveBytes)
            throw new InvalidDataException("The diagnostic archive exceeds the pending storage quota.");

        var totalBytes = archives.Sum(file => file.Length);
        var evictionCandidates = archives
            .Where(file => !ReferenceEquals(file, newArchive))
            .ToList();
        foreach (var archive in evictionCandidates)
        {
            if (archives.Count <= maximumPendingArchiveCount &&
                totalBytes <= maximumPendingArchiveBytes)
            {
                break;
            }

            var length = archive.Length;
            if (!TryDeleteArchive(archive.FullName)) continue;

            totalBytes -= length;
            archives.Remove(archive);
        }

        if (archives.Count > maximumPendingArchiveCount || totalBytes > maximumPendingArchiveBytes)
            throw new InvalidDataException("The pending diagnostic archive quota could not be enforced.");
    }

    private static void WriteManifest(ZipArchive archive, BugReportArchiveContents contents)
    {
        var entry = archive.CreateEntry("manifest.txt", CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.NewLine = "\n";
        writer.WriteLine("BannerlordCoop diagnostic bug report");
        writer.WriteLine("Request id: " + contents.RequestId);
        writer.WriteLine("Reporting client network id: " + contents.ReportingClientNetworkId);
        writer.WriteLine("Module version: " + ModInformation.Version);
        writer.WriteLine("Commit: " + ModInformation.Commit);
        writer.WriteLine("Build version: " + ModInformation.BuildVersion);
        writer.WriteLine("Triggers: " + string.Join(", ", contents.Triggers));
        writer.WriteLine("Started: " + contents.StartedAt.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteLine("Packaged: " + DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteLine("Player submissions: " + contents.Submissions.Count.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("Server save included: " + (contents.ServerSave != null ? "yes" : "no"));
        writer.WriteLine("Server save sidecar included: " +
                         (contents.ServerSave?.SidecarData != null ? "yes" : "no"));
        writer.WriteLine("Expected clients: " + contents.ExpectedClients.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("Logs included: " + contents.Logs.Count.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("Clients declined: " + contents.DeclinedClients.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("Clients failed: " + contents.FailedClients.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("Clients timed out: " + contents.TimedOutClients.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("The current server campaign save, its co-op session data, and current BannerlordCoop logs are included; client saves, configs, and dumps are excluded.");
    }

    private static void AddSubmissions(
        ZipArchive archive,
        IReadOnlyCollection<BugReportSubmission> submissions)
    {
        var index = 1;
        foreach (var submission in submissions)
        {
            if (submission == null) continue;

            var entry = archive.CreateEntry(
                "reports/report-" + index.ToString("D2", CultureInfo.InvariantCulture) + ".txt",
                CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.NewLine = "\n";
            writer.WriteLine("Reporting client network id: " + submission.ReportingClientNetworkId);
            writer.WriteLine("Summary: " + submission.Summary);
            writer.WriteLine();
            writer.WriteLine("Description:");
            writer.Write(submission.Description);
            index++;
        }
    }

    private static void AddServerSave(ZipArchive archive, CollectedBugReportServerSave save)
    {
        if (save.Data == null || save.Data.Length == 0)
            throw new InvalidDataException("The collected server save was empty.");
        if (!IsValidFileName(save.FileName))
            throw new InvalidDataException("The collected server save had an invalid file name.");

        var entry = archive.CreateEntry("server/" + save.FileName, CompressionLevel.NoCompression);
        using (var destination = entry.Open())
        {
            destination.Write(save.Data, 0, save.Data.Length);
        }

        if (save.SidecarFileName == null && save.SidecarData == null) return;
        if (!IsValidFileName(save.SidecarFileName) ||
            !string.Equals(
                Path.ChangeExtension(save.FileName, ".json"),
                save.SidecarFileName,
                StringComparison.OrdinalIgnoreCase) ||
            save.SidecarData == null || save.SidecarData.Length == 0)
        {
            throw new InvalidDataException("The collected server save sidecar was invalid.");
        }

        var sidecarEntry = archive.CreateEntry(
            "server/" + save.SidecarFileName,
            CompressionLevel.Optimal);
        using var sidecarDestination = sidecarEntry.Open();
        sidecarDestination.Write(save.SidecarData, 0, save.SidecarData.Length);
    }

    private static bool IsValidFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) &&
               string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal);
    }

    private static void AddServerLog(ZipArchive archive, CollectedBugReportServerLog log)
    {
        if (log.CompressedData == null)
            throw new InvalidDataException("The collected server log was empty.");

        AddCompressedLog(
            archive,
            "server/server.log",
            log.CompressedData,
            log.UncompressedLength,
            "server");
    }

    private static void AddLog(ZipArchive archive, CollectedBugReportLog log)
    {
        if (log == null || log.CompressedData == null)
            throw new InvalidDataException("A collected client log was empty.");

        AddCompressedLog(
            archive,
            "clients/client-" + log.ClientNumber.ToString("D2", CultureInfo.InvariantCulture) + ".log",
            log.CompressedData,
            log.UncompressedLength,
            "client");
    }

    private static void AddCompressedLog(
        ZipArchive archive,
        string entryName,
        byte[] compressedData,
        int uncompressedLength,
        string sourceName)
    {
        if (uncompressedLength < 0 || uncompressedLength > CoopLogSnapshotProvider.MaximumLogBytes)
            throw new InvalidDataException($"The collected {sourceName} log declared an invalid length.");

        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var source = new MemoryStream(compressedData, writable: false);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var destination = entry.Open();

        var buffer = new byte[81920];
        var total = 0;
        int read;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > uncompressedLength || total > CoopLogSnapshotProvider.MaximumLogBytes)
                throw new InvalidDataException($"The collected {sourceName} log exceeded its declared length.");

            destination.Write(buffer, 0, read);
        }

        if (total != uncompressedLength)
            throw new InvalidDataException($"The collected {sourceName} log did not match its declared length.");
    }
}
