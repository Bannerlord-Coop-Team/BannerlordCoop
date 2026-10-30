using Common.Logging;
using Serilog;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>Opens a server info link in the player's browser.</summary>
public interface IBrowserLinkOpener
{
    /// <summary>Hands the address to the shell; a failed start is logged, never thrown.</summary>
    void Open(string address);
}

/// <inheritdoc cref="IBrowserLinkOpener"/>
public sealed class BrowserLinkOpener : IBrowserLinkOpener
{
    private static readonly ILogger Logger = LogManager.GetLogger<BrowserLinkOpener>();

    private readonly Action<ProcessStartInfo> start;

    public BrowserLinkOpener() : this(info => Process.Start(info)?.Dispose())
    {
    }

    internal BrowserLinkOpener(Action<ProcessStartInfo> start)
    {
        if (start == null) throw new ArgumentNullException(nameof(start));

        this.start = start;
    }

    // The panel only holds addresses that passed IServerInfoLinkRules, so the shell gets an http or https address.
    public void Open(string address)
    {
        try
        {
            start(new ProcessStartInfo(address) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            // A missing browser association or a blocked start must not reach the UI click handler. Only the type and
            // code are logged, because newer runtimes put the file name, here the address, in the message.
            int code = exception is Win32Exception win32 ? win32.NativeErrorCode : exception.HResult;
            Logger.Error("Could not open a server info link in the browser ({Error}, code {Code})", exception.GetType().Name, code);
        }
    }
}
