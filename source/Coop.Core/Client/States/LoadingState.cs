using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Loading Client State
/// </summary>
public class LoadingState : ClientStateBase
{
    public LoadingState(IClientLogic logic) : base(logic)
    {
        Logic.MessageBroker.Subscribe<CampaignLoaded>(Handle_CampaignLoaded);
        Logic.MessageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public override void Dispose()
    {
        Logic.MessageBroker.Unsubscribe<CampaignLoaded>(Handle_CampaignLoaded);
        Logic.MessageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public override void EnterMainMenu()
    {
        Logic.MessageBroker.Publish(this, new EnterMainMenu());
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        Logic.State = new MainMenuState(Logic);
    }

    internal void Handle_CampaignLoaded(MessagePayload<CampaignLoaded> obj)
    {
        Logic.EnterCampaignState();
    }

    public override void Connect()
    {
    }

    public override void Disconnect()
    {
        Logic.MessageBroker.Publish(this, new EnterMainMenu());
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
        Logic.State = new CampaignState(Logic);
    }

    public override void EnterMissionState()
    {
    }

    public override void ValidateModules()
    {
    }
}
