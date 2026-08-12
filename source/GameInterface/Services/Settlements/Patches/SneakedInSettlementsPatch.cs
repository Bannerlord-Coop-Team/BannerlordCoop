using Common;
using Common.Messaging;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal class SneakedInSettlementsPatch
{
    [HarmonyPatch(nameof(EncounterGameMenuBehavior.AddCurrentSettlementAsAlreadySneakedIn))]
    [HarmonyPostfix]
    public static void AddCurrentSettlementAsAlreadySneakedInPostfix(EncounterGameMenuBehavior __instance)
    {
        if (ModInformation.IsServer) return;

        // Send to server to persist in CoopSession
        var message = new AddSettlementAsSneakedIn(Hero.MainHero, Settlement.CurrentSettlement);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
