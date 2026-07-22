using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Network.Packets;
using GameInterface.Services.Time.Interfaces;
using Serilog;
using System;
using System.Timers;

namespace Coop.Core.Server.Services.Time.Handlers;

/// <summary>
/// Periodically broadcasts the authoritative campaign time to all clients.
/// </summary>
/// <remarks>
/// Four times per second the server reads the current <c>MapTimeTracker</c> tick value and sends a
/// <see cref="CampaignTimePacket"/> to every connected client. It goes as a Sequenced packet — not a
/// reliable message — so the clock heartbeat stays live even while the reliable world-sync stream is
/// congested (see the packet's remarks).
/// </remarks>
public class CampaignTimeSyncHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CampaignTimeSyncHandler>();

    private const double PublishIntervalMs = 250d;

    private readonly INetwork network;
    private readonly IMapTimeTrackerInterface mapTimeTrackerInterface;

    private readonly object publishGate = new object();
    private readonly Timer publishTimer;
    private bool disposed;

    public CampaignTimeSyncHandler(INetwork network, IMapTimeTrackerInterface mapTimeTrackerInterface)
    {
        this.network = network;
        this.mapTimeTrackerInterface = mapTimeTrackerInterface;

        // Each broadcast re-arms the timer when it finishes instead of auto-resetting, so at most
        // one callback is ever in flight: a send stalled on a slow consumer delays the next tick
        // rather than stacking blocked thread pool callbacks behind it.
        publishTimer = new Timer(PublishIntervalMs) { AutoReset = false };
        publishTimer.Elapsed += PublishCampaignTime;
        publishTimer.Start();
    }

    public void Dispose()
    {
        lock (publishGate)
        {
            if (disposed) return;

            disposed = true;
            publishTimer.Elapsed -= PublishCampaignTime;
            publishTimer.Stop();
            publishTimer.Dispose();
        }
    }

    private void PublishCampaignTime(object sender, ElapsedEventArgs e)
    {
        lock (publishGate)
        {
            if (disposed) return;

            try
            {
                // No campaign loaded yet, nothing authoritative to broadcast.
                if (mapTimeTrackerInterface.TryGetCurrentTicks(out long currentTicks) == false) return;

                network.SendAll(new CampaignTimePacket(currentTicks));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to broadcast {message}", nameof(CampaignTimePacket));
            }
            finally
            {
                if (!disposed) publishTimer.Start();
            }
        }
    }
}
