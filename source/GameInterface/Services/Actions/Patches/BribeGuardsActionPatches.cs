using Common;
using Common.Messaging;
using GameInterface.Services.Actions.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(BribeGuardsAction))]
internal class BribeGuardsActionPatches
{
    [HarmonyPatch(nameof(BribeGuardsAction.ApplyInternal))]
    [HarmonyPrefix]
    public static bool ApplyInternalPrefix(Settlement settlement, int gold)
    {
        // Block on server, only clients should be starting a player bribe
        if (ModInformation.IsServer) return false;

        // Don't update with no gold change like in vanilla
        if (gold <= 0) return false;

        var message = new PlayerBribesGuard(Hero.MainHero, settlement, gold);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }
}
