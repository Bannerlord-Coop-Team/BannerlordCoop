using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Players;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

public interface IMapEventPlayerPresence
{
    /// <summary>True when a party in the map event belongs to a player who is connected right now.</summary>
    bool HasConnectedPlayerParty(MapEvent mapEvent);
}

/// <summary>
/// The server only resolves a battle with a player party through that player's mission. Once every such
/// player is offline nothing would resolve it (#3864), so the battle is then simulated like an AI battle.
/// A player party with no registered owner counts as connected, which keeps the battle waiting.
/// </summary>
internal sealed class MapEventPlayerPresence : IMapEventPlayerPresence
{
    private readonly IPlayerManager playerManager;

    public MapEventPlayerPresence(IPlayerManager playerManager)
    {
        this.playerManager = playerManager;
    }

    public bool HasConnectedPlayerParty(MapEvent mapEvent) =>
        mapEvent.InvolvedParties.Any(party =>
            party != null &&
            party.IsMobile &&
            !party.MobileParty.IsControlledByThisInstance() &&
            !playerManager.IsOwnerOfPartyDisconnected(party.MobileParty));
}