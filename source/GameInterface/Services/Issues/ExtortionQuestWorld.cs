using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using Helpers;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues;

using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using QuestState = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

internal interface IExtortionQuestWorld
{
    void Tick(Quest quest);
    void VillageRaided(Quest quest, Village village);
    void StartAmbush(Quest quest);
    void CleanupBattle(Quest quest);
    void PlayerReplaced(Hero heir);
    void BattleStarted(Quest quest, MapEvent mapEvent, PartyBase attacker, PartyBase defender);
}

internal sealed class ExtortionQuestWorld : IExtortionQuestWorld
{
    private readonly IExtortionQuestContext context;
    private readonly IMessageBroker broker;

    public ExtortionQuestWorld(IExtortionQuestContext context, IMessageBroker broker)
    {
        this.context = context;
        this.broker = broker;
    }

    public void Tick(Quest quest)
    {
        CleanupBattle(quest);
        if (!quest.IsOngoing || quest._deserterMobileParty == null || !quest._deserterMobileParty.IsActive ||
            quest._deserterMobileParty.MapEvent != null) return;
        if (!context.TryEnter(quest.QuestGiver, out var scope)) return;
        using (scope)
        {
            var deserters = quest._deserterMobileParty;
            var playerParty = MobileParty.MainParty;
            var previousState = quest._currentState;
            var previouslyWarned = quest._playerAwayFromSettlementNotificationSent;
            switch (quest._currentState)
            {
                case QuestState.DesertersMovingToSettlement:
                    if (playerParty.CurrentSettlement == quest.QuestSettlement)
                    {
                        ResumeRaid(quest);
                    }
                    else if (deserters.Position.Distance(playerParty.Position) <= deserters.SeeingRange * 0.8f)
                    {
                        quest.HandleDesertersRunningAway();
                        quest._currentState = QuestState.DesertersRunningAwayFromPlayer;
                        quest._desertersRunAwayTimeoutTime = CampaignTime.HoursFromNow(10f);
                    }
                    else
                    {
                        quest._playerAwayFromSettlementNotificationSent = true;
                    }
                    break;
                case QuestState.DesertersRunningAwayFromPlayer:
                    if (deserters.Position.Distance(playerParty.Position) > playerParty.SeeingRange + 3f)
                    {
                        quest.ApplyQuestResult(quest._questResultFail1);
                        quest.CompleteQuestWithFail(quest.OnQuestFailed1LogText);
                    }
                    else if (quest._desertersRunAwayTimeoutTime.IsPast)
                    {
                        quest.DestroyDeserterParty();
                        quest.ApplyQuestResult(quest._questResultFail1);
                        quest.CompleteQuestWithFail(quest.OnQuestFailed1LogText);
                    }
                    else
                    {
                        quest.HandleDesertersRunningAway();
                    }
                    break;
                case QuestState.DesertersDefeatedPlayer:
                    ResumeRaid(quest);
                    break;
            }
            if (previousState != quest._currentState || previouslyWarned != quest._playerAwayFromSettlementNotificationSent)
                broker.Publish(quest, new ExtortionQuestChanged(quest));
        }
    }

    public void PlayerReplaced(Hero heir)
    {
        foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>().ToArray())
        {
            if (!quest.IsOngoing || !context.TryGetPlayer(quest.QuestGiver, out var player, out _) || player != heir) continue;
            quest.CompleteQuestWithCancel(new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.\""));
        }
    }

    public void BattleStarted(Quest quest, MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        if (!quest.IsOngoing || !context.TryEnter(quest.QuestGiver, out var scope)) return;
        using (scope)
        {
            if (attacker != PartyBase.MainParty) return;
            if (mapEvent.IsFieldBattle && defender.IsMobile && defender.MobileParty.HomeSettlement == quest.QuestSettlement &&
                defender != quest._deserterMobileParty?.Party)
            {
                quest.CompleteQuestWithFail(quest.OnQuestFailed3LogText);
                quest.ApplyQuestResult(quest._questResultFail3);
            }
            else if (QuestHelper.CheckMinorMajorCoercion(quest, mapEvent, attacker))
            {
                QuestHelper.ApplyGenericMinorMajorCoercionConsequences(quest, mapEvent);
            }
        }
    }

    public void VillageRaided(Quest quest, Village village)
    {
        if (!quest.IsOngoing || village.Settlement != quest.QuestSettlement) return;
        if (!context.TryEnter(quest.QuestGiver, out var scope)) return;
        using (scope)
        {
            var attacker = village.Settlement.Party.MapEvent.AttackerSide.LeaderParty;
            if (attacker == quest._deserterMobileParty?.Party)
            {
                if (MobileParty.MainParty.CurrentSettlement != quest.QuestSettlement)
                {
                    quest.ApplyQuestResult(quest._questResultFail2);
                    quest.CompleteQuestWithFail(quest.OnQuestFailed2LogText);
                }
            }
            else if (attacker == PartyBase.MainParty)
            {
                quest.ApplyQuestResult(quest._questResultFail3);
                quest.CompleteQuestWithFail(quest.OnQuestFailed3LogText);
            }
            else
            {
                quest.ApplyQuestResult(quest._questResultCancel2);
                quest.CompleteQuestWithCancel(quest.OnQuestCancel2LogText);
            }
        }
    }

    public void StartAmbush(Quest quest)
    {
        if (!quest.IsOngoing || quest._defenderMobileParty != null ||
            !context.TryEnter(quest.QuestGiver, out var scope)) return;
        using (scope)
        {
            var party = MobileParty.MainParty;
            var battle = quest._deserterMobileParty?.MapEvent;
            if (party.CurrentSettlement != quest.QuestSettlement || party.MapEvent != null ||
                battle == null || !battle.IsRaid || battle.DefenderSide.LeaderParty != quest.QuestSettlement.Party) return;

            quest.CreateDefenderParty();
            party.MapEventSide = battle.DefenderSide;
            quest._defenderMobileParty.MapEventSide = battle.DefenderSide;
            quest._deserterMobileParty.IgnoreByOtherPartiesTill(CampaignTime.Now - CampaignTime.Hours(1f));
            if (quest.QuestSettlement.MilitiaPartyComponent != null)
                quest.QuestSettlement.MilitiaPartyComponent.MobileParty.MapEventSide = null;
            broker.Publish(quest, new ExtortionQuestChanged(quest, startAmbush: true));
        }
    }

    public void CleanupBattle(Quest quest)
    {
        if (!quest.IsOngoing || !quest._deserterBattleFinalizedForTheFirstTime ||
            quest._deserterMobileParty?.MapEvent != null || quest._defenderMobileParty?.MapEvent != null ||
            !context.TryEnter(quest.QuestGiver, out var scope)) return;
        using (scope)
        {
            quest._deserterBattleFinalizedForTheFirstTime = false;
            if (quest._currentState == QuestState.DesertersAreDefeated) quest.DestroyDeserterParty();
            quest.DestroyDefenderParty();
            broker.Publish(quest, new ExtortionQuestChanged(quest));
        }
    }

    private void ResumeRaid(Quest quest)
    {
        if (quest._deserterMobileParty.DefaultBehavior != AiBehavior.RaidSettlement)
            SetPartyAiAction.GetActionForRaidingSettlement(quest._deserterMobileParty, quest.QuestSettlement,
                MobileParty.NavigationType.Default, false, false);
    }
}
