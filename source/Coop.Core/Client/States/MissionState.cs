using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Mission (Battles) Client State
/// </summary>
public class MissionState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;

    public MissionState(IClientLogic logic, IMessageBroker messageBroker) : base(logic)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<CampaignStateEntered>(Handle_CampaignStateEntered);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<CampaignStateEntered>(Handle_CampaignStateEntered);
    }

    public override void EnterCampaignState()
    {
        messageBroker.Publish(this, new EnterCampaignState());
    }

    internal void Handle_CampaignStateEntered(MessagePayload<CampaignStateEntered> obj)
    {
        Logic.SetState<CampaignState>();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        Logic.SetState<MainMenuState>();
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

    public override void EnterMissionState()
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

    public override void ValidateModules()
    {
    }
}
