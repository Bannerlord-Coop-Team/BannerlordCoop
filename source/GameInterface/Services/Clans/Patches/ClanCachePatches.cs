using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(Clan))]
internal class ClanCachePatches
{
    [HarmonyPatch(nameof(Clan.OnWarPartyAdded))]
    [HarmonyPrefix]
    public static bool OnWarPartyAddedPrefix(ref Clan __instance, WarPartyComponent warPartyComponent)
    {
        // ActualClan sync and the cache message can both apply the same membership change.
        if (__instance.WarPartyComponents.Contains(warPartyComponent)) return false;

        if (ModInformation.IsClient) return CallOriginalPolicy.IsOriginalAllowed();

        var message = new WarPartyAdded(__instance, warPartyComponent);
        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }

    [HarmonyPatch(nameof(Clan.OnWarPartyRemoved))]
    [HarmonyPrefix]
    public static bool OnWarPartyRemovedPrefix(ref Clan __instance, WarPartyComponent warPartyComponent)
    {
        if (!__instance.WarPartyComponents.Contains(warPartyComponent)) return false;

        if (ModInformation.IsClient) return CallOriginalPolicy.IsOriginalAllowed();

        var message = new WarPartyRemoved(__instance, warPartyComponent);
        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }

    [HarmonyPatch(nameof(Clan.OnSupporterNotableAdded))]
    [HarmonyPrefix]
    public static bool OnSupporterNotableAddedPrefix(ref Clan __instance, Hero hero)
    {
        if (ModInformation.IsClient) return CallOriginalPolicy.IsOriginalAllowed();

        var message = new SupporterNotableAdded(__instance, hero);
        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }

    [HarmonyPatch(nameof(Clan.OnSupporterNotableRemoved))]
    [HarmonyPrefix]
    public static bool OnSupporterNotableRemovedPrefix(ref Clan __instance, Hero hero)
    {
        if (ModInformation.IsClient) return CallOriginalPolicy.IsOriginalAllowed();

        var message = new SupporterNotableRemoved(__instance, hero);
        MessageBroker.Instance.Publish(__instance, message);
        return true;
    }
}
