using Common.Messaging;
using Common.Network;
using GameInterface.Registry.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Messages;
using System;

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
        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_GameObjectsRegistered);
        messageBroker.Subscribe<LifetimesPatched>(Handle_LifetimesPatched);
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

        // Register all objects after main party is removed to keep order
        messageBroker.Publish(this, new RegisterAllGameObjects());
    }

    internal void Handle_GameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> payload)
    {
        messageBroker.Publish(this, new PatchLifetimes());
    }

    internal void Handle_LifetimesPatched(MessagePayload<LifetimesPatched> payload)
    {
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
