using Common;
using GameInterface.Extentions;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(RaftStateCampaignBehavior))]
internal class RaftStateCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(RaftStateCampaignBehavior.OnMapEventEnded))]
    [HarmonyPrefix]
    public static bool OnMapEventEndedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(RaftStateCampaignBehavior.OnShipDestroyed))]
    [HarmonyPrefix]
    public static bool OnShipDestroyedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(RaftStateCampaignBehavior.OnPartyLeftArmy))]
    [HarmonyPrefix]
    public static bool OnPartyLeftArmyPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(RaftStateCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix(MobileParty mobileParty)
    {
        if (ModInformation.IsClient) return false;

        // Vanilla ends MobileParty.MainParty's raft state here instead of using the party
        if (mobileParty != null && mobileParty.IsInRaftState)
        {
            RaftStateChangeAction.DeactivateRaftStateForParty(mobileParty);
        }

        return false;
    }

    [HarmonyPatch(nameof(RaftStateCampaignBehavior.OnHeroPrisonerTaken))]
    [HarmonyPrefix]
    public static bool OnHeroPrisonerTakenPrefix(Hero hero)
    {
        if (ModInformation.IsClient) return false;
        if (!hero.IsPlayerHero()) return false;

        // The hero has already left their party here, so find it by owner
        var playerParty = Campaign.Current.CampaignObjectManager.GetPlayerMobileParties().FirstOrDefault(party => party.Owner == hero);

        if (playerParty != null && playerParty.IsInRaftState)
        {
            RaftStateChangeAction.DeactivateRaftStateForParty(playerParty);
        }

        return false;
    }

    [HarmonyPatch(nameof(RaftStateCampaignBehavior.OnHeroPrisonerReleased))]
    [HarmonyPrefix]
    public static bool OnHeroPrisonerReleasedPrefix(Hero prisoner)
    {
        // Clients need the raft state menu, open it if the prisoner is their player character
        if (ModInformation.IsClient) return prisoner == Hero.MainHero;

        // Server calls MakeHeroFugitiveAction.Apply for non player heroes
        return !prisoner.IsPlayerHero();
    }

    [HarmonyPatch(nameof(RaftStateCampaignBehavior.OnPlayerCharacterChanged))]
    [HarmonyPrefix]
    public static bool OnPlayerCharacterChangedPrefix() => ModInformation.IsServer;
}
