using Common;
using HarmonyLib;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Shows the normal map cursor over hovered siege engine circles the local player can build.
/// Vanilla picks the hover cursor from map navigability, so wall-mounted defender circles show
/// Disabled even for the commander while clicking still opens the popup. Same joined gate as
/// the popup: only joined defenders, inside or outside; attackers and pre-join keep vanilla.
/// </summary>
[HarmonyPatch(typeof(MapScreen), "HandleMouse")]
internal static class MapSiegeHoverCursorPatch
{
    [HarmonyPostfix]
    internal static void Postfix()
    {
        try
        {
            if (ModInformation.IsServer) return;

            var manager = SettlementVisualManager.Current;
            if (manager == null || manager._hoveredSiegeEntityID == UIntPtr.Zero) return;

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
            if (!MapSiegeCommandAuthorityPatch.IsLocalDefenderJoined(mainParty, siege)) return;

            var screen = MapScreen.Instance;
            if (screen?.SceneLayer == null) return;
            ((ScreenLayer)screen.SceneLayer).ActiveCursor = CursorType.Default;
        }
        catch
        {
            // Cosmetic only; never break the map mouse flow.
        }
    }
}
