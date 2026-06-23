using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Patches;

// Clan.Renown is a trivial auto-property; the JIT inlines its setter into the methods that write it
// (Clan.AddRenown, ResetClanRenown), so the AutoSync setter prefix never runs and renown never replicates.
// Publish the new absolute renown from the (non-inlined) writer methods on the server instead; clients apply it
// in ClanRenownHandler. The Renown AutoSync property registration is removed in ClanSync.
[HarmonyPatch(typeof(Clan))]
internal class ClanRenownPatch
{
    [HarmonyPatch(nameof(Clan.AddRenown))]
    [HarmonyPostfix]
    private static void Postfix_AddRenown(Clan __instance) => PublishRenown(__instance);

    [HarmonyPatch(nameof(Clan.ResetClanRenown))]
    [HarmonyPostfix]
    private static void Postfix_ResetClanRenown(Clan __instance) => PublishRenown(__instance);

    private static void PublishRenown(Clan clan)
    {
        if (!ModInformation.IsServer) return;

        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (string.IsNullOrEmpty(clan?.StringId)) return;

        MessageBroker.Instance.Publish(clan, new ClanRenownChanged(clan.StringId, clan.Renown));
    }
}
