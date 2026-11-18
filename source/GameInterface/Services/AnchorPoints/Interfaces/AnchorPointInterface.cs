using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.AnchorPoints.Interfaces;

/// <summary>
/// Server-only: anchors a party's fleet at a port.
/// </summary>
public interface IAnchorPointInterface : IGameAbstraction
{
    /// <summary>
    /// Anchors the fleet of a party that has ships but no anchor at <paramref name="port"/>, like vanilla's ship purchase.
    /// </summary>
    void AnchorUnanchoredFleet(MobileParty party, Settlement port);
}

/// <inheritdoc cref="IAnchorPointInterface"/>
public class AnchorPointInterface : IAnchorPointInterface
{
    public void AnchorUnanchoredFleet(MobileParty party, Settlement port)
    {
        if (party.Ships.Count == 0 || party.Anchor.IsValid) return;

        party.Anchor.Settlement = port;
    }
}
