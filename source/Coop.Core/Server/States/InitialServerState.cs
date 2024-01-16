using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Messages;

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
        // Start server when game is fully loaded
        network.Start();

        // Remove server party
        messageBroker.Publish(this, new RemoveMainParty());

        // Change to server running state
        Logic.SetState<ServerRunningState>();
    }

    public override void Start()
    {
#if DEBUG
       messageBroker.Publish(this, new LoadDebugGame());
#endif
    }

    public override void Stop()
    {
    }
}
