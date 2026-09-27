using Common;
using Common.Logging;
using LiteNetLib;
using Serilog;
using System;
using System.Globalization;
using System.Net;
using System.Text;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Writes one sanitized server log line when a join is refused while validating the client's
/// co-op build and modules, so the operator can see why a player could not join.
/// </summary>
public interface IJoinValidationDenialLog
{
    /// <summary>
    /// Logs one refused join.
    /// </summary>
    void Report(NetPeer peer, JoinDenialKind kind, string? clientBuild, string? reason);

    /// <summary>
    /// Logs how many times one connection was refused.
    /// </summary>
    void ReportRepeats(NetPeer peer, int count);
}

/// <inheritdoc cref="IJoinValidationDenialLog"/>
public class JoinValidationDenialLog : IJoinValidationDenialLog
{
    internal const int MaxBuildLength = 96;
    internal const int MaxReasonLength = 400;
    internal const string Missing = "<none>";

    private const string LineSeparator = " | ";

    private static readonly ILogger DefaultLogger = LogManager.GetLogger<JoinValidationDenialLog>();

    private readonly ILogger logger;

    public JoinValidationDenialLog() : this(DefaultLogger)
    {
    }

    internal JoinValidationDenialLog(ILogger logger)
    {
        this.logger = logger;
    }

    public void Report(NetPeer peer, JoinDenialKind kind, string? clientBuild, string? reason)
    {
        if (peer == null) throw new ArgumentNullException(nameof(peer));

        // The build and reason come from the client, and the file template writes strings as they are.
        logger.Warning(
            "Join validation denied for peer {PeerId} ({Endpoint}): {Kind}; client build {ClientBuild}, server build {ServerBuild}; {Reason}",
            peer.Id,
            EndpointOf(peer),
            kind,
            SanitizeBuild(clientBuild),
            SanitizeBuild(ModInformation.BuildVersion),
            SanitizeReason(reason));
    }

    public void ReportRepeats(NetPeer peer, int count)
    {
        if (peer == null) throw new ArgumentNullException(nameof(peer));

        logger.Warning(
            "Join validation denied {Count} times for peer {PeerId} ({Endpoint}) on one connection",
            count,
            peer.Id,
            EndpointOf(peer));
    }

    internal static string SanitizeBuild(string? build)
    {
        if (string.IsNullOrEmpty(build)) return Missing;

        var text = new StringBuilder(build.Length);
        foreach (char c in build)
        {
            text.Append(IsBuildChar(c) ? c : '?');
        }

        return Cap(text, MaxBuildLength);
    }

    internal static string SanitizeReason(string? reason)
    {
        if (string.IsNullOrEmpty(reason)) return Missing;

        var text = new StringBuilder(reason.Length);
        bool pendingSeparator = false;
        foreach (char c in reason)
        {
            // Line breaks become a visible separator, since a module reason has one line per module.
            if (IsLineBreak(c))
            {
                pendingSeparator = true;
                continue;
            }

            if (char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format) continue;

            if (pendingSeparator && text.Length > 0) text.Append(LineSeparator);
            pendingSeparator = false;
            text.Append(c);
        }

        return text.Length == 0 ? Missing : Cap(text, MaxReasonLength);
    }

    private static string Cap(StringBuilder text, int maxLength)
    {
        if (text.Length <= maxLength) return text.ToString();

        int kept = char.IsHighSurrogate(text[maxLength - 1]) ? maxLength - 1 : maxLength;
        return text.ToString(0, kept) + $"...(+{text.Length - kept})";
    }

    private static bool IsBuildChar(char c)
    {
        return (c >= '0' && c <= '9') ||
               (c >= 'A' && c <= 'Z') ||
               (c >= 'a' && c <= 'z') ||
               c == '.' || c == '+' || c == '_' || c == '-';
    }

    private static bool IsLineBreak(char c)
    {
        return c == '\r' || c == '\n' || c == '\u0085' || c == '\u2028' || c == '\u2029';
    }

    private static string EndpointOf(NetPeer peer) => new IPEndPoint(peer.Address, peer.Port).ToString();
}

/// <summary>
/// Why a join was refused during module validation.
/// </summary>
public enum JoinDenialKind
{
    BuildMismatch,
    ModuleValidation,
}
