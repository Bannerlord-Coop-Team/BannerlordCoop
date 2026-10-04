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
    public void CheckedAddress_StartsTheShellWithThatAddressOnly()
    {
        var opener = new BrowserLinkOpener(new ServerInfoLinkRules(), started.Add);

        opener.Open("https://xn--bcher-kva.example/Pfad?q=%22x%22");

        var info = Assert.Single(started);
        Assert.Equal("https://xn--bcher-kva.example/Pfad?q=%22x%22", info.FileName);
        Assert.Equal(string.Empty, info.Arguments);
        Assert.True(info.UseShellExecute);
    }

    // The dialog shows the checked form, so text that checks to anything else was never shown and never opens.
    [Theory]
    [InlineData("HTTPS://Example.COM/")]
    [InlineData("https://Bücher.example/pfad")]
    [InlineData("http://example.com")]
    [InlineData("https://example.com:443/")]
    [InlineData("https://example.com/a\"b")]
    public void AddressThatIsNotInItsCheckedForm_NeverStartsAnything(string address)
    {
        var opener = new BrowserLinkOpener(new ServerInfoLinkRules(), started.Add);

        opener.Open(address);

        Assert.Empty(started);
        Assert.Contains(logs, log => log.Contains("Refused to open a server info link"));
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

    // 1155 is ERROR_NO_ASSOCIATION, what Windows reports when no browser is set up for http.
    [Fact]
    public void FailedStart_IsLoggedWithItsTypeAndCodeAndNotThrown()
    {
        string marker = "no browser " + Guid.NewGuid().ToString("N");
        var opener = new BrowserLinkOpener(new ServerInfoLinkRules(), _ => throw new Win32Exception(1155, marker));

        var thrown = Record.Exception(() => opener.Open("https://example.com/"));

        Assert.Null(thrown);
        Assert.Contains(logs, log => log.Contains("Could not open a server info link in the browser") && log.Contains("Win32Exception") && log.Contains("code 1155"));
        Assert.DoesNotContain(logs, log => log.Contains(marker));
    }

    // On .NET Core and newer the start error names the file, here the address, so the message is never logged.
    [Fact]
    public void LogsNeverCarryTheAddress()
    {
        string marker = Guid.NewGuid().ToString("N");
        var errors = new Queue<Exception>(new Exception[]
        {
            new Win32Exception(2, "An error occurred trying to start process 'https://example.com/" + marker + "' with working directory 'C:\\'."),
            new InvalidOperationException("start failed for https://example.com/" + marker),
        });
        var opener = new BrowserLinkOpener(new ServerInfoLinkRules(), _ => throw errors.Dequeue());

        opener.Open("javascript:" + marker);
        opener.Open("https://example.com/" + marker);
        opener.Open("https://example.com/" + marker);

        Assert.DoesNotContain(logs, log => log.Contains(marker));
        Assert.Equal(3, logs.Count(log => log.Contains("server info link")));
        Assert.Contains(logs, log => log.Contains("InvalidOperationException") && log.Contains("code " + new InvalidOperationException().HResult));
    }

    public void Dispose()
    {
        OutputSinkManager.RemoveLogCallback(capture);
    }
}
