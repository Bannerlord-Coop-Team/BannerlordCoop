using Common.Logging;
using Common.Network;
using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Messages;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents;

/// <summary>[Server] Opens, times and locks the keep stage of a siege assault on the game thread.</summary>
public interface ISiegeKeepStage : IGameAbstraction
{
    /// <summary>Seats the walls players inside vanilla's hall limits and moves the siege into the lord's hall.</summary>
    bool TryOpen(MapEvent mapEvent);

    /// <summary>Drops seats of players who left, and locks each stage once everyone answered or its window ran out.</summary>
    void Tick();

    /// <summary>Gives a player who never answered vanilla's suggested squad, then fixes the roster.</summary>
    void Lock(MapEvent mapEvent);

    int SecondsRemaining(MapEvent mapEvent);
}

/// <inheritdoc cref="ISiegeKeepStage"/>
public sealed class SiegeKeepStage : ISiegeKeepStage
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeKeepStage>();

    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ISiegeKeepTroopSelection selection;
    private readonly IBattleTroopReserveBuilder reserveBuilder;

    /// <summary>The same 90 s the hideout assault waits for its leader, so a player who never answers can't stall the keep.</summary>
    internal TimeSpan SelectionWindow { get; set; } = TimeSpan.FromSeconds(90);

    public SiegeKeepStage(INetwork network, IObjectManager objectManager, IPlayerManager playerManager,
        ISiegeKeepTroopSelection selection, IBattleTroopReserveBuilder reserveBuilder)
    {
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.selection = selection;
        this.reserveBuilder = reserveBuilder;
    }

    public bool TryOpen(MapEvent mapEvent)
    {
        var settlement = mapEvent.MapEventSettlement;
        if (!mapEvent.CanFallBackToKeep() || mapEvent.IsFinalized || mapEvent.BattleState != BattleState.None ||
            settlement.SiegeEvent == null)
            return false;

        var candidates = GetAttackingPlayers(mapEvent);
        if (candidates.Count == 0) return false;

        var model = Campaign.Current.Models.SiegeLordsHallFightModel;
        var defenderBudget = new Dictionary<string, int>();
        foreach (var element in model.GetPriorityListForLordsHallFightMission(mapEvent, BattleSideEnum.Defender,
                     model.MaxDefenderSideTroopCount))
        {
            if (!objectManager.TryGetId(element.Troop, out var characterId)) continue;
            defenderBudget.TryGetValue(characterId, out var count);
            defenderBudget[characterId] = count + 1;
        }
        var defenderCount = defenderBudget.Values.Sum();
        if (defenderCount == 0) return false;

        // Escorts a candidate could bring: every healthy member but the player's own hero.
        var escortsPerCandidate = candidates
            .Select(candidate => Math.Max(0, candidate.Party.MemberRoster.TotalHealthyCount - 1)).ToList();
        var shares = SiegeKeepTroopSelection.PlanSeats(defenderCount, model.AttackerDefenderTroopCountRatio,
            model.MaxAttackerSideTroopCount, escortsPerCandidate);
        var seats = candidates.Take(shares.Length).Select((candidate, index) => new SiegeKeepSeat(
            candidate.Player.ControllerId, candidate.Player.MobilePartyId, candidate.Player.CharacterObjectId,
            shares[index])).ToList();

        selection.Open(mapEvent, seats, defenderBudget, DateTime.UtcNow + SelectionWindow);
        settlement.CurrentSiegeState = Settlement.SiegeState.InTheLordsHall;

        Logger.Information("Opened keep stage for {Settlement}: {Defenders} defenders, {Seated} of {Players} players seated",
            settlement.StringId, defenderCount, seats.Count, candidates.Count);
        SendPhase(mapEvent, SiegeKeepPhase.Choosing);
        return true;
    }

    // The siege leader first, then the other walls players in the attacker side's order.
    private List<(Player Player, MobileParty Party)> GetAttackingPlayers(MapEvent mapEvent)
    {
        var players = new List<(Player, MobileParty)>();
        var leader = mapEvent.AttackerSide.LeaderParty?.MobileParty;
        foreach (var mapEventParty in mapEvent.AttackerSide.Parties.OrderBy(party => party.Party?.MobileParty == leader ? 0 : 1))
        {
            var party = mapEventParty.Party?.MobileParty;
            if (party == null || !party.IsActive || !playerManager.TryGetPlayer(party, out var player) ||
                !playerManager.IsConnected(player) ||
                !objectManager.TryGetObject<Hero>(player.HeroId, out var hero) || hero.IsWounded)
                continue;

            players.Add((player, party));
        }
        return players;
    }

    public void Tick()
    {
        foreach (var mapEvent in selection.GetOpenMapEvents())
        {
            if (selection.IsLocked(mapEvent)) continue;

            // A player who left before the roster locked frees the wait for the others.
            foreach (var seat in selection.GetSeats(mapEvent))
                if (!IsConnected(seat.ControllerId))
                    selection.RemoveSeat(mapEvent, seat.MobilePartyId);

            if (selection.HaveAllAnswered(mapEvent) ||
                (selection.TryGetDeadline(mapEvent, out var deadline) && DateTime.UtcNow >= deadline))
                Lock(mapEvent);
        }
    }

    public void Lock(MapEvent mapEvent)
    {
        foreach (var seat in selection.GetSeats(mapEvent))
        {
            if (!IsConnected(seat.ControllerId) ||
                !objectManager.TryGetObject<MobileParty>(seat.MobilePartyId, out var party) ||
                !ReferenceEquals(party.MapEvent, mapEvent))
            {
                selection.RemoveSeat(mapEvent, seat.MobilePartyId);
                continue;
            }

            if (selection.HasAnswered(mapEvent, seat.MobilePartyId)) continue;
            if (!selection.TrySelect(mapEvent, party, SuggestSquad(party, seat)))
                selection.RemoveSeat(mapEvent, seat.MobilePartyId);
        }

        if (selection.GetSeats(mapEvent).Count == 0)
        {
            Logger.Information("Closing keep stage for {MapEventId}: no seated player is left", mapEvent.StringId);
            SendPhase(mapEvent, SiegeKeepPhase.Closed);
            selection.Forget(mapEvent);
            return;
        }

        // The walls reserves are dropped as the keep filter turns on, so nothing built from full rosters survives it.
        reserveBuilder.ForgetMapEvent(mapEvent, preserveHealth: true);
        selection.Lock(mapEvent);
        Logger.Information("Locked keep roster for {MapEventId} with {Seats} player(s)",
            mapEvent.StringId, selection.GetSeats(mapEvent).Count);
        SendPhase(mapEvent, SiegeKeepPhase.Locked);
    }

    // The strongest healthy troops, companions first, as vanilla's hall picker suggests them.
    private HideoutTroopSelectionEntry[] SuggestSquad(MobileParty party, SiegeKeepSeat seat)
    {
        var roster = party.MemberRoster.ToFlattenedRoster();
        roster.RemoveIf(element => element.IsWounded ||
            (objectManager.TryGetId(element.Troop, out var characterId) && characterId == seat.HeroCharacterId));
        var squad = MobilePartyHelper.GetStrongestAndPriorTroops(roster, seat.EscortAllowance, includePlayer: false);
        var entries = new List<HideoutTroopSelectionEntry>();
        foreach (var element in squad.GetTroopRoster())
            if (element.Number > 0 && objectManager.TryGetId(element.Character, out var characterId))
                entries.Add(new HideoutTroopSelectionEntry(characterId, element.Number));
        return entries.ToArray();
    }

    public int SecondsRemaining(MapEvent mapEvent) =>
        selection.TryGetDeadline(mapEvent, out var deadline)
            ? Math.Max(0, (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalSeconds))
            : 0;

    private bool IsConnected(string controllerId) =>
        playerManager.TryGetPlayer(controllerId, out var player) && playerManager.IsConnected(player);

    // Every player in the map event hears where the keep stands, seated or not.
    private void SendPhase(MapEvent mapEvent, SiegeKeepPhase phase)
    {
        if (!objectManager.TryGetId(mapEvent, out var mapEventId)) return;

        var seconds = SecondsRemaining(mapEvent);
        foreach (var player in playerManager.Players)
        {
            if (!playerManager.TryGetPeer(player.ControllerId, out var peer) ||
                !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ||
                !ReferenceEquals(party.MapEvent, mapEvent))
                continue;

            var isSeated = selection.TryGetSeat(mapEvent, player.MobilePartyId, out var seat);
            network.Send(peer, new NetworkSiegeKeepPhaseChanged(mapEventId, phase, isSeated,
                isSeated ? seat.EscortAllowance : 0, seconds));
        }
    }
}
