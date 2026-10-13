using Common;
using GameInterface;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Players.Interfaces;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
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

        if (!ContainerProvider.TryResolve<IPlayerPartyLookup>(out var playerPartyLookup)) return false;

        // The hero has already left their party here, so find it through the player registry
        if (playerPartyLookup.TryGetPlayerParty(hero, out var playerParty) && playerParty.IsInRaftState)
        {
            RaftStateChangeAction.DeactivateRaftStateForParty(playerParty);
        }

        return false;
    }

    [HarmonyPatch(nameof(RaftStateCampaignBehavior.OnHeroPrisonerReleased))]
    [HarmonyPrefix]
    public static bool OnHeroPrisonerReleasedPrefix(Hero prisoner)
    {
        // PlayerRaftHandler strands a released player at sea on the server, and the synced raft state opens the client's raft menu
        if (ModInformation.IsClient) return false;

        // Server calls MakeHeroFugitiveAction.Apply for non player heroes
        return !prisoner.IsPlayerHero();
    }

    [HarmonyPatch(nameof(RaftStateCampaignBehavior.OnPlayerCharacterChanged))]
    [HarmonyPrefix]
    public static bool OnPlayerCharacterChangedPrefix() => ModInformation.IsServer;
}
