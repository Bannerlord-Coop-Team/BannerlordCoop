using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Loading Client State
/// </summary>
public class LoadingState : ClientStateBase
{
    public LoadingState(IClientLogic logic) : base(logic)
    {
        Logic.MessageBroker.Subscribe<CampaignReady>(Handle_CampaignLoaded);
    }

    public override void Dispose()
    {
        Logic.MessageBroker.Unsubscribe<CampaignReady>(Handle_CampaignLoaded);
    }

    public override void EnterMainMenu()
    {
        Logic.MessageBroker.Publish(this, new EnterMainMenu());
    }

    internal void Handle_CampaignLoaded(MessagePayload<CampaignReady> obj)
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

        Logic.MessageBroker.Publish(this, new RegisterAllGameObjects());
    }

    public override void EnterMissionState()
    {
    }

    public override void ValidateModules()
    {
    }
}
