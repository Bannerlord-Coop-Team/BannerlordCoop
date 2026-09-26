using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.SiegeEvents.Patches;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents;

public interface ISiegeBreakOut
{
    TroopRoster ApplySacrifice(MobileParty party, out int armyCasualties);
    void ShowDebrief(TroopRoster casualties, int armyCasualties);
    void Continue();
    void ProtectAfterLeave(MobileParty party, Settlement settlement);
}

public class SiegeBreakOut : ISiegeBreakOut
{
    public TroopRoster ApplySacrifice(MobileParty party, out int armyCasualties)
    {
        var casualties = TroopRoster.CreateDummyTroopRoster();
        var army = party.Army;
        bool leadsArmy = army != null && army.LeaderParty == party;
        armyCasualties = leadsArmy ? 0 : -1;
        var parties = leadsArmy ? army.Parties.ToArray() : new[] { party };
        var model = Campaign.Current.Models.TroopSacrificeModel;
        int losses = model.GetLostTroopCountForBreakingOutOfBesiegedSettlement(
            party, party.CurrentSettlement.SiegeEvent, false).RoundedResultNumber;
        int available = parties.Sum(member => member.MemberRoster.TotalRegulars);
        losses = Math.Min(Math.Max(0, losses), available);

        // The server selects the sacrificed troops once; normal roster patches replicate the losses.
        for (int loss = 0; loss < losses; loss++)
        {
            if (!leadsArmy)
            {
                var roster = party.MemberRoster;
                int index;
                do { index = MBRandom.RandomInt(roster.Count); }
                while (!roster.GetCharacterAtIndex(index).IsRegular || roster.GetElementNumber(index) == 0);
                var character = roster.GetCharacterAtIndex(index);
                roster.AddToCountsAtIndex(index, -1);
                casualties.AddToCounts(character, 1);
                continue;
            }
            int selected = MBRandom.RandomInt(available--);
            foreach (var member in parties)
            {
                var roster = member.MemberRoster;
                int count = roster.TotalRegulars;
                if (selected >= count)
                {
                    selected -= count;
                    continue;
                }

                for (int index = 0; index < roster.Count; index++)
                {
                    var element = roster.GetElementCopyAtIndex(index);
                    if (!element.Character.IsRegular) continue;
                    if (selected >= element.Number)
                    {
                        selected -= element.Number;
                        continue;
                    }

                    roster.AddToCountsAtIndex(index, -1);
                    if (member == party) casualties.AddToCounts(element.Character, 1);
                    else armyCasualties++;
                    break;
                }
                break;
            }
        }

        if (army != null && !leadsArmy)
        {
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                party.LeaderHero, army.LeaderParty.LeaderHero, model.BreakOutArmyLeaderRelationPenalty);
            foreach (var member in army.LeaderParty.AttachedParties)
            {
                if (member != party && member.LeaderHero != null)
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                        party.LeaderHero, member.LeaderHero, model.BreakOutArmyMemberRelationPenalty);
            }
            party.Army = null;
        }
        return casualties;
    }

    public void ShowDebrief(TroopRoster casualties, int armyCasualties)
    {
        var behavior = Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();
        behavior._breakInOutCasualties = casualties;
        behavior._breakInOutArmyCasualties = armyCasualties;
        behavior._isBreakingOutFromPort = false;
        GameMenu.SwitchToMenu("break_out_debrief_menu");
    }

    public void ProtectAfterLeave(MobileParty party, Settlement settlement)
    {
        party.Position = settlement.GatePosition;
        party.TeleportPartyToOutSideOfEncounterRadius();
        party.SetMoveModeHold();
        party.IgnoreForHours(1f);
    }

    public void Continue()
    {
        // Clear the defend order before closing menus so the same siege cannot reopen the encounter.
        SiegeEntryFlowPatches.HoldAfterSiegeLeave(MobileParty.MainParty);
        if (PlayerSiege.PlayerSiegeEvent != null) PlayerSiege.FinalizePlayerSiege();
        PlayerLeaveSettlementPatch.RequestLeave();
    }
}
