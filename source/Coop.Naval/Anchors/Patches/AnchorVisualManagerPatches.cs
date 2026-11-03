using Common;
using GameInterface.Extentions;
using HarmonyLib;
using NavalDLC.Missions.Objects;
using NavalDLC.View;
using NavalDLC.View.Map.Managers;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Coop.Naval.Anchors.Patches;

/// <summary>
/// Vanilla only draws MobileParty.MainParty's fleet. Clients also draw other players' anchored fleets within the main party's
/// seeing range, without the zoomed out tracker icon or map interaction the local player's own fleet has.
/// </summary>
[HarmonyPatch(typeof(AnchorVisualManager))]
internal class AnchorVisualManagerPatches
{
    private static readonly Dictionary<MobileParty, OtherPlayerFleetVisual> OtherPlayerFleets = new();

    [HarmonyPatch(nameof(AnchorVisualManager.OnInitialize))]
    [HarmonyPostfix]
    private static void OnInitializePostfix()
    {
        // Entities from a previous map scene are removed with it
        OtherPlayerFleets.Clear();
    }

    // The server's removed main party has no anchor, which vanilla never expects, so it can't have an anchor visual
    [HarmonyPatch(nameof(AnchorVisualManager.CanPartyHaveAnchor))]
    [HarmonyPrefix]
    private static bool CanPartyHaveAnchorPrefix(ref bool __result)
    {
        if (Campaign.Current?.MainParty?.Anchor != null) return true;

        __result = false;
        return false;
    }

    // Vanilla OnTick reads MainParty.Anchor unguarded, so without an anchor it only drops a visual left from before removal
    [HarmonyPatch(nameof(AnchorVisualManager.OnTick))]
    [HarmonyPrefix]
    private static bool OnTickPrefix(AnchorVisualManager __instance)
    {
        if (Campaign.Current?.MainParty?.Anchor != null) return true;

        if (__instance._anchorVisual != null) __instance.RemoveAnchorVisual();
        return false;
    }

    [HarmonyPatch(nameof(AnchorVisualManager.OnTick))]
    [HarmonyPostfix]
    private static void OnTickPostfix(AnchorVisualManager __instance)
    {
        if (ModInformation.IsServer) return;

        var mainParty = MobileParty.MainParty;
        if (mainParty == null) return;

        var visibleParties = new HashSet<MobileParty>();
        foreach (var party in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
        {
            if (party == mainParty || !IsFleetVisible(party, mainParty)) continue;

            visibleParties.Add(party);
            if (!OtherPlayerFleets.TryGetValue(party, out var visual))
            {
                visual = new OtherPlayerFleetVisual(party);
                OtherPlayerFleets.Add(party, visual);
            }

            visual.Update(__instance.MapScene);
        }

        foreach (var party in OtherPlayerFleets.Keys.Where(party => !visibleParties.Contains(party)).ToList())
        {
            OtherPlayerFleets[party].Remove();
            OtherPlayerFleets.Remove(party);
        }
    }

    // Matches AnchorVisualManager.CanPartyHaveAnchor and AnchorVisual.IsVisibleOrFadingOut for another player's party
    private static bool IsFleetVisible(MobileParty party, MobileParty mainParty)
    {
        var anchor = party.Anchor;
        if (anchor == null || !anchor.IsValid || anchor.IsDisabled) return false;
        if (!party.IsActive || party.IsCurrentlyAtSea || party.IsTransitionInProgress || !party.HasNavalNavigationCapability) return false;

        return anchor.Position.Distance(mainParty.Position) <= mainParty.SeeingRange;
    }

    private sealed class OtherPlayerFleetVisual
    {
        private readonly MobileParty owner;
        private readonly List<SailVisual> sailVisuals = new();
        private GameEntity entity;
        private Ship flagShip;
        private ShipHull flagshipHull;
        private uint cachedVersion;
        private CampaignVec2 cachedPosition = CampaignVec2.Invalid;

        public OtherPlayerFleetVisual(MobileParty owner)
        {
            this.owner = owner;
        }

        public void Update(Scene mapScene)
        {
            var currentFlagShip = owner.Party.FlagShip;
            var hull = currentFlagShip?.ShipHull;
            if (entity == null || currentFlagShip != flagShip || hull != flagshipHull)
            {
                Remove();

                // Without a physics body or a MapScreen.VisualsOfEntities entry the fleet can't be hovered or clicked
                entity = NavalDLCViewHelpers.ShipVisualHelper.GetFlagshipEntity(owner.Party, mapScene);
                NavalDLCViewHelpers.ShipVisualHelper.CollectSailVisuals(entity.WeakEntity, sailVisuals);
                flagShip = currentFlagShip;
                flagshipHull = hull;
                cachedVersion = currentFlagShip?.VersionNo ?? 0;
            }
            // Same as AnchorVisual.OnVisualUpdate, upgrades and figureheads change the ship's version but not its hull
            else if (currentFlagShip != null && currentFlagShip.VersionNo != cachedVersion)
            {
                cachedVersion = currentFlagShip.VersionNo;
                NavalDLCViewHelpers.ShipVisualHelper.RefreshShipVisuals(entity.WeakEntity, currentFlagShip, sailVisuals);
            }

            var anchor = owner.Anchor;
            if (cachedPosition == anchor.Position) return;

            cachedPosition = anchor.Position;
            var frame = CalculateFrame(anchor);
            entity.SetFrame(ref frame, true);
            entity.SetVisibilityExcludeParents(true);
        }

        public void Remove()
        {
            entity?.Remove(111);
            entity = null;
            sailVisuals.Clear();
            cachedPosition = CampaignVec2.Invalid;
        }

        // Same facing as AnchorVisual.CalculateAnchorFrame, side on to where the party disembarked
        private MatrixFrame CalculateFrame(AnchorPoint anchor)
        {
            var interactionPosition = anchor.GetInteractionPosition(owner);
            var facing = interactionPosition.IsValid()
                ? (interactionPosition.ToVec2() - anchor.Position.ToVec2()).Normalized()
                : Vec2.Forward;

            var scale = entity.GetLocalScale();
            var frame = MatrixFrame.Identity;
            frame.origin = anchor.Position.AsVec3();
            frame.rotation.f.AsVec2 = facing.RightVec();
            frame.rotation.f.NormalizeWithoutChangingZ();
            frame.rotation.Orthonormalize();
            frame.rotation.ApplyScaleLocal(in scale);
            return frame;
        }
    }
}
