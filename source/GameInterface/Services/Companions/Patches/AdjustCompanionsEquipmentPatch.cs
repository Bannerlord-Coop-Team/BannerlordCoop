using Common;
using Common.Messaging;
using GameInterface.Services.Companions.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Companions.Patches;

[HarmonyPatch(typeof(CompanionRolesCampaignBehavior))]
internal class AdjustCompanionsEquipmentPatch
{
    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.AdjustCompanionsEquipment))]
    [HarmonyPostfix]
    public static void AdjustCompanionsEquipmentPostfix(CompanionRolesCampaignBehavior __instance, Hero companionHero)
    {
        if (ModInformation.IsClient) return;

        var message = new AdjustCompanionsEquipment(companionHero);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
