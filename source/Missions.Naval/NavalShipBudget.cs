using GameInterface.Services.Players;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Missions.Naval;

/// <summary>
/// Splits a naval battle side's eight ships from campaign data every client holds: each player fields its own share, and the
/// side's AI parties get what the players leave (M11 A2).
/// </summary>
public interface INavalShipBudget
{
    /// <summary>
    /// [Game thread] The campaign ships <paramref name="playerParty"/> fields: vanilla's best player ship, then its party's ships up
    /// to its share of the side, against every other party there as its ally team.
    /// </summary>
    MBList<IShipOrigin> PlayerShips(MBReadOnlyList<MapEventParty> sideParties, PartyBase playerParty,
        out NavalShipDeploymentLimit playerLimit, out NavalShipDeploymentLimit allyLimit);

    /// <summary>[Game thread] The AI parties' share of the side: the eight ships less every player's share, at most their own limit.</summary>
    NavalShipDeploymentLimit AiLimit(MBReadOnlyList<MapEventParty> sideParties, MBReadOnlyList<MapEventParty> aiParties);
}

/// <inheritdoc cref="INavalShipBudget"/>
public class NavalShipBudget : INavalShipBudget
{
    // GetShipDeploymentLimitsOfPlayerTeams caps a side's player and ally teams at eight ships together.
    internal const int SideShips = 8;

    private readonly IPlayerManager playerManager;

    public NavalShipBudget(IPlayerManager playerManager)
    {
        this.playerManager = playerManager;
    }

    // The launcher's own fleet; every client computes the same for each player, so the AI share below matches it.
    public MBList<IShipOrigin> PlayerShips(MBReadOnlyList<MapEventParty> sideParties, PartyBase playerParty,
        out NavalShipDeploymentLimit playerLimit, out NavalShipDeploymentLimit allyLimit)
    {
        var model = NavalDLCManager.Instance.GameModels.ShipDeploymentModel;
        SplitPlayerSideParties(sideParties, playerParty, out var playerMapEventParty, out var playerTeamParties, out var allyTeamParties);
        model.GetShipDeploymentLimitsOfPlayerTeams(playerTeamParties, allyTeamParties, out playerLimit, out allyLimit);

        var ships = new MBList<IShipOrigin> { model.GetSuitablePlayerShip(playerMapEventParty, playerTeamParties) };
        model.FillShipsOfTeamParties(playerTeamParties, playerLimit, ships);
        return ships;
    }

    // Vanilla's team limit for the AI parties, with its net ships cut to what the players left, as vanilla cuts a team's share.
    public NavalShipDeploymentLimit AiLimit(MBReadOnlyList<MapEventParty> sideParties, MBReadOnlyList<MapEventParty> aiParties)
    {
        var limit = NavalDLCManager.Instance.GameModels.ShipDeploymentModel.GetTeamShipDeploymentLimit(aiParties);
        int playerShips = sideParties.Where(IsFieldingPlayer).Sum(party => PlayerShipCount(sideParties, party.Party));
        return new NavalShipDeploymentLimit(limit.PartiesLimit, limit.SkeletalCrewLimit,
            LeftForAi(playerShips, limit.NetDeploymentLimit));
    }

    /// <summary>What the players' ships leave of the side's eight, at most the AI parties' own limit and never below none.</summary>
    internal static int LeftForAi(int playerShips, int aiLimit) => Math.Max(0, Math.Min(aiLimit, SideShips - playerShips));

    // The launcher fields as many of its ships as its share allows.
    private int PlayerShipCount(MBReadOnlyList<MapEventParty> sideParties, PartyBase playerParty) =>
        Math.Min(PlayerShips(sideParties, playerParty, out var limit, out _).Count, limit.NetDeploymentLimit);

    // A player party with ships; the launcher refuses to open a naval battle for one without.
    private bool IsFieldingPlayer(MapEventParty party) =>
        party.Party?.MobileParty != null && playerManager.Contains(party.Party.MobileParty) && party.Ships.Count > 0;

    /// <summary>
    /// Own party only: vanilla GetMapEventPartiesOfPlayerTeams takes the first non-NPC party as the player's, which is another
    /// player's party whenever two players share a side.
    /// </summary>
    internal static void SplitPlayerSideParties(
        MBReadOnlyList<MapEventParty> sideParties,
        PartyBase ownParty,
        out MapEventParty ownMapEventParty,
        out MBList<MapEventParty> ownTeamParties,
        out MBList<MapEventParty> allyTeamParties)
    {
        ownMapEventParty = null;
        ownTeamParties = new MBList<MapEventParty>();
        allyTeamParties = new MBList<MapEventParty>();
        foreach (var party in sideParties)
        {
            if (party.Party == ownParty)
            {
                ownMapEventParty = party;
                ownTeamParties.Add(party);
                continue;
            }

            allyTeamParties.Add(party);
        }
    }
}
