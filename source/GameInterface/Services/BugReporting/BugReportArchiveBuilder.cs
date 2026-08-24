using Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

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

/// <summary>Contains the logs and metadata written to a diagnostic archive.</summary>
public sealed class BugReportArchiveContents
{
    public string RequestId { get; }
    public string ReportingClientNetworkId { get; }
    public IReadOnlyCollection<string> Triggers { get; }
    public IReadOnlyCollection<BugReportSubmission> Submissions { get; }
    public CollectedBugReportServerLog ServerLog { get; }
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
            StartedAt,
            Logs,
            ExpectedClients,
            DeclinedClients,
            FailedClients,
            TimedOutClients);
    }
}

/// <summary>Creates and retains server-side diagnostic report archives.</summary>
public interface IBugReportArchiveBuilder
{
    string Create(BugReportArchiveContents contents);
}

/// <inheritdoc />
public class BugReportArchiveBuilder : IBugReportArchiveBuilder
{
    private static readonly TimeSpan PendingArchiveRetention = TimeSpan.FromDays(7);
    private readonly string outputDirectory;

    public BugReportArchiveBuilder() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Mount and Blade II Bannerlord",
        "Coop Bug Reports",
        "Pending"))
    {
    }

    internal BugReportArchiveBuilder(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));

        this.outputDirectory = Path.GetFullPath(outputDirectory);
    }

    public string Create(BugReportArchiveContents contents)
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
                foreach (var log in contents.Logs.OrderBy(item => item.ClientNumber))
                {
                    AddLog(archive, log);
                }
            }

            return archivePath;
        }
        catch
        {
            TryDelete(archivePath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void PruneExpiredArchives()
    {
        try
        {
            var cutoff = DateTime.UtcNow - PendingArchiveRetention;
            foreach (var path in Directory.EnumerateFiles(outputDirectory, "bug_report_*.zip"))
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff) File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
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
        writer.WriteLine("Expected clients: " + contents.ExpectedClients.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("Logs included: " + contents.Logs.Count.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("Clients declined: " + contents.DeclinedClients.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("Clients failed: " + contents.FailedClients.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("Clients timed out: " + contents.TimedOutClients.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("Only current BannerlordCoop server and client logs are included; saves, configs, and dumps are excluded.");
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
