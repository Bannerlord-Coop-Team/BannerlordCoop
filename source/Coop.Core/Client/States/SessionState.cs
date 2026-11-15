using Common;
using Common.Messaging;
using Coop.Core.Common;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Sessions.Messages;
using GameInterface.Services.UI.Interfaces;

namespace Coop.Core.Client.States;

/// <summary>
/// Client state of a campaign-free session: there is no campaign to load, so entering the state asks the mod to
/// open its own mission. Copy of <see cref="MissionState"/> without the campaign re-entry.
/// </summary>
public class SessionState : ClientStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly IGameStateInterface gameStateInterface;
    private readonly ICoopFinalizer coopFinalizer;

    public SessionState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        IGameStateInterface gameStateInterface,
        ILoadingInterface loadingInterface,
        ICoopFinalizer coopFinalizer) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.gameStateInterface = gameStateInterface;
        this.coopFinalizer = coopFinalizer;
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);

        loadingInterface.HideLoadingScreen();

        var session = logic.Session;
        GameThread.RunSafe(
            () => messageBroker.Publish(this, new SessionMissionOpenRequested(session.SessionId, session.ModId, session.Payload)),
            context: nameof(SessionState));
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        // A mod that never opened its mission leaves the spawn gate engaged otherwise.
        BattleSpawnGate.EndBattle();
        coopFinalizer.Finalize("Left the co-op session");

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

    public override void EnterCampaignState()
    {
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
