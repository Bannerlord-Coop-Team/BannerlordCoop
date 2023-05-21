using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.States;

/// <summary>
/// State controller for campaign client state
/// </summary>
public class CampaignState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    public CampaignState(IClientLogic logic) : base(logic)
    {
        messageBroker = logic.MessageBroker;
        network = logic.Network;
        
        messageBroker.Subscribe<NetworkDisableTimeControls>(Handle_NetworkDisableTimeControls);

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<MissionStateEntered>(Handle_MissionStateEntered);
        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
    }

    internal void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> obj)
    {
        messageBroker.Publish(this, new SwitchToHero(Logic.ControlledHeroId));
        network.SendAll(new NetworkPlayerCampaignEntered());
    }

    internal void Handle_NetworkDisableTimeControls(MessagePayload<NetworkDisableTimeControls> obj)
    {
        // TODO will conflict with timemode changed event
        messageBroker.Publish(this, new PauseAndDisableGameTimeControls());
    }

    internal void Handle_MissionStateEntered(MessagePayload<MissionStateEntered> obj)
    {
        Logic.State = new MissionState(Logic);
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        Logic.State = new MainMenuState(Logic);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkDisableTimeControls>(Handle_NetworkDisableTimeControls);

        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<MissionStateEntered>(Handle_MissionStateEntered);
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
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
