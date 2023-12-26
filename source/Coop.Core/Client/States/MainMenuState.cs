using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Main Menu Client State
/// </summary>
public class MainMenuState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public MainMenuState(IClientLogic logic, IMessageBroker messageBroker, INetwork network) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkConnected>(Handle_NetworkConnected);
    }

    public override void Dispose() 
    {
        messageBroker.Unsubscribe<NetworkConnected>(Handle_NetworkConnected);
    }

    public override void Connect()
    {
        network.Start();
    }

    internal void Handle_NetworkConnected(MessagePayload<NetworkConnected> obj)
    {
        Logic.ValidateModules();
    }

    public override void Disconnect()
    {
        messageBroker.Publish(this, new EnterMainMenu());
    }

    public override void EnterMainMenu()
    {
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

    public override void EnterMissionState()
    {
    }

    public override void ValidateModules()
    {
        Logic.SetState<ValidateModuleState>();
    }
}
