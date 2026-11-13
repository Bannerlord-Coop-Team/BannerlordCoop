using GameInterface.Services.Players;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
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
    /// [Game thread] The fleet <paramref name="playerParty"/> fields: vanilla's best player ship, then its party's ships up to its
    /// share of the side, against every other party there as its ally team. A party without ships fields none.
    /// </summary>
    NavalPlayerFleet PlayerFleet(MBReadOnlyList<MapEventParty> sideParties, PartyBase playerParty);

    /// <summary>[Game thread] The AI parties' share of the side: the eight ships less every player's fleet, at most their own limit.</summary>
    NavalShipDeploymentLimit AiLimit(MBReadOnlyList<MapEventParty> sideParties, MBReadOnlyList<MapEventParty> aiParties);
}

/// <inheritdoc cref="INavalShipBudget"/>
public class NavalShipBudget : INavalShipBudget
{
    // GetShipDeploymentLimitsOfPlayerTeams caps a side's player and ally teams at eight ships together.
    internal const int SideShips = 8;

    private readonly IPlayerManager playerManager;

    // Reads which parties are players' from the player registry.
    public NavalShipBudget(IPlayerManager playerManager)
    {
        this.playerManager = playerManager;
    }

    // The launcher's own fleet; every client computes the same for each player, so the AI share below matches it.
    public NavalPlayerFleet PlayerFleet(MBReadOnlyList<MapEventParty> sideParties, PartyBase playerParty)
    {
        var model = NavalDLCManager.Instance.GameModels.ShipDeploymentModel;
        SplitPlayerSideParties(sideParties, playerParty, out var playerMapEventParty, out var playerTeamParties, out var allyTeamParties);
        model.GetShipDeploymentLimitsOfPlayerTeams(playerTeamParties, allyTeamParties, out var playerLimit, out var allyLimit);
        if (playerMapEventParty.Ships.Count == 0) return new NavalPlayerFleet(Array.Empty<Ship>(), playerLimit, allyLimit);

        var ships = new MBList<IShipOrigin> { model.GetSuitablePlayerShip(playerMapEventParty, playerTeamParties) };
        model.FillShipsOfTeamParties(playerTeamParties, playerLimit, ships);
        return new NavalPlayerFleet(ships.Cast<Ship>().ToList(), playerLimit, allyLimit);
    }

    // Vanilla's team limit for the AI parties, with its net ships cut to what the players left, as vanilla cuts a team's share.
    // The players' fleets are recomputed from campaign data rather than read from the registered hulls: a peer announces its
    // hulls only at its own deployment commit, which can come after the host fields the AI. A party joining the side between a
    // player's mission open and this call can still shift that player's ratio share; late joiners are not handled yet.
    public NavalShipDeploymentLimit AiLimit(MBReadOnlyList<MapEventParty> sideParties, MBReadOnlyList<MapEventParty> aiParties)
    {
        var limit = NavalDLCManager.Instance.GameModels.ShipDeploymentModel.GetTeamShipDeploymentLimit(aiParties);
        var playerFleets = sideParties.Where(party => playerManager.Contains(party.Party?.MobileParty))
            .Select(party => PlayerFleet(sideParties, party.Party).FieldedCount);
        return new NavalShipDeploymentLimit(limit.PartiesLimit, limit.SkeletalCrewLimit, LeftForAi(playerFleets, limit.NetDeploymentLimit));
    }

    /// <summary>What the players' fielded ships leave of the side's eight, at most the AI parties' own limit and never below none.</summary>
    internal static int LeftForAi(IEnumerable<int> playerFleets, int aiLimit) =>
        Math.Max(0, Math.Min(aiLimit, SideShips - playerFleets.Sum()));

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
