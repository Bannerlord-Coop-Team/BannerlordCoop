using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages.Roles;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class PartyRolesPatches
{
    [HarmonyPatch(nameof(MobileParty.RemoveAllPartyRolesOfHero))]
    [HarmonyPrefix]
    public static bool RemoveAllPartyRolesOfHeroPrefix(MobileParty __instance, Hero hero)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new AllPartyRolesOfHeroRemoved(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(MobileParty.RemovePartyRoleOfHero))]
    [HarmonyPrefix]
    public static bool RemovePartyRoleOfHeroPrefix(MobileParty __instance, Hero hero, PartyRole partyRole)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new PartyRoleOfHeroRemoved(hero, __instance, partyRole);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(MobileParty.SetPartyScout))]
    [HarmonyPrefix]
    public static bool SetPartyScoutPrefix(MobileParty __instance, Hero hero)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new PartyScoutSet(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(MobileParty.SetPartyQuartermaster))]
    [HarmonyPrefix]
    public static bool SetPartyQuartermasterPrefix(MobileParty __instance, Hero hero)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new PartyQuartermasterSet(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(MobileParty.SetPartyEngineer))]
    [HarmonyPrefix]
    public static bool SetPartyEngineerPrefix(MobileParty __instance, Hero hero)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new PartyEngineerSet(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(nameof(MobileParty.SetPartySurgeon))]
    [HarmonyPrefix]
    public static bool SetPartySurgeonPrefix(MobileParty __instance, Hero hero)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new PartySurgeonSet(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    // TODO: Warsails integration
    /*
    [HarmonyPatch(nameof(MobileParty.SetPartyFirstMate))]
    [HarmonyPrefix]
    public static bool SetPartyFirstMatePrefix(MobileParty __instance, Hero hero)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new PartyFirstMateSet(hero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(MobileParty.SetPartyNavigator))]
    [HarmonyPrefix]
    public static bool SetPartyNavigatorPrefix(MobileParty __instance, Hero hero)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new PartyPartyNavigatorSet(hero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
    */
}
