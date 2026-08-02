using Common;
using System;
using System.Threading;

namespace Coop.Steam;

public interface ISteamLobbyLeaseRenewer : IDisposable
{
    void Start(Action renew);
    void Stop();
}

/// <summary>Schedules required Steam lobby lease writes on the game thread.</summary>
public class SteamLobbyLeaseRenewer : ISteamLobbyLeaseRenewer
{
    public static readonly TimeSpan RenewalInterval = TimeSpan.FromSeconds(15);

    private readonly TimeSpan interval;
    private Timer timer;
    private bool disposed;

    public SteamLobbyLeaseRenewer()
        : this(RenewalInterval)
    {
    }

    internal SteamLobbyLeaseRenewer(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        this.interval = interval;
    }

    public void Start(Action renew)
    {
        if (renew == null) throw new ArgumentNullException(nameof(renew));
        if (disposed) return;

        Stop();
        timer = new Timer(_ => GameThread.RunSafe(renew, context: "RenewSteamLobbyLease"),
            null, interval, interval);
    }

    public void Stop()
    {
        timer?.Dispose();
        timer = null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Stop();
    }
}
