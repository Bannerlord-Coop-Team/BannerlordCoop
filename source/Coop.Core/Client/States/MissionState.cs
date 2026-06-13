using Common.Messaging;
using Coop.Core.Common;
using GameInterface;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States;

/// <summary>
/// State Logic Controller for the Mission (Battles) Client State
/// </summary>
public class MissionState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly IGameStateInterface gameStateInterface;
    private readonly ICoopFinalizer coopFinalizer;

    public MissionState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        IGameStateInterface gameStateInterface,
        ICoopFinalizer coopFinalizer) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.gameStateInterface = gameStateInterface;
        this.coopFinalizer = coopFinalizer;
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
        coopFinalizer.Finalize("Client has been stopped");

        Logic.SetState<MainMenuState>();
    }

    public override void EnterMainMenu()
    {
        gameStateInterface.GoToMainMenu();
    }

    public override void Connect()
    {
    }

    public override void Disconnect()
    {
        gameStateInterface.GoToMainMenu();
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
