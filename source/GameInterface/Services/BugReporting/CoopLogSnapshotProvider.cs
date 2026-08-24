using Common.Logging;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace GameInterface.Services.BugReporting;

/// <summary>Captures a bounded and redacted snapshot of the active co-op log.</summary>
public interface ICoopLogSnapshotProvider
{
    bool TryCapture(out CoopLogSnapshot snapshot);
}

/// <summary>Contains a compressed client log snapshot and its original size.</summary>
public sealed class CoopLogSnapshot
{
    public byte[] CompressedData { get; }
    public int UncompressedLength { get; }

    public CoopLogSnapshot(byte[] compressedData, int uncompressedLength)
    {
        CompressedData = compressedData;
        UncompressedLength = uncompressedLength;
    }
}

/// <inheritdoc />
public class CoopLogSnapshotProvider : ICoopLogSnapshotProvider
{
    internal const int MaximumLogBytes = 31 * 1024 * 1024;
    private const string RuntimeArgumentsMarker = "#TW#Runtime#TW#Arguments#TW#";
    private const string CommandArgumentsMarker = "Command Args:";

    private static readonly Regex SecretQueryValue = new Regex(
        @"(?i)([?&](?:access_token|token|password|secret|api_key)=)[^&\s]+",
        RegexOptions.CultureInvariant);

    private static readonly Regex AuthorizationValue = new Regex(
        @"(?i)(authorization\s*[:=]\s*(?:bearer\s+)?)[^\s,;]+",
        RegexOptions.CultureInvariant);

    private readonly ICoopLogFile coopLogFile;

    public CoopLogSnapshotProvider(ICoopLogFile coopLogFile)
    {
        if (coopLogFile == null) throw new ArgumentNullException(nameof(coopLogFile));
        this.coopLogFile = coopLogFile;
    }

    public bool TryCapture(out CoopLogSnapshot snapshot)
    {
        snapshot = null;
        if (string.IsNullOrEmpty(coopLogFile.Path) || !File.Exists(coopLogFile.Path)) return false;

        try
        {
            using var compressed = new MemoryStream();
            var uncompressedLength = 0;

            using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
            using (var source = new FileStream(
                coopLogFile.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(source, Encoding.UTF8, true))
            {
                writer.NewLine = "\n";
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = RedactLine(line);
                    var lineBytes = Encoding.UTF8.GetByteCount(line) + 1;
                    if (uncompressedLength + lineBytes > MaximumLogBytes) return false;

                    writer.WriteLine(line);
                    uncompressedLength += lineBytes;
                }
            }

            snapshot = new CoopLogSnapshot(compressed.ToArray(), uncompressedLength);
            return true;
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

    internal static string RedactLine(string line)
    {
        line = RedactAfterMarker(line, RuntimeArgumentsMarker);
        line = RedactAfterMarker(line, CommandArgumentsMarker);
        line = SecretQueryValue.Replace(line, "$1[redacted]");
        line = AuthorizationValue.Replace(line, "$1[redacted]");

        var userName = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(userName) && userName.Length > 1)
        {
            line = Regex.Replace(
                line,
                @"(?<=[\\/])" + Regex.Escape(userName) + @"(?=[\\/])",
                "[user]",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return line;
    }

    private static string RedactAfterMarker(string line, string marker)
    {
        var markerIndex = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0) return line;

        return line.Substring(0, markerIndex + marker.Length) + " [redacted]";
    }
}
