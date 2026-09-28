using Common.Logging;
using GameInterface.Services.UI.ServerInfo;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Protects what reaches the shell when a player opens a server info link.</summary>
public sealed class BrowserLinkOpenerTests : IDisposable
{
    private readonly ConcurrentQueue<string> logs = new();
    private readonly List<ProcessStartInfo> started = new();
    private readonly Action<string> capture;

    public BrowserLinkOpenerTests()
    {
        capture = logs.Enqueue;
        OutputSinkManager.AddLogCallback(capture);
    }

    [Fact]
    public void ValidAddress_StartsTheShellWithTheNormalizedAddressOnly()
    {
        var opener = new BrowserLinkOpener(new ServerInfoLinkRules(), started.Add);

        opener.Open("HTTPS://Bücher.Example/Pfad?q=\"x\"");

        var info = Assert.Single(started);
        Assert.Equal("https://xn--bcher-kva.example/Pfad?q=%22x%22", info.FileName);
        Assert.Equal(string.Empty, info.Arguments);
        Assert.True(info.UseShellExecute);
    }

    // The client never opens what it would not show, even if a caller passes the raw server text.
    [Theory]
    [InlineData("javascript:void(0)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("https://user@example.com/")]
    [InlineData("/relative/path")]
    [InlineData("https://example.com/ \"--flag")]
    [InlineData("")]
    [InlineData(null)]
    public void RefusedAddress_NeverStartsAnything(string address)
    {
        var opener = new BrowserLinkOpener(new ServerInfoLinkRules(), started.Add);

        opener.Open(address);

        Assert.Empty(started);
        Assert.Contains(logs, log => log.Contains("Refused to open a server info link"));
    }

    [Fact]
    public void FailedStart_IsLoggedAndNotThrown()
    {
        string marker = "no browser " + Guid.NewGuid().ToString("N");
        var opener = new BrowserLinkOpener(new ServerInfoLinkRules(), _ => throw new Win32Exception(marker));

        var thrown = Record.Exception(() => opener.Open("https://example.com/"));

        Assert.Null(thrown);
        Assert.Contains(logs, log => log.Contains("Could not open a server info link") && log.Contains(marker));
    }

    [Fact]
    public void LogsNeverCarryTheAddress()
    {
        string marker = Guid.NewGuid().ToString("N");
        var opener = new BrowserLinkOpener(new ServerInfoLinkRules(), _ => throw new InvalidOperationException("start failed"));

        opener.Open("javascript:" + marker);
        opener.Open("https://example.com/" + marker);

        Assert.DoesNotContain(logs, log => log.Contains(marker));
        Assert.Equal(2, logs.Count(log => log.Contains("server info link")));
    }

    public void Dispose()
    {
        OutputSinkManager.RemoveLogCallback(capture);
    }
}
