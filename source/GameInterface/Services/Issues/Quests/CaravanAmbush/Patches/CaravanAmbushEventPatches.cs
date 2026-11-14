using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class CaravanAmbushEventPatches
{
    // Dispatcher postfixes below replace these native quest listeners.
    [HarmonyPatch(typeof(Quest), nameof(Quest.MapEventEnded))]
    [HarmonyPrefix]
    private static bool NativeBattleEnded() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementEntered))]
    [HarmonyPrefix]
    private static bool NativeArrived() => false;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnBanditPartyRecruited))]
    [HarmonyPrefix]
    private static bool NativeRecruited() => false;

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void Hourly()
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICaravanAmbushWorldEventService>(out var events)) events.HourlyTick();
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void BattleStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICaravanAmbushWorldEventService>(out var events)) events.MapEventStarted(mapEvent, attackerParty, defenderParty);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void BattleEnded(MapEvent mapEvent)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICaravanAmbushWorldEventService>(out var events)) events.BattleEnded(mapEvent);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnBanditPartyRecruited))]
    [HarmonyPostfix]
    private static void Recruited(MobileParty banditParty)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICaravanAmbushWorldEventService>(out var events)) events.BanditsRecruited(banditParty);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementEntered))]
    [HarmonyPostfix]
    private static void Arrived(MobileParty party, Settlement settlement, Hero hero)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICaravanAmbushWorldEventService>(out var events)) events.SettlementEntered(party);
    }
}
