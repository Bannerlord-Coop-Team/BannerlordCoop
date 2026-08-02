using Common;
using Common.Messaging;
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
        if (ModInformation.IsServer) return true;

        var message = new RemoveAllPartyRolesOfHero(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(MobileParty.RemovePartyRoleOfHero))]
    [HarmonyPrefix]
    public static bool RemovePartyRoleOfHeroPrefix(MobileParty __instance, Hero hero, PartyRole partyRole)
    {
        if (ModInformation.IsServer) return true;

        var message = new RemovePartyRoleOfHero(hero, __instance, partyRole);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(MobileParty.RemoveOnePartyRoleOfHero))]
    [HarmonyPrefix]
    public static bool RemoveOnePartyRoleOfHeroPrefix(MobileParty __instance, Hero hero)
    {
        if (ModInformation.IsServer) return true;

        var message = new RemoveOnePartyRoleOfHero(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(MobileParty.SetPartyScout))]
    [HarmonyPrefix]
    public static bool SetPartyScoutPrefix(MobileParty __instance, Hero hero)
    {
        if (ModInformation.IsServer) return true;

        var message = new SetPartyScout(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(MobileParty.SetPartyQuartermaster))]
    [HarmonyPrefix]
    public static bool SetPartyQuartermasterPrefix(MobileParty __instance, Hero hero)
    {
        if (ModInformation.IsServer) return true;

        var message = new SetPartyQuartermaster(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(MobileParty.SetPartyEngineer))]
    [HarmonyPrefix]
    public static bool SetPartyEngineerPrefix(MobileParty __instance, Hero hero)
    {
        if (ModInformation.IsServer) return true;

        var message = new SetPartyEngineer(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(MobileParty.SetPartySurgeon))]
    [HarmonyPrefix]
    public static bool SetPartySurgeonPrefix(MobileParty __instance, Hero hero)
    {
        if (ModInformation.IsServer) return true;

        var message = new SetPartySurgeon(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(MobileParty.SetPartyFirstMate))]
    [HarmonyPrefix]
    public static bool SetPartyFirstMatePrefix(MobileParty __instance, Hero hero)
    {
        if (ModInformation.IsServer) return true;

        var message = new SetPartyFirstMate(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(MobileParty.SetPartyNavigator))]
    [HarmonyPrefix]
    public static bool SetPartyNavigatorPrefix(MobileParty __instance, Hero hero)
    {
        if (ModInformation.IsServer) return true;

        var message = new SetPartyNavigator(hero, __instance);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}
