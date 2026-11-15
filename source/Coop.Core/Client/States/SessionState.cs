using Common;
using Common.Messaging;
using Coop.Core.Common;
using GameInterface.Registry;
using GameInterface.Services.GameState;
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
    private readonly ICoopFinalizer coopFinalizer;
    private readonly IRegistryManager registryManager;

    public SessionState(
        IClientLogic logic,
        IMessageBroker messageBroker,
        ILoadingInterface loadingInterface,
        ICoopFinalizer coopFinalizer,
        IRegistryManager registryManager) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.coopFinalizer = coopFinalizer;
        this.registryManager = registryManager;
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);

        var session = logic.Session;
        GameThread.RunSafe(() =>
        {
            loadingInterface.HideLoadingScreen();
            registryManager.RegisterMissionItemObjects();
            messageBroker.Publish(this, new SessionMissionOpenRequested(session.SessionId, session.ModId, session.Payload));
        }, context: nameof(SessionState));
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        registryManager.ClearAllRegistries();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
    {
        LeaveToMainMenu();
    }

    private void LeaveToMainMenu()
    {
        // A mod that never opened its mission leaves the spawn gate engaged otherwise.
        BattleSpawnGate.EndBattle();
        coopFinalizer.Finalize("Left the co-op session");

        Logic.SetState<MainMenuState>();
    }

    public override void EnterMainMenu()
    {
        Disconnect();
    }

    public override void Connect()
    {
    }

    public override void Disconnect()
    {
        // GoToMainMenu does nothing without a campaign, so the mod ends its own mission instead.
        var sessionId = Logic.Session?.SessionId;
        GameThread.RunSafe(() =>
        {
            if (GameStateQuery.IsAtMainMenu)
            {
                LeaveToMainMenu();
                return;
            }

            messageBroker.Publish(this, new SessionMissionEndRequested(sessionId));
        }, context: nameof(Disconnect));
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
