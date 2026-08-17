using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Alliances;

[HarmonyPatch(typeof(AllianceCampaignBehavior))]
internal class DisableAllianceCampaignBehavior
{
    [HarmonyPatch(nameof(AllianceCampaignBehavior.RegisterEvents))]
    static bool RegisterEventsPrefix() => true;

    [HarmonyPatch(nameof(AllianceCampaignBehavior.DailyTickClan))]
    [HarmonyPrefix]
    private static bool DailyTickClanPrefix()
    {
        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(AllianceCampaignBehavior.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix()
    {
        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(AllianceCampaignBehavior.OnMakePeace))]
    [HarmonyPrefix]
    private static bool OnMakePeacePrefix()
    {
        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(AllianceCampaignBehavior.OnKingdomDestroyed))]
    [HarmonyPrefix]
    private static bool OnKingdomDestroyedPrefix()
    {
        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(AllianceCampaignBehavior.OnGameLoadFinished))]
    [HarmonyPrefix]
    private static bool OnGameLoadFinished()
    {
        return ModInformation.IsServer;
    }
}
