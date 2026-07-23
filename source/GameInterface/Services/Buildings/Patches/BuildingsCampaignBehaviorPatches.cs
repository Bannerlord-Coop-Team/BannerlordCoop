using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Buildings.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Buildings.Patches;

[HarmonyPatch(typeof(BuildingsCampaignBehavior))]
internal class BuildingsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(BuildingsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(BuildingsCampaignBehavior.OnSettlementOwnerChanged))]
    [HarmonyPrefix]
    public static bool OnSettlementOwnerChangedPrefix(Settlement settlement, Hero newOwner)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Replace implementation to check for player clans instead of using static Clan.PlayerClan
        var message = new OnSettlementOwnerChanged(settlement, newOwner);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }

    [HarmonyPatch(nameof(BuildingsCampaignBehavior.DailyTickSettlement))]
    [HarmonyPrefix]
    public static bool DailyTickSettlementPrefix(ref BuildingsCampaignBehavior __instance, Settlement settlement)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Replace implementation to check for player clans instead of using static Clan.PlayerClan
        var message = new BuildingsDailySettlementTick(__instance, settlement);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }
}
