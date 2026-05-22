using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEventParties.Patches;

[HarmonyPatch(typeof(MapEventParty))]
internal class MapEventPartyUpdatePatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(MapEventParty.Update))]
    static bool PrefixUpdate(MapEventParty __instance)
    {
        if (__instance._roster == null)
        {
            __instance._roster = new FlattenedTroopRoster(__instance.Party.MemberRoster.TotalManCount);
        }
        else
        {
            __instance._roster.Clear();
        
        }
        if (__instance.Party.MemberRoster._troopRosterElements == null) return false;

        foreach (TroopRosterElement troopRosterElement in __instance.Party.MemberRoster.GetTroopRoster())
        {
            if (troopRosterElement.Character.IsHero)
            {
                if (!__instance._woundedInBattle.Contains(troopRosterElement.Character) && !__instance._diedInBattle.Contains(troopRosterElement.Character))
                {
                    __instance._roster.Add(troopRosterElement.Character, troopRosterElement.Character.HeroObject.IsWounded, troopRosterElement.Xp);
                }
            }
            else
            {
                __instance._roster.Add(troopRosterElement.Character, troopRosterElement.Number, troopRosterElement.WoundedNumber);
            }
        }


        return false;
    }
}
