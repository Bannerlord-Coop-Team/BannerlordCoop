using Common.Logging;
using Serilog;
using System;
using System.Diagnostics;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>Opens a server info link in the player's browser.</summary>
public interface IBrowserLinkOpener
{
    /// <summary>Checks the address again and opens it; a refused address or a failed start is logged, never thrown.</summary>
    void Open(string address);
}

/// <inheritdoc cref="IBrowserLinkOpener"/>
public sealed class BrowserLinkOpener : IBrowserLinkOpener
{
    private static readonly ILogger Logger = LogManager.GetLogger<BrowserLinkOpener>();

    private readonly IServerInfoLinkRules linkRules;
    private readonly Action<ProcessStartInfo> start;

    public BrowserLinkOpener(IServerInfoLinkRules linkRules) : this(linkRules, info => Process.Start(info)?.Dispose())
    {
    }

    internal BrowserLinkOpener(IServerInfoLinkRules linkRules, Action<ProcessStartInfo> start)
    {
        if (linkRules == null) throw new ArgumentNullException(nameof(linkRules));
        if (start == null) throw new ArgumentNullException(nameof(start));

        this.linkRules = linkRules;
        this.start = start;
    }

    // The shell gets only the re-checked http or https address, never the text the server sent.
    public void Open(string address)
    {
        if (!linkRules.TryNormalize(address, out string link))
        {
            Logger.Warning("Refused to open a server info link that is not an absolute http or https address");
            return;
        }

        try
        {
            start(new ProcessStartInfo(link) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            // A missing browser association or a blocked start must not reach the UI click handler.
            Logger.Error(exception, "Could not open a server info link in the browser");
        }
    }
}
