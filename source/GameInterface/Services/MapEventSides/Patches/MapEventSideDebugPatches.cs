using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;

namespace GameInterface.Services.MapEventSides.Patches;

[HarmonyPatch(typeof(MapEventSide))]
internal class MapEventSideDebugPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(MapEventSide.HandleMapEventEnd))]
    static bool Prefix_HandleMapEventEnd(ref MapEventSide __instance)
    {
        while (__instance.Parties.Count > 0)
        {
            MapEventParty mapEventParty = __instance.Parties.FirstOrDefault((MapEventParty x) => !x.Party.IsMobile || x.Party.MobileParty.Army == null || x.Party.MobileParty.Army.LeaderParty != x.Party.MobileParty) ?? __instance.Parties[__instance.Parties.Count - 1];
            __instance.HandleMapEventEndForPartyInternal(mapEventParty.Party);
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MapEventSide.HandleMapEventEndForPartyInternal))]
    static bool Prefix_HandleMapEventEndForPartyInternal(ref MapEventSide __instance, PartyBase party)
    {
        IEnumerable<TroopRosterElement> enumerable = party.MemberRoster.GetTroopRoster().WhereQ((TroopRosterElement x) => x.Character.IsHero && x.Character.HeroObject.IsAlive && x.Character.HeroObject.DeathMark == KillCharacterAction.KillCharacterActionDetail.DiedInBattle);
        PartyBase leaderParty = __instance._mapEvent.GetLeaderParty(party.OpponentSide);
        bool flag = __instance._mapEvent.IsWinnerSide(party.Side);
        party.MapEventSide = null;
        foreach (TroopRosterElement troopRosterElement in enumerable)
        {
            KillCharacterAction.ApplyByBattle(troopRosterElement.Character.HeroObject, __instance.OtherSide.LeaderParty.LeaderHero, true);
        }
        if (party.IsMobile && party != PartyBase.MainParty && party.IsActive && (party.NumberOfAllMembers == 0 || (!flag && !__instance.MapEvent.EndedByRetreat && (party.NumberOfHealthyMembers == 0 || (__instance._mapEvent.BattleState != BattleState.None && party.MobileParty.IsMilitia)) && (party.MobileParty.Army == null || party.MobileParty.Army.LeaderParty.Party.NumberOfHealthyMembers == 0))) && (!party.MobileParty.IsDisbanding || party.MemberRoster.Count == 0))
        {
            if (party.LeaderHero != null)
            {
                party.LeaderHero.ChangeState(Hero.CharacterStates.Fugitive);
            }
            DestroyPartyAction.Apply(leaderParty, party.MobileParty);
        }
        party.MemberRoster.RemoveZeroCounts();
        party.PrisonRoster.RemoveZeroCounts();
        if (party.IsMobile && party.MobileParty.IsActive && party.MobileParty.CurrentSettlement == null)
        {
            party.SetVisualAsDirty();
        }

        return false;
    }
}
