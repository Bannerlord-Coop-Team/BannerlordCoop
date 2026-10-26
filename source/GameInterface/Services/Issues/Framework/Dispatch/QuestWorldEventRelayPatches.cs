using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class QuestWorldEventRelayPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix]
    private static void OnWarDeclaredPostfix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
    {
        Relay("OnWarDeclared", new[] { typeof(IFaction), typeof(IFaction), typeof(DeclareWarAction.DeclareWarDetail) }, faction1, faction2, declareWarDetail);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroPrisonerTaken))]
    [HarmonyPostfix]
    private static void OnHeroPrisonerTakenPostfix(PartyBase capturer, Hero prisoner)
    {
        Relay("OnHeroPrisonerTaken", new[] { typeof(PartyBase), typeof(Hero) }, capturer, prisoner);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void OnClanChangedKingdomPostfix(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail actionDetail, bool showNotification)
    {
        Relay(
            "OnClanChangedKingdom",
            new[] { typeof(Clan), typeof(Kingdom), typeof(Kingdom), typeof(ChangeKingdomAction.ChangeKingdomActionDetail), typeof(bool) },
            clan, oldKingdom, newKingdom, actionDetail, showNotification);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementOwnerChanged))]
    [HarmonyPostfix]
    private static void OnSettlementOwnerChangedPostfix(
        Settlement settlement,
        bool openToClaim,
        Hero newOwner,
        Hero oldOwner,
        Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        Relay(
            "OnSettlementOwnerChanged",
            new[] { typeof(Settlement), typeof(bool), typeof(Hero), typeof(Hero), typeof(Hero), typeof(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail) },
            settlement, openToClaim, newOwner, oldOwner, capturerHero, detail);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnArmyDispersed))]
    [HarmonyPostfix]
    private static void OnArmyDispersedPostfix(Army army, Army.ArmyDispersionReason reason, bool isPlayersArmy)
    {
        Relay("OnArmyDispersed", new[] { typeof(Army), typeof(Army.ArmyDispersionReason), typeof(bool) }, army, reason, isPlayersArmy);
    }

    private static void Relay(string listenerName, System.Type[] parameterTypes, params object[] arguments)
    {
        if (!ContainerProvider.TryResolve<IQuestEventRelay>(out var relay))
        {
            return;
        }

        relay.Relay(listenerName, parameterTypes, arguments);
    }
}
