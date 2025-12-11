using Common.Messaging;
using Common.Network;
using GameInterface.Registry.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Server.States;

/// <summary>
/// State represting the server has just started
/// </summary>
public class InitialServerState : ServerStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public InitialServerState(IServerLogic context, IMessageBroker messageBroker, INetwork network) : base(context)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<CampaignReady>(Handle_GameLoaded);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_GameLoaded);
    }

    internal void Handle_GameLoaded(MessagePayload<CampaignReady> payload)
    {
        global::Common.Logging.LogManager.GetLogger<InitialServerState>().Information("Campaign ready");

        messageBroker.Publish(this, new RegisterAllGameObjects());

        global::Common.Logging.LogManager.GetLogger<InitialServerState>().Information("Switching to ServerRunningState");
        Logic.SetState<ServerRunningState>();

        // Auto-save au démarrage du serveur
        messageBroker.Publish(this, new PackageGameSaveData());
    }

    public override void Start()
    {
        global::Common.Logging.LogManager.GetLogger<InitialServerState>().Information("InitialServerState Start, starting network listener");
        network.Start();
        if (network is Coop.Core.Common.Network.CoopNetworkBase nb)
        {
            messageBroker.Publish(this, new SendInformationMessage($"Serveur: écoute sur port {nb.Configuration.Port}"));
        }
#if DEBUG
        messageBroker.Publish(this, new LoadDebugGame());
#endif
    }

    public override void Stop()
    {
    }
}
