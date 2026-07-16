using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Display;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Common.Logging;

/// <summary>
/// Writes a bounded text log while preserving its beginning and newest entries.
/// </summary>
public sealed class SizeLimitedFileSink : ILogEventSink, IDisposable
{
    private const int MarkerReserveBytes = 1024;
    private const string TruncationMarkerPrefix = "[LOG TRUNCATED ";

    private readonly object syncRoot = new object();
    private readonly string filePath;
    private readonly long maxFileSizeBytes;
    private readonly long preservedBeginningBytes;
    private readonly long compactionTargetBytes;
    private readonly MessageTemplateTextFormatter formatter;
    private readonly Encoding encoding = new UTF8Encoding(false);
    private readonly FileStream stream;

    private int preservedBeginningLength = -1;
    private int markerLength;
    private long truncationCount;
    private long removedBytes;
    private bool disposed;

    public SizeLimitedFileSink(
        string filePath,
        string outputTemplate,
        long maxFileSizeBytes,
        long preservedBeginningBytes,
        long compactionTargetBytes)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (outputTemplate == null) throw new ArgumentNullException(nameof(outputTemplate));
        if (maxFileSizeBytes <= MarkerReserveBytes || maxFileSizeBytes > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes));
        if (preservedBeginningBytes < 0 || preservedBeginningBytes + MarkerReserveBytes >= compactionTargetBytes)
            throw new ArgumentOutOfRangeException(nameof(preservedBeginningBytes));
        if (compactionTargetBytes >= maxFileSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(compactionTargetBytes));

        this.filePath = filePath;
        this.maxFileSizeBytes = maxFileSizeBytes;
        this.preservedBeginningBytes = preservedBeginningBytes;
        this.compactionTargetBytes = compactionTargetBytes;
        formatter = new MessageTemplateTextFormatter(outputTemplate, CultureInfo.InvariantCulture);

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        stream.Position = stream.Length;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

        try
        {
            using (var writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                formatter.Format(logEvent, writer);
                Write(encoding.GetBytes(writer.ToString()));
            }
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("Unable to write log event to {0}: {1}", filePath, ex);
        }
    }

    private void Write(byte[] eventBytes)
    {
        lock (syncRoot)
        {
            if (disposed) return;

            var beginningReserve = preservedBeginningLength >= 0
                ? preservedBeginningLength
                : Math.Min(preservedBeginningBytes, stream.Length);
            var maximumEventBytes = maxFileSizeBytes - beginningReserve - MarkerReserveBytes;
            var eventBytesRemoved = 0L;

            if (eventBytes.LongLength > maximumEventBytes)
            {
                var originalLength = eventBytes.LongLength;
                eventBytes = KeepUtf8Suffix(eventBytes, (int)maximumEventBytes);
                eventBytesRemoved = originalLength - eventBytes.LongLength;
            }

            if (stream.Length + eventBytes.LongLength > maxFileSizeBytes || eventBytesRemoved > 0)
                Compact(eventBytes.Length, eventBytesRemoved);

            stream.Position = stream.Length;
            stream.Write(eventBytes, 0, eventBytes.Length);
            stream.Flush();
        }
    }

    private void Compact(int appendedBytes, long eventBytesRemoved)
    {
        var content = ReadContent();
        if (preservedBeginningLength < 0)
            preservedBeginningLength = FindPreservedBeginningLength(content);

        var contentStart = preservedBeginningLength + markerLength;
        var targetBeforeAppend = Math.Min(compactionTargetBytes, maxFileSizeBytes - appendedBytes);
        var tailBudget = Math.Max(0, targetBeforeAppend - preservedBeginningLength - MarkerReserveBytes);
        var tailStartCandidate = (int)Math.Max(contentStart, content.LongLength - tailBudget);
        var tailStart = FindNextLineStart(content, tailStartCandidate, contentStart);

        truncationCount++;
        removedBytes += tailStart - contentStart + eventBytesRemoved;

        var marker = CreateMarker();
        if (marker.Length > MarkerReserveBytes)
            throw new InvalidOperationException("The log truncation marker exceeded its reserved space.");

        stream.Position = 0;
        stream.Write(content, 0, preservedBeginningLength);
        stream.Write(marker, 0, marker.Length);
        stream.Write(content, tailStart, content.Length - tailStart);
        stream.SetLength(stream.Position);
        stream.Flush();

        markerLength = marker.Length;
    }

    private byte[] ReadContent()
    {
        stream.Flush();
        stream.Position = 0;

        if (stream.Length > int.MaxValue)
            throw new IOException("The log file is too large to compact.");

        var content = new byte[(int)stream.Length];
        var offset = 0;
        while (offset < content.Length)
        {
            var read = stream.Read(content, offset, content.Length - offset);
            if (read == 0) break;
            offset += read;
        }

        if (offset != content.Length)
            throw new EndOfStreamException("The log file changed while it was being compacted.");

        return content;
    }

    private int FindPreservedBeginningLength(byte[] content)
    {
        var candidate = (int)Math.Min(preservedBeginningBytes, content.LongLength);
        if (candidate == content.Length) return candidate;

        for (var index = candidate - 1; index >= 0; index--)
        {
            if (content[index] == (byte)'\n') return index + 1;
        }

        return 0;
    }

    private static int FindNextLineStart(byte[] content, int candidate, int minimum)
    {
        if (candidate <= minimum) return minimum;
        if (candidate > 0 && content[candidate - 1] == (byte)'\n') return candidate;

        while (candidate < content.Length && content[candidate] != (byte)'\n')
            candidate++;

        return candidate < content.Length ? candidate + 1 : candidate;
    }

    private byte[] CreateMarker()
    {
        var marker = string.Format(
            CultureInfo.InvariantCulture,
            "{0}{1:O}] Middle truncation count: {2}; removed {3} bytes cumulatively. Startup and newest entries were preserved to enforce the {4}-byte limit.{5}",
            TruncationMarkerPrefix,
            DateTime.UtcNow,
            truncationCount,
            removedBytes,
            maxFileSizeBytes,
            Environment.NewLine);
        return encoding.GetBytes(marker);
    }

    private static byte[] KeepUtf8Suffix(byte[] content, int maximumBytes)
    {
        var start = content.Length - maximumBytes;
        while (start < content.Length && (content[start] & 0xC0) == 0x80)
            start++;

        var suffix = new byte[content.Length - start];
        Buffer.BlockCopy(content, start, suffix, 0, suffix.Length);
        return suffix;
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (disposed) return;

            stream.Flush();
            stream.Dispose();
            disposed = true;
        }
    }
}
