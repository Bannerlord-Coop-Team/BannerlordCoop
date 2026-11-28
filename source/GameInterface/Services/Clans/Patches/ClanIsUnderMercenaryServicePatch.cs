using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Patches;

// Clan.StartMercenaryService and EndMercenaryService only set IsUnderMercenaryService, so the JIT inlines them into
// callers compiled before they are patched. Publish from the mercenary events vanilla raises right after them instead.
[HarmonyPatch(typeof(CampaignEventDispatcher))]
internal class ClanIsUnderMercenaryServicePatch
{
    [HarmonyPatch(nameof(CampaignEventDispatcher.OnMercenaryServiceStarted))]
    [HarmonyPostfix]
    private static void OnMercenaryServiceStartedPostfix(Clan mercenaryClan) => PublishMercenaryService(mercenaryClan);

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnMercenaryServiceEnded))]
    [HarmonyPostfix]
    private static void OnMercenaryServiceEndedPostfix(Clan mercenaryClan) => PublishMercenaryService(mercenaryClan);

    private static void PublishMercenaryService(Clan clan)
    {
        if (!ModInformation.IsServer) return;

        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        MessageBroker.Instance.Publish(clan, new ClanMercenaryServiceChanged(clan, clan.IsUnderMercenaryService));
    }
}
