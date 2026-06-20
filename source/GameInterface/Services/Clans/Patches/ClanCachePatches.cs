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
    public static void OnWarPartyAddedPostfix(ref Clan __instance, WarPartyComponent warPartyComponent)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed()) return;

        var message = new WarPartyAdded(__instance, warPartyComponent);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(Clan.OnWarPartyRemoved))]
    [HarmonyPrefix]
    public static void OnWarPartyRemovedPostfix(ref Clan __instance, WarPartyComponent warPartyComponent)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed()) return;

        var message = new WarPartyRemoved(__instance, warPartyComponent);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(Clan.OnSupporterNotableAdded))]
    [HarmonyPrefix]
    public static void OnSupporterNotableAddedPostfix(ref Clan __instance, Hero hero)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed()) return;

        var message = new SupporterNotableAdded(__instance, hero);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(Clan.OnSupporterNotableRemoved))]
    [HarmonyPrefix]
    public static void OnSupporterNotableRemovedPostfix(ref Clan __instance, Hero hero)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed()) return;

        var message = new SupporterNotableRemoved(__instance, hero);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
