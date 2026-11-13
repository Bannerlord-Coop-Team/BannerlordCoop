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
    /// Redraws visible parties and settlements whose war stance differs between the two factions, then every settlement nameplate.
    /// </summary>
    void RedrawStanceVisuals(IFaction oldFaction, IFaction newFaction);
}

/// <inheritdoc cref="IMapStanceVisualsInterface"/>
public class MapStanceVisualsInterface : IMapStanceVisualsInterface
{
    /// <inheritdoc/>
    public void RedrawStanceVisuals(IFaction oldFaction, IFaction newFaction)
    {
        MarkStanceFlippedVisualsDirty(oldFaction, newFaction);
        MapScreen.Instance?.GetMapView<GauntletMapSettlementNameplateView>()?._dataSource?.RefreshRelationsOfNameplates();
    }

    /// <summary>
    /// Marks visible party and settlement markers dirty when they are at war with exactly one of the two factions.
    /// </summary>
    private static void MarkStanceFlippedVisualsDirty(IFaction oldFaction, IFaction newFaction)
    {
        var visibleParties = MobileParty.All.Where(mobileParty => mobileParty.IsVisible).Select(mobileParty => mobileParty.Party);
        var visibleSettlements = Settlement.All.Where(settlement => settlement.IsVisible).Select(settlement => settlement.Party);
        foreach (var mapParty in visibleParties.Concat(visibleSettlements))
        {
            // Same stance-flip test as ChangeKingdomAction.CheckIfPartyIconIsDirty, also applied to visible settlements.
            var stanceFlipped = FactionManager.IsAtWarAgainstFaction(mapParty.MapFaction, oldFaction) !=
                FactionManager.IsAtWarAgainstFaction(mapParty.MapFaction, newFaction);
            if (!stanceFlipped) continue;

            mapParty.SetVisualAsDirty();
        }
    }
}
