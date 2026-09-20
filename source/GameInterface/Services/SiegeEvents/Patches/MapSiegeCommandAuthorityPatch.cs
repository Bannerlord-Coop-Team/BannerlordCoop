using Common;
using HarmonyLib;
using SandBox.ViewModelCollection.MapSiege;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Lets any joined player defender open the siege production popup. Vanilla gates both the map-circle
/// click (OnSelectionFromScene) and the popup itself (OnPOISelection) on the single top leader matching
/// Hero.MainHero, so only the first joiner gets a popup. Presence alone is not enough: parties inside
/// are involved automatically, so everyone (including the vanilla top leader) also needs the local join
/// from the "Join the defense" button. The flag stays vanilla for attackers and the server.
/// </summary>
[HarmonyPatch(typeof(MapSiegeVM), "get_IsPlayerLeaderOfSiegeEvent")]
internal static class MapSiegeCommandAuthorityPatch
{
    [HarmonyPostfix]
    internal static void Postfix(ref bool __result)
    {
        try
        {
            if (ModInformation.IsServer) return;

            BattleSideEnum side;
            try { side = PlayerSiege.PlayerSide; }
            catch { return; }
            if (side != BattleSideEnum.Defender) return;

            SiegeEvent siege;
            try { siege = PlayerSiege.PlayerSiegeEvent; }
            catch { return; }
            if (siege == null) return;

            MobileParty mainParty;
            try { mainParty = MobileParty.MainParty; }
            catch { return; }

            // Joined defenders build, inside or outside. Unjoined presence stays locked out,
            // which deliberately overrides a vanilla true for the pre-join top leader.
            __result = IsLocalDefenderJoined(mainParty, siege);
        }
        catch
        {
            // Preserve vanilla on unexpected UI-thread failures.
        }
    }

    // Joined means the join button's local DefendSettlement order targeting this siege
    // (survives save/load like the rest of the party AI state). Checking it instead of
    // involved parties or PlayerSiege, which presence grants for free: vanilla resolves
    // PlayerSiegeEvent from MainParty.SiegeEvent ?? CurrentSettlement.SiegeEvent and reports
    // Defender for anyone without a besieger camp, so any insider would pass a PlayerSiege
    // check without ever joining. No location check: outside joiners count, and a break-out
    // return rebuilds through rejoining. Residual: a pre-existing parked defend order counts
    // as joined; semantically defending, accepted.
    internal static bool IsLocalDefenderJoined(MobileParty mainParty, SiegeEvent siege)
    {
        if (mainParty == null || siege == null) return false;

        try
        {
            return mainParty.DefaultBehavior == AiBehavior.DefendSettlement
                && mainParty.TargetSettlement == siege.BesiegedSettlement;
        }
        catch
        {
            return false;
        }
    }
}
