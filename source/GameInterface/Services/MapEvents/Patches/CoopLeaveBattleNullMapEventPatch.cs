using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Guards the vanilla encounter "can I leave the battle?" condition against a NULL map event at a coop battle finish.
/// <para>
/// When a coop battle concludes, the server's <c>MapEvent.FinalizeEventAux</c> removes the event from every
/// involved party, and that removal is synced to the clients — so on the host <c>MobileParty.MainParty.MapEvent</c>
/// becomes <c>null</c> BEFORE its post-battle encounter menu is dismissed (the close is deferred until the mission
/// finishes tearing down). The menu keeps re-evaluating its leave option each tick, and
/// <see cref="MapEventHelper.CanMainPartyLeaveBattleCommonCondition"/> dereferences
/// <c>MainParty.MapEvent.PlayerSide</c> → <see cref="System.NullReferenceException"/> every frame (caught by the
/// OnTick robustness finalizer, but the host is left NRE-spamming on a stuck post-battle menu).
/// </para>
/// With no map event the battle is already over, so report "can leave" and let the deferred encounter close (or the
/// player's own Leave) dismiss the menu. Only changes behaviour when the map event is null — a state vanilla never
/// reaches while this condition is evaluated — so single-player is untouched.
/// </summary>
[HarmonyPatch(typeof(MapEventHelper), nameof(MapEventHelper.CanMainPartyLeaveBattleCommonCondition))]
internal class CoopLeaveBattleNullMapEventPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (MobileParty.MainParty?.MapEvent != null)
            return true; // normal case — run the original condition

        __result = true; // concluded coop battle (no map event) → leaving is allowed
        return false;    // skip the original, which would NRE on MainParty.MapEvent.PlayerSide
    }
}
