using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Interfaces;

/// <summary>
/// Redraws the local map's friend and enemy markers after the local player's faction changes.
/// </summary>
public interface IMapRelationsInterface : IGameAbstraction
{
    /// <summary>
    /// Redraws visible parties and settlements whose war stance differs between the two factions, then the settlement nameplates.
    /// </summary>
    void RefreshRelations(IFaction oldFaction, IFaction newFaction);

    /// <summary>
    /// Recomputes every settlement nameplate's relation against the current main hero.
    /// </summary>
    void RefreshSettlementNameplates();
}

/// <inheritdoc cref="IMapRelationsInterface"/>
public class MapRelationsInterface : IMapRelationsInterface
{
    /// <inheritdoc/>
    public void RefreshRelations(IFaction oldFaction, IFaction newFaction)
    {
        // Matches ChangeKingdomAction.CheckIfPartyIconIsDirty: only redraw what changed side.
        var parties = MobileParty.All.Where(party => party.IsVisible).Select(party => party.Party)
            .Concat(Settlement.All.Where(settlement => settlement.IsVisible).Select(settlement => settlement.Party));
        foreach (var party in parties)
        {
            if (FactionManager.IsAtWarAgainstFaction(party.MapFaction, oldFaction) !=
                FactionManager.IsAtWarAgainstFaction(party.MapFaction, newFaction))
                party.SetVisualAsDirty();
        }

        RefreshSettlementNameplates();
    }

    /// <inheritdoc/>
    public void RefreshSettlementNameplates()
    {
        MapScreen.Instance?.GetMapView<GauntletMapSettlementNameplateView>()?._dataSource?.RefreshRelationsOfNameplates();
    }
}
