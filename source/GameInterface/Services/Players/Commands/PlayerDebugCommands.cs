using Common.Commands;
using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Players.Commands;

internal class PlayerDebugCommands
{
    private static CoopCommandResult Succeeded(string output) =>

        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>

        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.players.list

    public sealed class PlayerListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.players";

        public string Name => "list";

        public string Description => "Lists registered co-op players.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) == false)
                return Failed($"Unable to get {nameof(IPlayerManager)}");
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
                return Failed($"Unable to get {nameof(IObjectManager)}");

            ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
            var localId = controllerIdProvider?.ControllerId;

            var players = playerManager.Players;

            var sb = new StringBuilder();
            sb.AppendLine($"Side: {(ModInformation.IsServer ? "Server" : "Client")}  LocalControllerId: {localId ?? "<unknown>"}");
            sb.AppendLine($"Registered players: {players.Count} (expected: one per client, host excluded)");

            // PlayerObjects (the hero/party/clan -> controller table) is not enumerable, so report it indirectly:
            // each player contributes its resolvable, registered hero/party/clan. The total should be 3x the
            // player count once everyone's objects are present.
            int controlledObjects = 0;

            foreach (var player in players)
            {
                var marker = player.ControllerId == localId ? " (you)" : "";
                sb.AppendLine($"- ControllerId: {player.ControllerId}{marker}");
                controlledObjects += AppendObject<Hero>(sb, objectManager, playerManager, "Hero", player.HeroId);
                controlledObjects += AppendObject<MobileParty>(sb, objectManager, playerManager, "Party", player.MobilePartyId);
                controlledObjects += AppendObject<Clan>(sb, objectManager, playerManager, "Clan", player.ClanId);
            }

            sb.AppendLine($"PlayerObjects entries (resolved & controlled): {controlledObjects}");

            return Succeeded(sb.ToString());
        }
    }

    public sealed class PlayerPartyStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.players";

        public string Name => "party_state";

        public string Description => "Reports replicated party state for a player.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player controller id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer)
                return Failed("Command can only be run on the server.");
            if (ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) == false)
                return Failed($"Unable to get {nameof(IPlayerManager)}");
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
                return Failed($"Unable to get {nameof(IObjectManager)}");
            if (!playerManager.TryGetPlayer(args[0], out var player))
                return Failed($"Controller {args[0]} is not registered.");
            if (!objectManager.TryGetObject(player.MobilePartyId, out MobileParty party))
                return Failed($"Party {player.MobilePartyId} is not resolved.");

            string mapEventId = party.MapEvent?.StringId ?? "none";
            bool hasVisual = party.Party.GetPartyVisual() != null;
            string structuredState = JsonConvert.SerializeObject(new
            {
                controllerId = player.ControllerId,
                partyId = player.MobilePartyId,
                partyStringId = party.StringId,
                connected = playerManager.IsConnected(player),
                active = party.IsActive,
                mapEvent = mapEventId,
                visual = hasVisual,
            });

            return
                Succeeded($"controller={player.ControllerId}|" +
                $"party={party.StringId}|" +
                $"connected={playerManager.IsConnected(player)}|" +
                $"active={party.IsActive}|" +
                $"mapEvent={mapEventId}|" +
                $"visual={hasVisual}" +
                $"\nLIVE_TEST_JSON={structuredState}");
        }
    }

#if DEBUG
    private sealed class RejoinReserveEntryState
    {
        public int Seed { get; set; }
        public string CharacterId { get; set; }
        public int FormationClass { get; set; }
        public int SupplyOrder { get; set; }
    }

    private sealed class RejoinReservePartyState
    {
        public string LedgerPartyId { get; set; }
        public bool ReserveAvailable { get; set; }
        public bool MapEventPartyResolved { get; set; }
        public bool SideResolved { get; set; }
        public string Side { get; set; }
        public bool IsReturningParty { get; set; }
        public string ReturningPartyAssociation { get; set; }
        public int SuppliedCount { get; set; }
        public int RemainingCount { get; set; }
        public RejoinReserveEntryState[] Entries { get; set; }
    }

    private sealed class RejoinReserveSideState
    {
        public string Side { get; set; }
        public int EntryCount { get; set; }
        public int SuppliedCount { get; set; }
        public int RemainingCount { get; set; }
    }

    private sealed class RejoinObservationState
    {
        public bool Success { get; set; }
        public string ControllerId { get; set; }
        public bool PlayerRegistered { get; set; }
        public bool PlayerConnected { get; set; }
        public string RegisteredHeroId { get; set; }
        public string RegisteredPartyId { get; set; }
        public bool ReturningHeroResolved { get; set; }
        public bool ReturningHeroControlled { get; set; }
        public string ReturningHeroStringId { get; set; }
        public bool ReturningPartyResolved { get; set; }
        public bool ReturningPartyControlled { get; set; }
        public string ReturningPartyStringId { get; set; }
        public bool ReturningPartyActive { get; set; }
        public bool MapEventPresent { get; set; }
        public string MapEventId { get; set; }
        public string MapEventStringId { get; set; }
        public bool LedgerAvailable { get; set; }
        public int AuthoritativeLedgerPartyCount { get; set; }
        public int UnresolvedLedgerPartyCount { get; set; }
        public bool ReturningLedgerPartyPresent { get; set; }
        public string ReturningLedgerPartyId { get; set; }
        public bool HostAssignmentPresent { get; set; }
        public string HostControllerId { get; set; }
        public string[] SuccessorControllerIds { get; set; } = Array.Empty<string>();
        public int HostEpoch { get; set; }
        public RejoinReservePartyState[] LedgerParties { get; set; } = Array.Empty<RejoinReservePartyState>();
        public RejoinReserveSideState[] SideTotals { get; set; } = Array.Empty<RejoinReserveSideState>();
    }

    public sealed class RejoinObservationCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.players";

        public string Name => "rejoin_observation";

        public string Description => "Reports authoritative returning-player reserve and battle ownership state.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The returning player controller id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer)
                return Failed("Command can only be run on the server.");
            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
                return Failed($"Unable to get {nameof(IPlayerManager)}");
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return Failed($"Unable to get {nameof(IObjectManager)}");

            var state = new RejoinObservationState
            {
                Success = true,
                ControllerId = args[0],
            };
            if (!playerManager.TryGetPlayer(args[0], out var player))
                return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));

            state.PlayerRegistered = true;
            state.PlayerConnected = playerManager.IsConnected(player);
            state.RegisteredHeroId = player.HeroId;
            state.RegisteredPartyId = player.MobilePartyId;
            if (objectManager.TryGetObject(player.HeroId, out Hero hero))
            {
                state.ReturningHeroResolved = true;
                state.ReturningHeroControlled = playerManager.Contains(hero);
                state.ReturningHeroStringId = hero.StringId;
            }
            if (!objectManager.TryGetObject(player.MobilePartyId, out MobileParty party))
                return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));

            state.ReturningPartyResolved = true;
            state.ReturningPartyControlled = playerManager.Contains(party);
            state.ReturningPartyStringId = party.StringId;
            state.ReturningPartyActive = party.IsActive;
            MapEvent mapEvent = party.MapEvent;
            if (mapEvent == null || !objectManager.TryGetId(mapEvent, out string mapEventId))
                return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));

            state.MapEventPresent = true;
            state.MapEventId = mapEventId;
            state.MapEventStringId = mapEvent.StringId;
            if (ContainerProvider.TryResolve<IBattleHostRegistry>(out var hostRegistry) &&
                hostRegistry.TryGet(mapEventId, out var assignment))
            {
                state.HostAssignmentPresent = true;
                state.HostControllerId = assignment.HostControllerId;
                state.SuccessorControllerIds = assignment.SuccessorControllerIds?.ToArray() ?? Array.Empty<string>();
                state.HostEpoch = assignment.Epoch;
            }

            if (!ContainerProvider.TryResolve<IBattleTroopLedger>(out var ledger))
                return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));

            state.LedgerAvailable = true;
            var reserves = new List<RejoinReservePartyState>();
            foreach (string ledgerPartyId in ledger.GetParties(mapEventId).OrderBy(id => id, StringComparer.Ordinal))
            {
                bool reserveAvailable = ledger.TryGetReserve(
                    mapEventId,
                    ledgerPartyId,
                    out var entries,
                    out int suppliedCount);
                if (!reserveAvailable || entries == null)
                {
                    entries = Array.Empty<TroopReserveEntry>();
                    suppliedCount = 0;
                }
                bool mapEventPartyResolved = objectManager.TryGetObject<MapEventParty>(
                    ledgerPartyId,
                    out var mapEventParty);
                bool sideResolved = mapEventPartyResolved && mapEventParty?.Party != null;
                bool isReturningParty = sideResolved && ReferenceEquals(mapEventParty.Party, party.Party);
                var reserve = new RejoinReservePartyState
                {
                    LedgerPartyId = ledgerPartyId,
                    ReserveAvailable = reserveAvailable,
                    MapEventPartyResolved = mapEventPartyResolved,
                    SideResolved = sideResolved,
                    Side = sideResolved ? mapEventParty.Party.Side.ToString() : "unresolved",
                    IsReturningParty = isReturningParty,
                    ReturningPartyAssociation = !mapEventPartyResolved
                        ? "map-event-party-unresolved"
                        : !sideResolved
                            ? "map-event-party-has-no-party"
                            : isReturningParty
                                ? "returning-party-resolved"
                                : "other-party-resolved",
                    SuppliedCount = suppliedCount,
                    RemainingCount = entries.Count - suppliedCount,
                    Entries = entries.Select(entry => new RejoinReserveEntryState
                    {
                        Seed = entry.Seed,
                        CharacterId = entry.CharacterId,
                        FormationClass = entry.FormationClass,
                        SupplyOrder = entry.SupplyOrder,
                    }).ToArray(),
                };
                reserves.Add(reserve);
                if (!mapEventPartyResolved || !sideResolved)
                    state.UnresolvedLedgerPartyCount++;
                if (isReturningParty)
                {
                    state.ReturningLedgerPartyPresent = true;
                    state.ReturningLedgerPartyId = ledgerPartyId;
                }
            }

            state.AuthoritativeLedgerPartyCount = reserves.Count;
            state.LedgerParties = reserves
                .OrderBy(reserve => reserve.Side, StringComparer.Ordinal)
                .ThenBy(reserve => reserve.LedgerPartyId, StringComparer.Ordinal)
                .ToArray();
            state.SideTotals = state.LedgerParties
                .GroupBy(reserve => reserve.Side, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RejoinReserveSideState
                {
                    Side = group.Key,
                    EntryCount = group.Sum(reserve => reserve.Entries.Length),
                    SuppliedCount = group.Sum(reserve => reserve.SuppliedCount),
                    RemainingCount = group.Sum(reserve => reserve.RemainingCount),
                })
                .ToArray();
            return Succeeded("LIVE_TEST_JSON=" + JsonConvert.SerializeObject(state));
        }
    }
#endif

    /// <summary>
    /// Reports one of a player's controlled ids: whether it resolves and whether it is in the
    /// PlayerManager's control table. Returns 1 when both hold, otherwise 0.
    /// </summary>
    private static int AppendObject<T>(
        StringBuilder sb,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        string label,
        string id) where T : class
    {
        if (string.IsNullOrEmpty(id))
        {
            sb.AppendLine($"    {label}: <none>");
            return 0;
        }

        if (objectManager.TryGetObject<T>(id, out var obj) == false)
        {
            sb.AppendLine($"    {label}: {id} <NOT RESOLVED>");
            return 0;
        }

        bool controlled = playerManager.Contains(obj);
        sb.AppendLine($"    {label}: {id} resolved, controlled={controlled}");
        return controlled ? 1 : 0;
    }
}
