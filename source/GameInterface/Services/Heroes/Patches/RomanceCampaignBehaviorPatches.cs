using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages.RomanceFlow;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(RomanceCampaignBehavior), nameof(RomanceCampaignBehavior.DailyTick))]
internal class RomanceCampaignBehaviorPatches
{
    [HarmonyPrefix]
    private static void DailyTickPrefix(out int __state)
    {
        __state = Romance.RomanticStateList?.Count ?? 0;
    }

    [HarmonyPostfix]
    private static void DailyTickPostfix(int __state)
    {
        if (!ModInformation.IsServer) return;
        if ((Romance.RomanticStateList?.Count ?? 0) == __state) return;

        MessageBroker.Instance.Publish(null, new RomanceStatesChanged());
    }
}
