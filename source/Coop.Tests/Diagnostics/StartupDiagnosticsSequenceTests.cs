using Common;
using Coop.Core.Diagnostics;
using System;
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
            emitLogHeader: version =>
            {
                callOrder.Add("header");
                headerVersion = version;
            },
            initializeCrashReporting: version =>
            {
                callOrder.Add("crash-reporting");
                crashReportingVersion = version;
            },
            resolveInformationalVersion: () =>
            {
                callOrder.Add("resolve");
                return "1.2.3-test";
            });

        Assert.Equal(new[] { "resolve", "header", "crash-reporting" }, callOrder);
        Assert.Equal("1.2.3-test", headerVersion);
        Assert.Equal("1.2.3-test", crashReportingVersion);
    }

    [Fact]
    public void CrashReportingNeverReceivesTheUnresolvedPlaceholder()
    {
        StartupDiagnosticsSequence.Run(
            emitLogHeader: _ => { },
            initializeCrashReporting: version => Assert.NotEqual("unknown", version),
            resolveInformationalVersion: () => "9.9.9");
    }

    [Fact]
    public void CrashReportingStillInitializesWhenHeaderDelegateThrows()
    {
        string crashReportingVersion = null;

        StartupDiagnosticsSequence.Run(
            emitLogHeader: _ => throw new InvalidOperationException("header emission failed"),
            initializeCrashReporting: version => crashReportingVersion = version,
            resolveInformationalVersion: () => "1.2.3-test");

        Assert.Equal("1.2.3-test", crashReportingVersion);
    }

    [Fact]
    public void DefaultsToTheRealBuildVersionWhenNoResolverIsSupplied()
    {
        string headerVersion = null;
        string crashReportingVersion = null;

        StartupDiagnosticsSequence.Run(
            emitLogHeader: version => headerVersion = version,
            initializeCrashReporting: version => crashReportingVersion = version);

        Assert.Equal(ModInformation.BuildVersion, headerVersion);
        Assert.Equal(ModInformation.BuildVersion, crashReportingVersion);
    }
}
