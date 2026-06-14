using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network.Instances.Messages;
using GameInterface.Services.UI.Interfaces;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameInterface.Services.Locations.Handlers;

/// <summary>
/// Shows a loading screen on a client that is <em>joining</em> a settlement interior (tavern) instance
/// while the scene loads and the P2P link to the players already inside is established. Entering an
/// interior in live co-op leaves the joiner standing in an empty room for several seconds — the remote
/// players cannot spawn until the joiner's own scene load finishes freeing the main thread, and the
/// NAT-punched P2P link comes up. Holding the engine's loading window over that gap hides the empty
/// interior until at least one remote player has spawned (<see cref="InstanceReady"/>).
///
/// Only the joining client is blocked: the first member of an instance is its host
/// (<see cref="InstanceAssigned.IsHost"/>), has nobody to wait for, and is skipped.
/// </summary>
public class LocationLoadingScreenHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationLoadingScreenHandler>();

    // Safety net: if the P2P link never comes up (NAT punch failed, the other player left), drop the
    // loading screen anyway rather than trapping the player on it forever.
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(45);

    private readonly IMessageBroker messageBroker;
    private readonly ILoadingInterface loadingInterface;

    private readonly object gate = new object();
    private CancellationTokenSource timeoutCts;
    private bool showing;

    public LocationLoadingScreenHandler(IMessageBroker messageBroker, ILoadingInterface loadingInterface)
    {
        this.messageBroker = messageBroker;
        this.loadingInterface = loadingInterface;

        messageBroker.Subscribe<InstanceAssigned>(Handle_Assigned);
        messageBroker.Subscribe<InstanceReady>(Handle_Ready);
        messageBroker.Subscribe<InstanceCleared>(Handle_Cleared);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InstanceAssigned>(Handle_Assigned);
        messageBroker.Unsubscribe<InstanceReady>(Handle_Ready);
        messageBroker.Unsubscribe<InstanceCleared>(Handle_Cleared);
        Hide();
    }

    private void Handle_Assigned(MessagePayload<InstanceAssigned> payload)
    {
        // The host is the first member of the instance — there is nobody to wait for, so it never shows
        // the loading screen. Only a later joiner (IsHost == false) is held until a peer appears.
        if (ModInformation.IsServer || payload.What.IsHost) return;

        Logger.Information("[LocationSync] Joining instance {Id} — showing loading screen until a peer connects", payload.What.InstanceId);
        Show();
    }

    private void Handle_Ready(MessagePayload<InstanceReady> payload)
    {
        Logger.Information("[LocationSync] Instance {Id} populated — hiding loading screen", payload.What.InstanceId);
        Hide();
    }

    private void Handle_Cleared(MessagePayload<InstanceCleared> payload) => Hide();

    private void Show()
    {
        lock (gate)
        {
            if (showing) return;
            showing = true;

            timeoutCts?.Cancel();
            timeoutCts = new CancellationTokenSource();
            StartTimeout(timeoutCts.Token);
        }

        // Engine UI calls must run on the main thread; InstanceAssigned is published off the network
        // receive thread.
        GameLoopRunner.RunOnMainThread(() =>
            loadingInterface.ShowLoadingScreen("Entering location", "Connecting to other players..."));
    }

    private void Hide()
    {
        lock (gate)
        {
            if (showing == false) return;
            showing = false;

            timeoutCts?.Cancel();
            timeoutCts = null;
        }

        GameLoopRunner.RunOnMainThread(() => loadingInterface.HideLoadingScreen());
    }

    private void StartTimeout(CancellationToken token)
    {
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(MaxWait, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Logger.Warning("[LocationSync] No peer connected within {Seconds}s — hiding loading screen anyway. REPORT THIS.", MaxWait.TotalSeconds);
            Hide();
        });
    }
}
