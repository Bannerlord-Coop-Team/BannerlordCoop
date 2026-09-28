using Common;
using Common.Logging;
using Coop.Core.Server.Connections;
using Coop.Tests.Mocks;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Connections;

public class JoinValidationDenialLogTests : IDisposable
{
    private const int NullPeerCount = 7331;

    private readonly ConcurrentQueue<string> logs = new ConcurrentQueue<string>();
    private readonly Action<string> captureLog;
    private readonly JoinValidationDenialLog denialLog = new JoinValidationDenialLog();
    private readonly TestNetwork network = new TestNetwork();

    public JoinValidationDenialLogTests()
    {
        captureLog = logs.Enqueue;
        OutputSinkManager.AddLogCallback(captureLog);
    }

    public void Dispose() => OutputSinkManager.RemoveLogCallback(captureLog);

    private static string ServerBuild => JoinValidationDenialLog.SanitizeBuild(ModInformation.BuildVersion);

    [Fact]
    public void Report_LogsOneLineWithEveryField()
    {
        NetPeer peer = network.CreatePeer("10.0.0.7");

        denialLog.Report(peer, JoinDenialKind.BuildMismatch, "9.9.9+deadbeef", "Incompatible co-op mod build.");

        Assert.Equal(
            $"Join validation denied for peer {peer.Id} (\"10.0.0.7:5555\"): BuildMismatch; client build \"9.9.9+deadbeef\", server build \"{ServerBuild}\"; \"Incompatible co-op mod build.\"",
            Assert.Single(Denials(peer)));
    }

    [Fact]
    public void ReportRepeats_LogsOneLineWithTheCount()
    {
        NetPeer peer = network.CreatePeer("10.0.0.7");

        denialLog.ReportRepeats(peer, 3);

        Assert.Equal(
            $"Join validation denied 3 times for peer {peer.Id} (\"10.0.0.7:5555\") on one connection",
            Assert.Single(Denials(peer)));
    }

    [Fact]
    public void Report_TwoPeers_GiveTwoLines()
    {
        NetPeer first = network.CreatePeer();
        NetPeer second = network.CreatePeer();

        denialLog.Report(first, JoinDenialKind.BuildMismatch, "1.0.0+aaaa", "reason");
        denialLog.Report(second, JoinDenialKind.BuildMismatch, "2.0.0+bbbb", "reason");

        Assert.Collection(
            Denials(first, second),
            line => Assert.StartsWith($"Join validation denied for peer {first.Id} (\"127.0.0.1:5555\"): BuildMismatch; client build \"1.0.0+aaaa\",", line),
            line => Assert.StartsWith($"Join validation denied for peer {second.Id} (\"127.0.0.1:5555\"): BuildMismatch; client build \"2.0.0+bbbb\",", line));
    }

    [Theory]
    [InlineData(null, "<none>")]
    [InlineData("", "<none>")]
    [InlineData("0.1.5+4ad976c8", "0.1.5+4ad976c8")]
    [InlineData("1.0_rc-2", "1.0_rc-2")]
    [InlineData("1.0\r\n[INF] SERVING", "1.0???INF??SERVING")]
    [InlineData("1.0\u001b[31m", "1.0??31m")]
    [InlineData("1.0 \u202E\u00E9", "1.0???")]
    public void SanitizeBuild_KeepsOnlyBuildCharacters(string? build, string expected)
    {
        Assert.Equal(expected, JoinValidationDenialLog.SanitizeBuild(build));
    }

    [Theory]
    [InlineData(null, "<none>")]
    [InlineData("", "<none>")]
    [InlineData("\r\n\u001b", "<none>")]
    [InlineData("Wrong game version detected.", "Wrong game version detected.")]
    [InlineData("first\r\nsecond\nthird\rfourth", "first | second | third | fourth")]
    [InlineData("\nfirst\n\n\u001b\nsecond\n", "first | second")]
    [InlineData("a\u2028b\u2029c\u0085d", "a | b | c | d")]
    [InlineData("red\u001b[31m text\u0007", "red[31m text")]
    [InlineData("module 'abc\u202Edef'", "module 'abcdef'")]
    public void SanitizeReason_KeepsOneLine(string? reason, string expected)
    {
        Assert.Equal(expected, JoinValidationDenialLog.SanitizeReason(reason));
    }

    [Fact]
    public void Report_CoopPlusAnotherModule_KeepsTheRealModuleVisible()
    {
        NetPeer peer = network.CreatePeer();
        string reason = string.Join(Environment.NewLine,
            "Server does not support module 'Coop'.",
            "Server does not support module 'WarSails'.");

        denialLog.Report(peer, JoinDenialKind.ModuleValidation, "build", reason);

        Assert.EndsWith(
            "): ModuleValidation; client build \"build\", server build \"" + ServerBuild + "\"; \"Server does not support module 'Coop'. | Server does not support module 'WarSails'.\"",
            Assert.Single(Denials(peer)));
    }

    [Fact]
    public void Report_ForgedLogLine_StaysOnOneRenderedLine()
    {
        const string forged = "\r\n[(4242) 12:00:00 INF Coop.Core.Server.CoopServer] SERVING";
        NetPeer peer = network.CreatePeer();

        denialLog.Report(peer, JoinDenialKind.BuildMismatch, "1.0" + forged, "reason" + forged);

        string rendered = Assert.Single(Denials(peer));
        Assert.DoesNotContain("\r", rendered);
        Assert.DoesNotContain("\n", rendered);
        Assert.Contains("reason | [(4242) 12:00:00 INF Coop.Core.Server.CoopServer] SERVING", rendered);
    }

    [Fact]
    public void Report_HugeStrings_AreCapped()
    {
        string huge = new string('a', 10 * 1024);
        NetPeer peer = network.CreatePeer();

        denialLog.Report(peer, JoinDenialKind.BuildMismatch, huge, huge);

        string line = Assert.Single(Denials(peer));
        Assert.Contains($"; client build \"{new string('a', 96)}...(+10144)\", server build ", line);
        Assert.EndsWith($"; \"{new string('a', 400)}...(+9840)\"", line);
    }

    [Fact]
    public void SanitizeReason_CapDoesNotSplitASurrogatePair()
    {
        string reason = new string('a', JoinValidationDenialLog.MaxReasonLength - 1) + "\U0001F600" + "tail";

        string sanitized = JoinValidationDenialLog.SanitizeReason(reason);

        Assert.Equal(new string('a', JoinValidationDenialLog.MaxReasonLength - 1) + "...(+6)", sanitized);
    }

    [Fact]
    public void NullPeer_Throws()
    {
        string marker = Guid.NewGuid().ToString("N");

        Assert.Throws<ArgumentNullException>(() =>
            denialLog.Report(null!, JoinDenialKind.BuildMismatch, marker, marker));
        Assert.Throws<ArgumentNullException>(() => denialLog.ReportRepeats(null!, NullPeerCount));
        Assert.DoesNotContain(logs, line =>
            line.Contains(marker) || line.StartsWith($"Join validation denied {NullPeerCount} times", StringComparison.Ordinal));
    }

    // Log callbacks are shared with tests running in parallel, so lines are matched by peer id
    private string[] Denials(params NetPeer[] peers)
    {
        string[] markers = peers.Select(peer => $" for peer {peer.Id} (").ToArray();
        return logs
            .Where(line => line.StartsWith("Join validation denied ", StringComparison.Ordinal) &&
                           markers.Any(marker => line.Contains(marker)))
            .ToArray();
    }
}
