using SandBox.GauntletUI.Map;
using SandBox.View.Map;
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
        // Same stance-flip test as ChangeKingdomAction.CheckIfPartyIconIsDirty, also applied to visible settlements.
        foreach (var mobileParty in MobileParty.All)
        {
            if (mobileParty.IsVisible && HasStanceFlipped(mobileParty.Party, oldFaction, newFaction))
            {
                mobileParty.Party.SetVisualAsDirty();
            }
        }

        foreach (var settlement in Settlement.All)
        {
            if (settlement.IsVisible && HasStanceFlipped(settlement.Party, oldFaction, newFaction))
            {
                settlement.Party.SetVisualAsDirty();
            }
        }

        MapScreen.Instance?.GetMapView<GauntletMapSettlementNameplateView>()?._dataSource?.RefreshRelationsOfNameplates();
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
