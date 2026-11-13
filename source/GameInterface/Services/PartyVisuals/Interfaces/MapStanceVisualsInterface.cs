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
    /// Marks visible party and settlement markers dirty when their war stance differs between the two factions.
    /// </summary>
    private static void MarkStanceFlippedVisualsDirty(IFaction oldFaction, IFaction newFaction)
    {
        // Same stance-flip test as ChangeKingdomAction.CheckIfPartyIconIsDirty, also applied to visible settlements.
        var visibleMapParties = MobileParty.All.Where(mobileParty => mobileParty.IsVisible).Select(mobileParty => mobileParty.Party)
            .Concat(Settlement.All.Where(settlement => settlement.IsVisible).Select(settlement => settlement.Party));
        foreach (var mapParty in visibleMapParties)
        {
            if (!HasStanceFlipped(mapParty, oldFaction, newFaction)) continue;

            mapParty.SetVisualAsDirty();
        }
    }

    /// <summary>
    /// Whether the party is at war with exactly one of the two factions, so its marker changes colour.
    /// </summary>
    private static bool HasStanceFlipped(PartyBase party, IFaction oldFaction, IFaction newFaction)
    {
        return FactionManager.IsAtWarAgainstFaction(party.MapFaction, oldFaction) !=
            FactionManager.IsAtWarAgainstFaction(party.MapFaction, newFaction);
    }
}
