// Ignore Spelling: Finalizer

using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Common;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;

namespace Coop.Core.Client.States;

/// <summary>
/// State controller for campaign client state
/// </summary>
public class CampaignState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly ICoopFinalizer coopFinalizer;

    public CampaignState(
        IClientLogic logic,
        IMessageBroker messageBroker, 
        INetwork network, 
        ICoopFinalizer coopFinalizer) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.coopFinalizer = coopFinalizer;
        messageBroker.Subscribe<NetworkNewPartyCreated>(Handle_NetworkNewPartyCreated);

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<MissionStateEntered>(Handle_MissionStateEntered);

        
        network.SendAll(new NetworkPlayerCampaignEntered());
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkNewPartyCreated>(Handle_NetworkNewPartyCreated);

        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<MissionStateEntered>(Handle_MissionStateEntered);
    }

    private void Handle_NetworkNewPartyCreated(MessagePayload<NetworkNewPartyCreated> obj)
    {
        var message = new RegisterNewPlayerHero((NetPeer)obj.Who, obj.What.PlayerId, obj.What.PlayerHero);
        messageBroker.Publish(this, message);
    }

    internal void Handle_MissionStateEntered(MessagePayload<MissionStateEntered> obj)
    {
        Logic.SetState<MissionState>();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        coopFinalizer.Finalize("Client has been stopped");
    }
    

    public override void EnterMissionState()
    {
        messageBroker.Publish(this, new EnterMissionState());
    }

    public override void EnterMainMenu()
    {
        messageBroker.Publish(this, new EnterMainMenu());
    }

    public override void Connect()
    {
    }

    public override void Disconnect()
    {
        messageBroker.Publish(this, new EnterMainMenu());
    }

    public override void ExitGame()
    {
    }

    public override void LoadSavedData()
    {
    }

    public override void StartCharacterCreation()
    {
    }

    public override void EnterCampaignState()
    {
    }

    public override void ValidateModules()
    {
    }
}
