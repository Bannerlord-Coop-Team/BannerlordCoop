using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyVisuals.Interfaces;

/// <summary>
/// Redraws the local map's friend and enemy markers after the local player's faction changes.
/// </summary>
public interface IMapStanceVisualsInterface : IGameAbstraction
{
    /// <summary>
    /// Redraws visible markers whose war stance changed from the previous faction to the local main hero's current one, then every settlement nameplate.
    /// </summary>
    void RedrawStanceVisuals(IFaction previousFaction);
}

/// <inheritdoc cref="IMapStanceVisualsInterface"/>
public class MapStanceVisualsInterface : IMapStanceVisualsInterface
{
    /// <inheritdoc/>
    public void RedrawStanceVisuals(IFaction previousFaction)
    {
        // Vanilla nameplates compare against the main hero, so the markers use the same faction.
        var currentFaction = Hero.MainHero.MapFaction;
        MarkStanceFlippedVisualsDirty(previousFaction, currentFaction);
        MapScreen.Instance?.GetMapView<GauntletMapSettlementNameplateView>()?._dataSource?.RefreshRelationsOfNameplates();
    }

    /// <summary>
    /// Marks visible party and settlement markers dirty when they are at war with exactly one of the two factions.
    /// </summary>
    private static void MarkStanceFlippedVisualsDirty(IFaction previousFaction, IFaction currentFaction)
    {
        var visibleParties = MobileParty.All.Where(mobileParty => mobileParty.IsVisible).Select(mobileParty => mobileParty.Party);
        var visibleSettlements = Settlement.All.Where(settlement => settlement.IsVisible).Select(settlement => settlement.Party);
        foreach (var mapParty in visibleParties.Concat(visibleSettlements))
        {
            // Same stance-flip test as ChangeKingdomAction.CheckIfPartyIconIsDirty, also applied to visible settlements.
            var stanceFlipped = FactionManager.IsAtWarAgainstFaction(mapParty.MapFaction, previousFaction) !=
                FactionManager.IsAtWarAgainstFaction(mapParty.MapFaction, currentFaction);
            if (!stanceFlipped) continue;

            mapParty.SetVisualAsDirty();
        }
    }
}
