using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(BeHostileAction))]
internal class BeHostileActionPatches
{
    [HarmonyPatch(nameof(BeHostileAction.ApplyEncounterHostileAction))]
    [HarmonyPostfix]
    public static void ApplyEncounterHostileActionPostfix(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (ModInformation.IsClient) return;

        if (Campaign.Current.Models.EncounterModel.IsEncounterExemptFromHostileActions(attackerParty, defenderParty))
        {
            return;
        }

        // Re-do check for player parties
        if (attackerParty.MobileParty != null && attackerParty.MobileParty.IsPlayerParty() && attackerParty.MapFaction != defenderParty.MapFaction && !FactionManager.IsAtWarAgainstFaction(attackerParty.MapFaction, defenderParty.MapFaction))
        {
            ChangeRelationAction.ApplyInternal(attackerParty.LeaderHero, defenderParty.MapFaction.Leader, -10, true, ChangeRelationAction.ChangeRelationDetail.Default);
            DeclareWarAction.ApplyByPlayerHostility(attackerParty.MapFaction, defenderParty.MapFaction);
        }
    }
}
