using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.UI.Cutscenes.Messages;
using SandBox.CampaignBehaviors;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.UI.Cutscenes.Handlers;

internal class PlayerDeathCutsceneHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerDeathCutsceneHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    private readonly Queue<Action> pendingDeathPresentations = new();
    private SceneNotificationData pendingDeathScene;
    private bool isCreatingDeathScene;
    private bool disposed;

    public PlayerDeathCutsceneHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<GameExited>(Handle_GameExited);

        messageBroker.Subscribe<SceneNotificationQueued>(Handle_SceneNotificationQueued);
        messageBroker.Subscribe<SceneNotificationClosed>(Handle_SceneNotificationClosed);

        messageBroker.Subscribe<InitiateCutscenePlayerCharacterDied>(Handle_InitiateCutscenePlayerCharacterDied);
        messageBroker.Subscribe<NetworkInitiateCutscenePlayerCharacterDied>(Handle_NetworkInitiateCutscenePlayerCharacterDied);
    }

    public void Dispose()
    {
        disposed = true;
        ClearDeathPresentations();

        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<GameExited>(Handle_GameExited);

        messageBroker.Unsubscribe<SceneNotificationQueued>(Handle_SceneNotificationQueued);
        messageBroker.Unsubscribe<SceneNotificationClosed>(Handle_SceneNotificationClosed);

        messageBroker.Unsubscribe<InitiateCutscenePlayerCharacterDied>(Handle_InitiateCutscenePlayerCharacterDied);
        messageBroker.Unsubscribe<NetworkInitiateCutscenePlayerCharacterDied>(Handle_NetworkInitiateCutscenePlayerCharacterDied);
    }

    // Try to display any deferred death cutscenes
    private void Handle_CampaignTick(MessagePayload<CampaignTick> obj) => TryShowDeathPresentation();

    // Clean up queued presentations when leaving to prevent leaking into other sessions
    private void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj) => ClearDeathPresentations();
    private void Handle_GameExited(MessagePayload<GameExited> obj) => ClearDeathPresentations();

    private void Handle_SceneNotificationQueued(MessagePayload<SceneNotificationQueued> obj)
    {
        // Only use death scenes and not other cutscenes
        if (isCreatingDeathScene) pendingDeathScene = obj.What.Notification;
    }

    private void Handle_SceneNotificationClosed(MessagePayload<SceneNotificationClosed> obj)
    {
        if (ReferenceEquals(pendingDeathScene, obj.What.Notification)) pendingDeathScene = null;
    }

    private void Handle_InitiateCutscenePlayerCharacterDied(MessagePayload<InitiateCutscenePlayerCharacterDied> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Victim, out var victimId)) return;
        string killerId = null;
        if (data.Killer != null && !objectManager.TryGetIdWithLogging(data.Killer, out killerId)) return;

        network.SendAll(new NetworkInitiateCutscenePlayerCharacterDied(victimId, killerId, data.Detail));
    }

    private void Handle_NetworkInitiateCutscenePlayerCharacterDied(MessagePayload<NetworkInitiateCutscenePlayerCharacterDied> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() => EnqueueDeathPresentation(() =>
        {
            if (!TryGetCutscenesBehavior(out var cutscenesBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.VictimId, out var victim)) return;
            if (victim != Hero.MainHero) return;

            Hero killer = null;
            if (data.KillerId != null && !objectManager.TryGetObjectWithLogging(data.KillerId, out killer)) return;

            // Block invalid death cutscenes
            if (killer == null && victim.PartyBelongedTo?.IsCurrentlyAtSea != true &&
                (data.Detail == KillCharacterAction.KillCharacterActionDetail.Executed ||
                 data.Detail == KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent)) return;

            // Capture only the cutscene queued by this call
            isCreatingDeathScene = true;
            try
            {
                cutscenesBehavior.OnBeforeMainCharacterDied(victim, killer, data.Detail);
            }
            finally
            {
                isCreatingDeathScene = false;
            }
        }));
    }

    // Keep the death scene and its heir selection or game-over UI in order after the battle.
    public void EnqueueDeathPresentation(Action presentation)
    {
        if (disposed) return;

        pendingDeathPresentations.Enqueue(presentation);
        TryShowDeathPresentation();
    }

    private void TryShowDeathPresentation()
    {
        if (disposed || pendingDeathPresentations.Count == 0 || pendingDeathScene != null) return;

        // Unlike vanilla, MapEvents end while players are still in a mission
        // Don't show death cutscene while client is still in a mission for the battle they potentialy died in
        // Defer until campaign tick
        if (Mission.Current != null || MissionState.Current != null) return;

        // Defer death cutscene if not looking at the map or in a simulated battle
        var stateManager = Game.Current?.GameStateManager;
        if (stateManager?.ActiveState is not MapState mapState || mapState.IsSimulationActive) return;

        // Defer death cutscene if any other cutscenes are currently active
        if (MBInformationManager.GetIsAnySceneNotificationActive() == true) return;

        GameThread.RunSafe(pendingDeathPresentations.Dequeue());
    }

    private void ClearDeathPresentations()
    {
        pendingDeathPresentations.Clear();
        pendingDeathScene = null;
    }

    private bool TryGetCutscenesBehavior(out DefaultCutscenesCampaignBehavior cutscenesBehavior)
    {
        cutscenesBehavior = Campaign.Current?.GetCampaignBehavior<DefaultCutscenesCampaignBehavior>();
        if (cutscenesBehavior != null) return true;

        Logger.Debug("Skipping cutscene update because DefaultCutscenesCampaignBehavior is unavailable.");
        return false;
    }
}
