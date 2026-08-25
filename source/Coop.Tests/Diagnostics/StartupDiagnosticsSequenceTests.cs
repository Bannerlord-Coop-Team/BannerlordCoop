using Coop.Core.Diagnostics;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Diagnostics;

public class StartupDiagnosticsSequenceTests
{
    [Fact]
    public void ResolvesVersionBeforeEmittingHeaderAndInitializingCrashReporting()
    {
        var callOrder = new List<string>();
        string headerVersion = null;
        string crashReportingVersion = null;

        StartupDiagnosticsSequence.Run(
            resolveInformationalVersion: () =>
            {
                callOrder.Add("resolve");
                return "1.2.3-test";
            },
            emitLogHeader: version =>
            {
                callOrder.Add("header");
                headerVersion = version;
            },
            initializeCrashReporting: version =>
            {
                callOrder.Add("crash-reporting");
                crashReportingVersion = version;
            });

        Assert.Equal(new[] { "resolve", "header", "crash-reporting" }, callOrder);
        Assert.Equal("1.2.3-test", headerVersion);
        Assert.Equal("1.2.3-test", crashReportingVersion);
    }

    [Fact]
    public void CrashReportingNeverReceivesTheUnresolvedPlaceholder()
    {
        StartupDiagnosticsSequence.Run(
            resolveInformationalVersion: () => "9.9.9",
            emitLogHeader: _ => { },
            initializeCrashReporting: version => Assert.NotEqual("unknown", version));
    }
}
