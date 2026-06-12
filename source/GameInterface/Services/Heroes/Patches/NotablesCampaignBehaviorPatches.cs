using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Buildings.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(NotablesCampaignBehavior))]
internal class NotablesCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(NotablesCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NotablesCampaignBehavior.UpdateNotableRelations))]
    [HarmonyPrefix]
    public static bool UpdateNotableRelationsPrefix(ref NotablesCampaignBehavior __instance, Hero notable)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Replace implementation to check for player clans instead of using static Clan.PlayerClan
        var message = new UpdateNotableRelations(notable);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(NotablesCampaignBehavior.UpdateNotableSupport))]
    [HarmonyPrefix]
    public static bool UpdateNotableSupportPrefix(ref NotablesCampaignBehavior __instance, Hero notable)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Replace implementation to check for player clans instead of using static Clan.PlayerClan
        var message = new UpdateNotableSupport(notable);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}