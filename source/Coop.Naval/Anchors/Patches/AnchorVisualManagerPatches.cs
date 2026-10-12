using Common;
using GameInterface.Extentions;
using HarmonyLib;
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
        private GameEntity entity;
        private ShipHull flagshipHull;
        private CampaignVec2 cachedPosition = CampaignVec2.Invalid;

        public OtherPlayerFleetVisual(MobileParty owner)
        {
            this.owner = owner;
        }

        public void Update(Scene mapScene)
        {
            var hull = owner.Party.FlagShip?.ShipHull;
            if (entity == null || hull != flagshipHull)
            {
                Remove();

                // Without a physics body or a MapScreen.VisualsOfEntities entry the fleet can't be hovered or clicked
                entity = NavalDLCViewHelpers.ShipVisualHelper.GetFlagshipEntity(owner.Party, mapScene);
                flagshipHull = hull;
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
