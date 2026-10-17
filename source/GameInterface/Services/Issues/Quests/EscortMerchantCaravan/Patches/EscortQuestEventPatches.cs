using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan.Patches;

[HarmonyPatch]
internal class EscortQuestEventPatches
{
    [ThreadStatic] internal static Quest CurrentQuest;

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementEntered))]
    [HarmonyPostfix]
    private static void Entered(MobileParty party, Settlement settlement, Hero hero)
        => Run(quest => quest.OnSettlementEntered(party, settlement, hero));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementLeft))]
    [HarmonyPostfix]
    private static void Left(MobileParty party, Settlement settlement)
        => Run(quest => quest.OnSettlementLeft(party, settlement));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void Battle(MapEvent mapEvent)
        => Run(quest => quest.OnMapEventEnded(mapEvent));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix]
    private static void War(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
        => Run(quest => quest.OnWarDeclared(faction1, faction2, declareWarDetail));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void Kingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail actionDetail, bool showNotification)
        => Run(quest => quest.OnClanChangedKingdom(clan, oldKingdom, newKingdom, actionDetail, showNotification));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementOwnerChanged))]
    [HarmonyPostfix]
    private static void Owner(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
        Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        => Run(quest => quest.OnSettlementOwnerChanged(settlement, openToClaim, newOwner, oldOwner, capturerHero, detail));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTickParty))]
    [HarmonyPostfix]
    private static void PartyTick(MobileParty mobileParty)
        => Run(quest => quest.OnPartyHourlyTick(mobileParty));

    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.HourlyTick))]
    [HarmonyPostfix]
    private static void Hourly()
    {
        Run(quest =>
        {
            if (quest.QuestDueTime.IsPast) quest.CompleteQuestWithTimeOut();
            else if (quest._questCaravanMobileParty.IsCaravan) quest.CompleteQuestWithCancel();
            else quest.HourlyTick();
        });
    }

    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.DailyTick))]
    [HarmonyPostfix]
    private static void Daily() => Run(quest => quest.DailyTick());

    private static void Run(Action<Quest> action)
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) return;
        dispatcher.RunAsOwners<Quest>(quest =>
        {
            if (quest._questCaravanMobileParty == null) return;
            if (!ContainerProvider.TryResolve<IEscortOwnerParty>(out var parties) ||
                !parties.TryGet(IssueManager.GetIssueOfQuest(quest), out var party))
            {
                quest.CompleteQuestWithCancel(new TextObject("Your caravan escort ended because your party is no longer available."));
                return;
            }
            var previous = CurrentQuest;
            CurrentQuest = quest;
            var before = Progress(quest);
            using (new MainHeroSubstitutionScope(Hero.MainHero, party))
            {
                try
                {
                    action(quest);
                    if (quest.IsOngoing && !before.Equals(Progress(quest)) &&
                        ContainerProvider.TryResolve<IEscortProgress>(out var progress))
                        progress.Publish(quest);
                }
                finally
                {
                    CurrentQuest = previous;
                }
            }
        });
    }

    private static object Progress(Quest quest) => (quest._daysSpentForEscorting,
        quest._visitedSettlements.Count, quest.JournalEntries.Count, quest._questBanditMobileParty,
        quest._otherBanditParty, quest._isPlayerNotifiedForDanger,
        Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(quest).Count);
}
