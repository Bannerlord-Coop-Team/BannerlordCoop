using Common;
using HarmonyLib;
using SandBox;
using SandBox.CampaignBehaviors;
using SandBox.Missions.AgentBehaviors;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Follower dialogs and the settlement bodyguard only touch the local main party, location encounter
/// and mission agents, so they run on each client and never on the partyless server.
/// </summary>
[HarmonyPatch(typeof(ClanMemberRolesCampaignBehavior))]
internal class ClanMemberRolesCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(ClanMemberRolesCampaignBehavior.AddDialogs))]
    [HarmonyPrefix]
    private static bool AddDialogsPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(ClanMemberRolesCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    private static bool OnSettlementEnteredPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(ClanMemberRolesCampaignBehavior.OnSettlementLeft))]
    [HarmonyPrefix]
    private static bool OnSettlementLeftPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(ClanMemberRolesCampaignBehavior.OnMissionEnded))]
    [HarmonyPrefix]
    private static bool OnMissionEndedPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(ClanMemberRolesCampaignBehavior.OnHeroJoinedParty))]
    [HarmonyPrefix]
    private static bool OnHeroJoinedPartyPrefix() => ModInformation.IsClient;

    // Clients add a joining hero through the roster delta instead of AddHeroToPartyAction, so OnHeroJoinedPartyEvent never fires there.
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.OnHeroAdded))]
    [HarmonyPostfix]
    private static void OnHeroAddedPostfix(MobileParty __instance, Hero hero)
    {
        if (ModInformation.IsServer || !__instance.IsMainParty) return;

        Campaign.Current.GetCampaignBehavior<ClanMemberRolesCampaignBehavior>()?.OnHeroJoinedParty(hero, __instance);
    }

    // Vanilla adjusts the AI of any hero agent in the mission, but another player's companion is a puppet with no AgentNavigator.
    [HarmonyPatch(nameof(ClanMemberRolesCampaignBehavior.OnHeroGetsBusy))]
    [HarmonyPrefix]
    private static bool OnHeroGetsBusyPrefix(ClanMemberRolesCampaignBehavior __instance, Hero hero)
    {
        if (ModInformation.IsServer) return false;

        return __instance.IsFollowingPlayer(hero);
    }

    // Fires on every client for any clan's companion and would add it to this client's location as a MainParty character.
    [HarmonyPatch(nameof(ClanMemberRolesCampaignBehavior.OnNewCompanionAdded))]
    [HarmonyPrefix]
    private static bool OnNewCompanionAddedPrefix() => false;

    // The gatherer only matches a wandering companion's walking pace and can trail it for minutes, so the picked companion walks to the gatherer too.
    [HarmonyPatch(nameof(ClanMemberRolesCampaignBehavior.SelectTarget))]
    [HarmonyPostfix]
    private static void SelectTargetPostfix(Agent agent, ref Agent targetAgent, bool __result)
    {
        if (!__result || targetAgent == null) return;

        var behaviorGroup = targetAgent.GetComponent<CampaignAgentComponent>()?.AgentNavigator?.GetBehaviorGroup<DailyBehaviorGroup>();

        // OnTargetReached already scripted it to follow, overriding that would undo the hand-off.
        if (behaviorGroup == null || behaviorGroup.ScriptedBehavior is FollowAgentBehavior) return;

        ScriptBehavior.AddAgentTarget(targetAgent, agent);
    }

    // EnterSettlementActionPatches skips the enter action when the party is already inside (a rejoin), so SettlementEntered never picks the bodyguard.
    [HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.EnterSettlement))]
    [HarmonyPrefix]
    private static void EnterSettlementPrefix(out bool __state)
    {
        __state = ModInformation.IsClient && MobileParty.MainParty.CurrentSettlement == PlayerEncounter.EncounterSettlement;
    }

    [HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.EnterSettlement))]
    [HarmonyPostfix]
    private static void EnterSettlementPostfix(bool __state)
    {
        if (!__state) return;

        var campaignBehavior = Campaign.Current.GetCampaignBehavior<ClanMemberRolesCampaignBehavior>();
        if (campaignBehavior == null) return;

        campaignBehavior.OnSettlementEntered(MobileParty.MainParty, PlayerEncounter.EncounterSettlement, Hero.MainHero);
    }

    [HarmonyPatch(nameof(ClanMemberRolesCampaignBehavior.BeforeMissionOpened))]
    [HarmonyPrefix]
    private static bool BeforeMissionOpenedPrefix(ClanMemberRolesCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return false;
        if (PlayerEncounter.LocationEncounter == null) return true;

        // Party join and hero busy events only run on the server, and vanilla never drops wounded followers, so drop any that can no longer spawn.
        bool removed = false;
        foreach (Hero hero in __instance._isFollowingPlayer.ToList())
        {
            if (hero.PartyBelongedTo == MobileParty.MainParty && !hero.IsPrisoner && !hero.IsWounded) continue;

            __instance.RemoveAccompanyingHero(hero);
            removed = true;
        }

        if (removed && __instance._isFollowingPlayer.Count == 0)
        {
            __instance.UpdateAccompanyingCharacters();
        }

        return true;
    }
}
