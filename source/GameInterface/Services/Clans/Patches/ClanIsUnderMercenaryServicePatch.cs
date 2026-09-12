using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(Clan))]
internal class ClanIsUnderMercenaryServicePatch
{
    [HarmonyPatch(nameof(Clan.StartMercenaryService))]
    [HarmonyPostfix]
    private static void StartMercenaryServicePostfix(Clan __instance) => PublishMercenaryService(__instance);

    [HarmonyPatch(nameof(Clan.EndMercenaryService))]
    [HarmonyPostfix]
    private static void EndMercenaryServicePostfix(Clan __instance) => PublishMercenaryService(__instance);

    private static void PublishMercenaryService(Clan clan)
    {
        if (!ModInformation.IsServer) return;

        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        MessageBroker.Instance.Publish(clan, new ClanMercenaryServiceChanged(clan, clan.IsUnderMercenaryService));
    }
}
