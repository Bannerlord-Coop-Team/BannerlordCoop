using GameInterface.Services.ItemRosters;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyBases.Patches;

[HarmonyPatch(typeof(PartyBase))]
internal class PartyBasePatch
{
    [HarmonyPatch(nameof(PartyBase.ItemRoster), MethodType.Setter)]
    [HarmonyPostfix]
    public static void ItemRosterSetterPostfix(PartyBase __instance)
    {
        if (ModInformation.IsClient) return;

        ItemRosterLookup.Set(__instance.ItemRoster, __instance);
    }

    [HarmonyPatch(typeof(PartyBase), MethodType.Constructor, new Type[] { typeof(MobileParty), typeof(Settlement) } )]
    [HarmonyPriority(Priority.Low)]
    [HarmonyPrefix]
    public static bool CtorPrefix(PartyBase __instance, MobileParty mobileParty, Settlement settlement)
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
