using Common.Messaging;
using Common.Network;
using GameInterface.Registry;
using GameInterface.Registry.Messages;
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
    private readonly IRegistryManager registryManager;

    public InitialServerState(
        IServerLogic context,
        IMessageBroker messageBroker,
        IRegistryManager registryManager) : 
        base(context)
    {
        this.messageBroker = messageBroker;
        this.registryManager = registryManager;
        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);
    }

    internal void Handle_CampaignReady(MessagePayload<CampaignReady> payload)
    {
        // Remove server party
        messageBroker.Publish(this, new RemoveMainParty());

        // Register all objects after main party is removed to keep order
        registryManager.RegisterAllGameObjects();
        registryManager.PatchLifetimes();

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
