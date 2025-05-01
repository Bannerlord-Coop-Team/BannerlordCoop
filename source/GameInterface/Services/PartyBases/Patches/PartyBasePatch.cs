using GameInterface.Services.ItemRosters;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyBases.Patches;

[HarmonyPatch(typeof(PartyBase))]
internal class PartyBasePatch
{

    [HarmonyPatch(MethodType.Constructor, typeof(MobileParty), typeof(Settlement))]
    [HarmonyPrefix]
    private static bool Ctor(PartyBase __instance, MobileParty mobileParty, Settlement settlement)
    {
        __instance.Index = Campaign.Current.GeneratePartyId(__instance);
        __instance.MobileParty = mobileParty;
        __instance.Settlement = settlement;
        __instance.ItemRoster = new ItemRoster();
        __instance.MemberRoster = new TroopRoster(__instance);
        __instance.PrisonRoster = new TroopRoster(__instance);
        __instance.MemberRoster.NumberChangedCallback = new NumberChangedCallback(__instance.MemberRosterNumberChanged);
        __instance.PrisonRoster.IsPrisonRoster = true;

        return false;
    }
}

[HarmonyPatch(typeof(PartyBaseHelper))]
internal class PartyBaseHelperPatch
{
    [HarmonyPatch(nameof(PartyBaseHelper.HasFeat))]
    [HarmonyPrefix]
    public static bool HasFeat(PartyBase party, FeatObject feat, ref bool __result)
    {
        if (party == null)
            __result = false;
        if (party.LeaderHero != null)
            __result = party.LeaderHero.Culture.HasFeat(feat);
        if (party.Culture != null)
            __result = party.Culture.HasFeat(feat);
        if (party.Owner != null)
            __result = party.Owner.Culture.HasFeat(feat);
        __result = party.Settlement != null && party.Settlement.Culture.HasFeat(feat);

        return false;
    }
}


