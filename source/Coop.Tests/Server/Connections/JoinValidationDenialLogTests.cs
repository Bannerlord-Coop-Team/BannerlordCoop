using Common;
using Coop.Core.Server.Connections;
using Coop.Tests.Mocks;
using LiteNetLib;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Connections;

public class JoinValidationDenialLogTests
{
    private readonly CaptureSink sink = new CaptureSink();
    private readonly JoinValidationDenialLog denialLog;
    private readonly TestNetwork network = new TestNetwork();

    public JoinValidationDenialLogTests()
    {
        ILogger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
        denialLog = new JoinValidationDenialLog(logger);
    }

    [Fact]
    public void Report_LogsOneWarningWithEveryField()
    {
        NetPeer peer = network.CreatePeer("10.0.0.7");

        denialLog.Report(peer, JoinDenialKind.BuildMismatch, "9.9.9+deadbeef", "Incompatible co-op mod build.");

        LogEvent line = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, line.Level);
        Assert.Equal(peer.Id, Scalar(line, "PeerId"));
        Assert.Equal("10.0.0.7:5555", Scalar(line, "Endpoint"));
        Assert.Equal(JoinDenialKind.BuildMismatch, Scalar(line, "Kind"));
        Assert.Equal("9.9.9+deadbeef", Scalar(line, "ClientBuild"));
        Assert.Equal(JoinValidationDenialLog.SanitizeBuild(ModInformation.BuildVersion), Scalar(line, "ServerBuild"));
        Assert.Equal("Incompatible co-op mod build.", Scalar(line, "Reason"));
    }

    [Fact]
    public void ReportRepeats_LogsOneWarningWithTheCount()
    {
        NetPeer peer = network.CreatePeer("10.0.0.7");

        denialLog.ReportRepeats(peer, 3);

        LogEvent line = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, line.Level);
        Assert.Equal(3, Scalar(line, "Count"));
        Assert.Equal(peer.Id, Scalar(line, "PeerId"));
        Assert.Equal("10.0.0.7:5555", Scalar(line, "Endpoint"));
    }

    [Fact]
    public void Report_TwoPeers_GiveTwoLines()
    {
        NetPeer first = network.CreatePeer();
        NetPeer second = network.CreatePeer();

        denialLog.Report(first, JoinDenialKind.BuildMismatch, "1.0.0+aaaa", "reason");
        denialLog.Report(second, JoinDenialKind.BuildMismatch, "2.0.0+bbbb", "reason");

        Assert.Equal(
            new object[] { "1.0.0+aaaa", "2.0.0+bbbb" },
            sink.Events.Select(line => Scalar(line, "ClientBuild")).ToArray());
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
        string reason = string.Join(Environment.NewLine,
            "Server does not support module 'Coop'.",
            "Server does not support module 'WarSails'.");

        denialLog.Report(network.CreatePeer(), JoinDenialKind.ModuleValidation, "build", reason);

        Assert.Equal(
            "Server does not support module 'Coop'. | Server does not support module 'WarSails'.",
            Scalar(Assert.Single(sink.Events), "Reason"));
    }

    [Fact]
    public void Report_ForgedLogLine_StaysOnOneRenderedLine()
    {
        const string forged = "\r\n[(4242) 12:00:00 INF Coop.Core.Server.CoopServer] SERVING";

        denialLog.Report(network.CreatePeer(), JoinDenialKind.BuildMismatch, "1.0" + forged, "reason" + forged);

        string rendered = Assert.Single(sink.Events).RenderMessage();
        Assert.DoesNotContain("\r", rendered);
        Assert.DoesNotContain("\n", rendered);
        Assert.Contains("reason | [(4242) 12:00:00 INF Coop.Core.Server.CoopServer] SERVING", rendered);
    }

    [Fact]
    public void Report_HugeStrings_AreCapped()
    {
        string huge = new string('a', 10 * 1024);

        denialLog.Report(network.CreatePeer(), JoinDenialKind.BuildMismatch, huge, huge);

        LogEvent line = Assert.Single(sink.Events);
        Assert.Equal(new string('a', 96) + "...(+10144)", Scalar(line, "ClientBuild"));
        Assert.Equal(new string('a', 400) + "...(+9840)", Scalar(line, "Reason"));
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
        Assert.Throws<ArgumentNullException>(() =>
            denialLog.Report(null!, JoinDenialKind.BuildMismatch, "build", "reason"));
        Assert.Throws<ArgumentNullException>(() => denialLog.ReportRepeats(null!, 2));
        Assert.Empty(sink.Events);
    }

    private static object Scalar(LogEvent logEvent, string property)
    {
        return Assert.IsType<ScalarValue>(logEvent.Properties[property]).Value;
    }

    private sealed class CaptureSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> events = new ConcurrentQueue<LogEvent>();

        public List<LogEvent> Events => events.ToList();

        public void Emit(LogEvent logEvent) => events.Enqueue(logEvent);
    }
}
