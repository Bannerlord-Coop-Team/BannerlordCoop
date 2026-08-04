using Common;
using Common.Messaging;
using GameInterface.Policies;
using Common.Util;
using GameInterface.Services.MapEvents.Messages.Retreat;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Routes "Try to get away." through the server instead of letting the client charge itself.
/// </summary>
/// <remarks>
/// Vanilla's accept consequence removes troops, destroys 15% of the party's goods, clears the besieger
/// camp, teleports the party clear and sets an ignore timer - all client-local writes that replicate
/// nothing, so only the retreating machine believed any of it happened. Worse, its
/// RemoveTroopsForTryToGetAway spreads the loss over the army leader's AttachedParties, which in co-op
/// means one player deleting another player's troops.
///
/// The client now only announces the intent; the server validates ownership, applies the whole cost to the
/// REQUESTING party, and the result arrives as ordinary roster/item deltas plus a behaviour snapshot for
/// the teleport. The player stays on the current menu until the verdict lands.
/// </remarks>
[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal class TryToGetAwayPatches
{
    [HarmonyPatch("game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence")]
    [HarmonyPrefix]
    private static bool AcceptPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        var mainParty = MobileParty.MainParty;
        if (mainParty == null) return true;

        var battle = mainParty.MapEvent ?? PlayerEncounter.Battle;
        if (battle != null)
        {
            // Retreating out of a live battle: the server owns the whole cost.
            MessageBroker.Instance.Publish(mainParty, new BattleRetreatAttempted(mainParty, battle));
            return false;
        }

        // No battle to retreat from - this is the besieging-camp shape, where vanilla's body is only the
        // camp write plus the debrief menu. Keep the established routing for it: the native flow owns its
        // menus, so the approval must not finish them, and the local clear stops native's guarded write
        // from re-running.
        if (mainParty.BesiegerCamp == null) return true;

        MessageBroker.Instance.Publish(null, new BreakSiegeAttempted(mainParty, finishLocalMenus: false));
        using (new AllowedThread())
        {
            mainParty.BesiegerCamp = null;
        }

        return true;
    }

    [HarmonyPatch("game_menu_try_to_get_away_end")]
    [HarmonyPrefix]
    private static bool DebriefEndPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Its whole body is shared state - clearing BesiegerCamp on EVERY defender-side party, the
        // diplomatic finish, ProtectPlayerSide - which one retreating player must not apply to the others.
        // The server already did the owned parts.
        return ModInformation.IsServer;
    }
}
