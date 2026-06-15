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
/// Holds a loading screen on a joining client until a peer spawns (<see cref="InstanceReady"/>), hiding
/// the empty interior while the scene loads and the P2P link comes up. The host (first member) has nobody
/// to wait for and is skipped.
/// </summary>
public class LocationLoadingScreenHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationLoadingScreenHandler>();

    // Safety net so a failed NAT punch can't trap the player on the loading screen.
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
        // Host has nobody to wait for; only a joiner (IsHost == false) is held.
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

        // Engine UI calls must run on the main thread (InstanceAssigned arrives off the network thread).
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
