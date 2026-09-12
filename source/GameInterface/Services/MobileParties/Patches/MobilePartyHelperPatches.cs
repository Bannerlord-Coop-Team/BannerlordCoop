using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobilePartyHelper))]
internal class MobilePartyHelperPatches
{
    [HarmonyPatch(nameof(MobilePartyHelper.CreateNewClanMobileParty))]
    [HarmonyPrefix]
    public static bool PrefixCreateNewClanMobileParty(Hero hero, ref MobileParty __result)
    {
        var currentSettlement = hero.CurrentSettlement;

        if (currentSettlement == null) return true;

        // Replace hero.CurrentSettlement != null block to not use MainParty
        hero.PartyBelongedTo?.AddElementToMemberRoster(hero.CharacterObject, -1);

        __result = MobilePartyHelper.SpawnLordParty(hero, currentSettlement);

        return false;
    }
}