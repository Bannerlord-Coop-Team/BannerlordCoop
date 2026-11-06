using Common;
using GameInterface;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(RaftStateCampaignBehavior))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
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

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return false;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;

        // The hero has already left their party here, so find it through the player registry
        if (playerManager.TryGetPlayer(hero, out var player)
            && objectManager.TryGetObjectWithLogging(player.MobilePartyId, out MobileParty playerParty)
            && playerParty.IsInRaftState)
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

    // The server owns the raft state, so Continue only moves on to the wait menu on clients
    [HarmonyPatch(nameof(RaftStateCampaignBehavior.player_raft_state_continue_on_consequence))]
    [HarmonyPrefix]
    public static bool PlayerRaftStateContinueOnConsequencePrefix()
    {
        GameMenu.SwitchToMenu("player_raft_state_wait");
        return false;
    }

    [HarmonyPatch(nameof(RaftStateCampaignBehavior.OnMobilePartyRaftStateChanged))]
    [HarmonyPrefix]
    public static bool OnMobilePartyRaftStateChangedPrefix(MobileParty mobileParty)
    {
        if (ModInformation.IsServer) return false;

        // Vanilla leaves the first raft menu open when the server lands the party
        if (!mobileParty.IsInRaftState && mobileParty.IsMainParty &&
            Campaign.Current.CurrentMenuContext?.GameMenu?.StringId == "player_raft_state")
        {
            GameMenu.SwitchToMenu("player_raft_state_wait");
            return false;
        }

        return true;
    }
}
