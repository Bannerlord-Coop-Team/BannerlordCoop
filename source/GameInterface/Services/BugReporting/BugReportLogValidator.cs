using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace GameInterface.Services.BugReporting;

/// <summary>Validates collected client logs before they are archived or uploaded.</summary>
public interface IBugReportLogValidator
{
    BugReportLogValidationResult Validate(IReadOnlyCollection<CollectedBugReportLog> logs);
}

/// <inheritdoc />
public class BugReportLogValidator : IBugReportLogValidator
{
    public BugReportLogValidationResult Validate(IReadOnlyCollection<CollectedBugReportLog> logs)
    {
        if (logs == null) throw new ArgumentNullException(nameof(logs));

        var validLogs = new List<CollectedBugReportLog>();
        var invalidCount = 0;
        foreach (var log in logs)
        {
            if (IsValid(log))
                validLogs.Add(log);
            else
                invalidCount++;
        }

        return new BugReportLogValidationResult(validLogs, invalidCount);
    }

    private static bool IsValid(CollectedBugReportLog log)
    {
        if (log?.CompressedData == null || log.CompressedData.Length == 0 ||
            log.UncompressedLength < 0 ||
            log.UncompressedLength > CoopLogSnapshotProvider.MaximumLogBytes)
        {
            return false;
        }

        try
        {
            using var input = new MemoryStream(log.CompressedData, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            var buffer = new byte[81920];
            var length = 0;
            int read;
            while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                length += read;
                if (length > log.UncompressedLength ||
                    length > CoopLogSnapshotProvider.MaximumLogBytes)
                {
                    return false;
                }
            }

            return length == log.UncompressedLength;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}

/// <summary>Contains valid client logs and the number rejected during validation.</summary>
public sealed class BugReportLogValidationResult
{
    public IReadOnlyCollection<CollectedBugReportLog> ValidLogs { get; }
    public int InvalidCount { get; }

    public BugReportLogValidationResult(
        IReadOnlyCollection<CollectedBugReportLog> validLogs,
        int invalidCount)
    {
        ValidLogs = validLogs;
        InvalidCount = invalidCount;
    }
}
