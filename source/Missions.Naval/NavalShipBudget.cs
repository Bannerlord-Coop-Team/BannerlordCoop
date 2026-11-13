using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Ships;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using Serilog;
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
/// Splits a naval battle side's eight ships: each player fields its own share, the side's AI parties get what the players leave
/// (M11 A2), and a player who joins after the side's AI fleet attempt gets only what the recorded attempt leaves.
/// </summary>
public interface INavalShipBudget
{
    /// <summary>
    /// [Game thread] The fleet <paramref name="playerParty"/> fields: vanilla's best player ship, then its party's ships up to its
    /// share of the side, against every other party there as its ally team, capped by the side's recorded AI fleet attempt if it
    /// joined after it. A party without ships fields none.
    /// </summary>
    NavalPlayerFleet PlayerFleet(MBReadOnlyList<MapEventParty> sideParties, PartyBase playerParty);

    /// <summary>[Game thread] Every player's fleet on the side, which the side's AI share leaves room for.</summary>
    NavalSidePlayerFleets PlayerFleets(MBReadOnlyList<MapEventParty> sideParties);

    /// <summary>[Game thread] The AI parties' share of the side: the eight ships less the players' fleets, at most their own limit.</summary>
    NavalShipDeploymentLimit AiLimit(NavalSidePlayerFleets players, MBReadOnlyList<MapEventParty> aiParties);
}

/// <inheritdoc cref="INavalShipBudget"/>
public class NavalShipBudget : INavalShipBudget
{
    // GetShipDeploymentLimitsOfPlayerTeams caps a side's player and ally teams at eight ships together.
    internal const int SideShips = 8;

    private static readonly ILogger Logger = LogManager.GetLogger<NavalShipBudget>();

    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly INavalNpcFleetAttemptTracker attempts;

    // Reads which parties are players' from the player registry, their ids from the object manager, and the sides' recorded AI
    // fleet attempts.
    public NavalShipBudget(IPlayerManager playerManager, IObjectManager objectManager, INavalNpcFleetAttemptTracker attempts)
    {
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.attempts = attempts;
    }

    // The launcher's own fleet; every client computes the same for each player, so the AI share below matches it.
    public NavalPlayerFleet PlayerFleet(MBReadOnlyList<MapEventParty> sideParties, PartyBase playerParty)
    {
        var model = NavalDLCManager.Instance.GameModels.ShipDeploymentModel;
        SplitPlayerSideParties(sideParties, playerParty, out var playerMapEventParty, out var playerTeamParties, out var allyTeamParties);
        model.GetShipDeploymentLimitsOfPlayerTeams(playerTeamParties, allyTeamParties, out var playerLimit, out var allyLimit);
        if (playerMapEventParty.Ships.Count == 0) return new NavalPlayerFleet(Array.Empty<Ship>(), playerLimit, allyLimit);

        playerLimit = CapLateJoiner(sideParties, playerParty, playerLimit);
        var ships = new MBList<IShipOrigin> { model.GetSuitablePlayerShip(playerMapEventParty, playerTeamParties) };
        model.FillShipsOfTeamParties(playerTeamParties, playerLimit, ships);
        return new NavalPlayerFleet(ships.Cast<Ship>().ToList(), playerLimit, allyLimit);
    }

    // Players' fleets come from campaign data, not registered hulls: a peer announces its hulls only at its deployment commit.
    public NavalSidePlayerFleets PlayerFleets(MBReadOnlyList<MapEventParty> sideParties)
    {
        var partyIds = new List<string>();
        int hulls = 0;
        foreach (var party in sideParties)
        {
            if (!TryGetPlayerPartyId(party.Party, out string partyId)) continue;

            partyIds.Add(partyId);
            hulls += PlayerFleet(sideParties, party.Party).FieldedCount;
        }

        int playerCount = sideParties.Count(party => playerManager.Contains(party.Party.MobileParty));
        return new NavalSidePlayerFleets(partyIds.ToArray(), hulls, playerCount);
    }

    // Vanilla's team limit for the AI parties, with its net ships cut to what the players left, as vanilla cuts a team's share.
    public NavalShipDeploymentLimit AiLimit(NavalSidePlayerFleets players, MBReadOnlyList<MapEventParty> aiParties)
    {
        var limit = NavalDLCManager.Instance.GameModels.ShipDeploymentModel.GetTeamShipDeploymentLimit(aiParties);
        return new NavalShipDeploymentLimit(limit.PartiesLimit, limit.SkeletalCrewLimit, LeftForAi(players.Hulls, limit.NetDeploymentLimit));
    }

    /// <summary>What the players' fielded ships leave of the side's eight, at most the AI parties' own limit and never below none.</summary>
    internal static int LeftForAi(int playerHulls, int aiLimit) => Math.Max(0, Math.Min(aiLimit, SideShips - playerHulls));

    // Caps a player that joined after the side's recorded AI fleet attempt (so the attempt didn't count it) at what the attempt leaves.
    private NavalShipDeploymentLimit CapLateJoiner(MBReadOnlyList<MapEventParty> sideParties, PartyBase playerParty,
        NavalShipDeploymentLimit limit)
    {
        if (!objectManager.TryGetIdWithLogging(playerParty.MapEventSide.MapEvent, out string mapEventId)) return limit;
        if (!attempts.TryGet(mapEventId, playerParty.Side, out var attempt)) return limit;

        var model = NavalDLCManager.Instance.GameModels.ShipDeploymentModel;
        int MostFieldedOf(PartyBase party) => MostFielded(party.Ships.Count, model.GetShipDeploymentLimit(party.MobileParty));
        int recordedHulls = attempt.AiHulls + attempt.PlayerHulls;
        int cap = LateJoinerCap(
            sideParties.Select(party => party.Party),
            playerParty,
            party => IsUncountedPlayer(party, attempt.CountedPartyIds),
            MostFieldedOf,
            recordedHulls);
        if (cap >= limit.NetDeploymentLimit) return limit;

        Logger.Information("[NavalBattle] Capped the fleet joining {MapEventId} after its {Side} AI fleet attempt: " +
            "cap={Cap} share={Share} aiHulls={AiHulls} playerHulls={PlayerHulls}", mapEventId, playerParty.Side, cap,
            limit.NetDeploymentLimit, attempt.AiHulls, attempt.PlayerHulls);
        return new NavalShipDeploymentLimit(limit.PartiesLimit, limit.SkeletalCrewLimit, cap);
    }

    /// <summary>Whether the party is a player's the attempt didn't count; an attempt that counted none arrives with null ids.</summary>
    internal bool IsUncountedPlayer(PartyBase party, string[] countedPartyIds)
    {
        if (!TryGetPlayerPartyId(party, out string partyId)) return false;

        var counted = countedPartyIds ?? Array.Empty<string>();
        return !counted.Contains(partyId);
    }

    // A player's party and its id, the way the attempt counts players; logs a player party without an id.
    private bool TryGetPlayerPartyId(PartyBase party, out string partyId)
    {
        partyId = null;
        return playerManager.Contains(party.MobileParty) && objectManager.TryGetIdWithLogging(party.MobileParty, out partyId);
    }

    /// <summary>
    /// The side's eight less the attempt's hulls and the most each earlier uncounted player can field. Never below one, the
    /// player's own ship. A player the attempt counted keeps the eight.
    /// </summary>
    internal static int LateJoinerCap<T>(IEnumerable<T> sideParties, T own, Func<T, bool> isUncountedPlayer, Func<T, int> mostFielded,
        int recordedHulls) where T : class
    {
        if (!isUncountedPlayer(own)) return SideShips;

        int earlierJoiners = sideParties.TakeWhile(party => !ReferenceEquals(party, own)).Where(isUncountedPlayer).Sum(mostFielded);
        return Math.Max(1, SideShips - recordedHulls - earlierJoiners);
    }

    /// <summary>The most hulls a party can field, from its ships and perk limit, which casualties never lower as they do its crew.</summary>
    internal static int MostFielded(int ships, int partyShipLimit) => Math.Min(SideShips, Math.Min(ships, partyShipLimit));

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
