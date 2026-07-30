using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.UI.Interfaces;
using Serilog;

namespace Coop.Core.Server.States;

/// <summary>
/// State representing the server is in the campaign and running
/// </summary>
public class ServerRunningState : ServerStateBase
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerRunningState>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IGameStateInterface gameStateInterface;

    public ServerRunningState(
        IServerLogic logic,
        IMessageBroker messageBroker,
        INetwork network,
        IGameStateInterface gameStateInterface,
        ILoadingInterface loadingInterface) : base(logic)    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.gameStateInterface = gameStateInterface;

        // Start server
        network.Start();

        loadingInterface.HideLoadingScreen();

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);

        // Join-baseline preflight: capture every party once right after the campaign
        // comes up. A save can carry parties whose AI references objects that no
        // longer exist (#2489), and any such party blocks every join until repaired —
        // the capture self-heals them, so running it at boot repairs the world before
        // the first client arrives instead of during a join. Failures that survive
        // healing are logged so the operator knows joins will not work.
        GameThread.RunSafe(() =>
        {
            if (!GameInterface.ContainerProvider.TryResolve(out IMobilePartyBehaviorSnapshot behaviorSnapshot)) return;
            var parties = TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager?.MobileParties;
            if (parties == null) return;
            int bad = 0;
            for (int i = 0; i < parties.Count; i++)
            {
                if (!behaviorSnapshot.TryCreateJoinState(parties[i], out _, out string reason))
                {
                    bad++;
                    Logger.Error("Join-baseline preflight: party {Party} cannot be captured and will block joins: {Reason}",
                        parties[i]?.StringId, reason);
                }
            }
            if (bad > 0)
                Logger.Error("Join-baseline preflight: {Bad} of {Total} parties cannot be captured; joins will fail until this is resolved", bad, parties.Count);
        }, context: "JoinBaselinePreflight");
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public override void Start()
    {
    }

    public override void Stop()
    {
        // Stop server
        network.Dispose();

        // Go to main menu
        gameStateInterface.GoToMainMenu();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> payload)
    {
        messageBroker.Publish(this, new SendPopupMessage("Server has been stopped"));
        messageBroker.Publish(this, new EndCoopMode());

        Logic.SetState<InitialServerState>();
    }
}
