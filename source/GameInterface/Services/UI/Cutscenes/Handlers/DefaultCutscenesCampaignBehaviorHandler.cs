using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Cutscenes.Messages;
using SandBox.CampaignBehaviors;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.Core;

namespace GameInterface.Services.UI.Cutscenes.Handlers;

internal class DefaultCutscenesCampaignBehaviorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<DefaultCutscenesCampaignBehaviorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public DefaultCutscenesCampaignBehaviorHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<InitiateCutscenePlayerCharacterDied>(Handle_InitiateCutscenePlayerCharacterDied);
        messageBroker.Subscribe<NetworkInitiateCutscenePlayerCharacterDied>(Handle_NetworkInitiateCutscenePlayerCharacterDied);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitiateCutscenePlayerCharacterDied>(Handle_InitiateCutscenePlayerCharacterDied);
        messageBroker.Unsubscribe<NetworkInitiateCutscenePlayerCharacterDied>(Handle_NetworkInitiateCutscenePlayerCharacterDied);
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

        GameThread.RunSafe(() =>
        {
            if (!TryGetCutscenesBehavior(out var cutscenesBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.VictimId, out var victim)) return;
            if (victim != Hero.MainHero) return;

            Hero killer = null;
            if (data.KillerId != null && !objectManager.TryGetObjectWithLogging(data.KillerId, out killer)) return;

            var detail = data.Detail;
            SceneNotificationData sceneNotificationData = null;
            if (victim == Hero.MainHero)
            {
                MobileParty partyBelongedTo = victim.PartyBelongedTo;
                if (partyBelongedTo != null && partyBelongedTo.IsCurrentlyAtSea)
                {
                    sceneNotificationData = new NavalDeathSceneNotificationItem(victim, CampaignTime.Now, detail);
                }
                else if (detail == KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge)
                {
                    sceneNotificationData = new DeathOldAgeSceneNotificationItem(victim);
                }
                else if (detail == KillCharacterAction.KillCharacterActionDetail.DiedInBattle)
                {
                    if (cutscenesBehavior._heroWonLastMapEVent)
                    {
                        bool noCompanions = !victim.CompanionsInParty.Any<Hero>();
                        List<CharacterObject> encounterAllyCharacters = new List<CharacterObject>();
                        DefaultCutscenesCampaignBehavior.FillAllyCharacters(noCompanions, ref encounterAllyCharacters);
                        sceneNotificationData = new MainHeroBattleVictoryDeathNotificationItem(victim, encounterAllyCharacters);
                    }
                    else
                    {
                        sceneNotificationData = new MainHeroBattleDeathNotificationItem(victim, cutscenesBehavior._lastEnemyCulture);
                    }
                }
                else if (detail == KillCharacterAction.KillCharacterActionDetail.Executed || detail == KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent)
                {
                    if (killer != null)
                    {
                        sceneNotificationData = HeroExecutionSceneNotificationData.CreateForInformingPlayer(killer, victim, SceneNotificationData.RelevantContextType.Map, null);
                    }
                }
            }
            if (sceneNotificationData != null)
            {
                MBInformationManager.ShowSceneNotification(sceneNotificationData);
            }
        });
    }

    private bool TryGetCutscenesBehavior(out DefaultCutscenesCampaignBehavior cutscenesBehavior)
    {
        cutscenesBehavior = Campaign.Current?.GetCampaignBehavior<DefaultCutscenesCampaignBehavior>();
        if (cutscenesBehavior != null) return true;

        Logger.Debug("Skipping cutscene update because DefaultCutscenesCampaignBehavior is unavailable.");
        return false;
    }
}
