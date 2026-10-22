using Common;
using Common.Commands;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Commands;
using Serilog;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Party.Commands;

/// <summary>Debug commands that give, list, damage and remove a player's War Sails ships.</summary>
public class PartyShipCommands
{
    internal const string DefaultHullId = "northern_light_ship";

    private static readonly ILogger Logger = LogManager.GetLogger<PartyShipCommands>();

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.party.add_ship PlayerOne northern_light_ship
    /// <summary>Gives a player's party one new ship through the vanilla production ownership path.</summary>
    public sealed class AddShipCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.party";

        public string Name => "add_ship";

        public string Description => "Gives a player's party one new ship of the given War Sails hull.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player whose party receives the ship.", true),
            new ExpectedArgs("ship_hull_id", $"The hull id, {DefaultHullId} when omitted.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            if (!ModInformation.IsNavalDlcActive)
                return Failed("NavalDLC is not active on the server.");

            var hullId = args.Count == 2 ? args[1] : DefaultHullId;
            if (!MapEventDebugCommands.TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var party, out var error))
                return Failed(error);

            var hull = MBObjectManager.Instance.GetObject<ShipHull>(hullId);
            if (hull == null)
                return Failed($"No ship hull has id {hullId}.");

            var ship = new Ship(hull);
            ChangeShipOwnerAction.ApplyByProduction(party.Party, ship);

            var shipId = DescribeId(objectManager, ship);
            Logger.Information("Added {HullId} ship {ShipId} to party {PartyId} of player {ControllerId}",
                hullId, shipId, party.StringId, args[0]);
            return Succeeded($"Added {hullId} {shipId} to {party.StringId} (ships={party.Ships.Count}).");
        }
    }

    // coop.debug.party.damage_ship PlayerOne 0 200
    /// <summary>Damages one of a player's ships through the vanilla damage path, which sinks it at 0 hit points.</summary>
    public sealed class DamageShipCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.party";

        public string Name => "damage_ship";

        public string Description => "Damages a player's ship by index (from list_ships); it sinks at 0 hit points.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player whose ship is damaged.", true),
            new ExpectedArgs("ship_index", "The ship's index in list_ships.", true),
            new ExpectedArgs("damage", "Raw damage before the ship damage model, greater than 0.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            if (!ModInformation.IsNavalDlcActive)
                return Failed("NavalDLC is not active on the server.");

            if (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var damage) || damage <= 0f)
                return Failed("damage must be a number greater than 0.");

            if (!TryGetPlayerShip(args[0], args[1], out var objectManager, out var ship, out var error))
                return Failed(error);

            var shipId = DescribeId(objectManager, ship);
            ship.OnShipDamaged(damage, null, out _);

            return Succeeded(ship.Owner == null
                ? $"{shipId} sank."
                : $"{shipId} hp={ship.HitPoints:0}/{ship.MaxHitPoints:0}.");
        }
    }

    // coop.debug.party.remove_ship PlayerOne 0
    /// <summary>Destroys one of a player's ships through DestroyShipAction.</summary>
    public sealed class RemoveShipCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.party";

        public string Name => "remove_ship";

        public string Description => "Destroys a player's ship by index (from list_ships).";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player whose ship is destroyed.", true),
            new ExpectedArgs("ship_index", "The ship's index in list_ships.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            if (!ModInformation.IsNavalDlcActive)
                return Failed("NavalDLC is not active on the server.");

            if (!TryGetPlayerShip(args[0], args[1], out var objectManager, out var ship, out var error))
                return Failed(error);

            var shipId = DescribeId(objectManager, ship);
            DestroyShipAction.Apply(ship);

            return Succeeded($"Removed {shipId}.");
        }
    }

    // coop.debug.party.add_troops PlayerOne 2
    /// <summary>Gives a player's party the mobile_party.add_troops template, addressed by controller id instead of hero name.</summary>
    public sealed class AddTroopsCoopCommand : ICoopCommand
    {
        private const int MaxTemplates = 20;

        public string Prefix => "coop.debug.party";

        public string Name => "add_troops";

        public string Description => "Gives a player's party the add_troops template (11 troops) count times.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player whose party receives the troops.", true),
            new ExpectedArgs("count", $"How many 11-troop templates to add, 1 to {MaxTemplates}; 1 when omitted.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("Run this command on the server.");

            int count = 1;
            if (args.Count == 2 &&
                (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1 || count > MaxTemplates))
                return Failed($"count must be an integer from 1 to {MaxTemplates}.");

            if (!MapEventDebugCommands.TryGetPlayerParty(args[0], requireReady: false, out var objectManager, out var party, out var error))
                return Failed(error);

            var log = new StringBuilder();
            for (int i = 0; i < count; i++)
                PartyCommands.AddTroopTemplate(party.MemberRoster, objectManager, log);

            return Succeeded(log + $"{party.StringId} now has {party.MemberRoster.TotalManCount} members.");
        }
    }

    // coop.debug.party.list_ships PlayerOne
    // coop.debug.party.list_ships MobileParty_southern_pirates_13
    /// <summary>Lists the ships this machine's copy of a party owns, addressed by player controller id or party id.</summary>
    public sealed class ListShipsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.party";

        public string Name => "list_ships";

        public string Description => "Lists the ships of a party on this machine.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("party", "A player controller id, or a MobileParty or PartyBase id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsNavalDlcActive)
                return Failed("NavalDLC is not active.");

            if (!TryGetListedParty(args[0], out var objectManager, out var party, out var error))
                return Failed(error);

            var ships = party.Ships
                .Select((ship, index) => $"[{index}] {DescribeId(objectManager, ship)} {ship.ShipHull.StringId} " +
                    $"name=\"{ship.Name}\" owner={ship.Owner?.Id ?? "none"} " +
                    $"hp={ship.HitPoints:0}/{ship.MaxHitPoints:0} sail={ship.SailHitPoints:0}/{ship.MaxSailHitPoints:0}")
                .ToArray();
            return Succeeded($"{party.Id} ships={ships.Length}" +
                   (ships.Length == 0 ? "" : ": " + string.Join(", ", ships)));
        }
    }

#if DEBUG
    // coop.debug.naval.fleet
    // coop.debug.naval.fleet MobileParty_southern_pirates_13
    /// <summary>
    /// Lists the campaign id, HP, sail HP and owner of every ship of a party on this machine; with no party, the main party on
    /// a client and every player's party on the server.
    /// </summary>
    public sealed class NavalFleetCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.naval";

        public string Name => "fleet";

        public string Description => "Lists a party's ships with their id, hit points, sail hit points and owner on this machine.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("party", "A player controller id, or a MobileParty or PartyBase id; every player's party when omitted.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsNavalDlcActive)
                return Failed("NavalDLC is not active.");

            if (args.Count == 1)
            {
                if (!TryGetListedParty(args[0], out var objectManager, out var party, out var error))
                    return Failed(error);

                return Succeeded(DescribeFleet(objectManager, party));
            }

            if (!ContainerProvider.TryResolve<IObjectManager>(out var manager) || !ContainerProvider.TryResolve<IPlayerManager>(out var players))
                return Failed("Unable to resolve ObjectManager or PlayerManager.");

            var parties = ModInformation.IsClient
                ? new[] { MobileParty.MainParty?.Party }
                : players.Players.Select(player => manager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ? party.Party : null);
            var fleets = parties.Where(party => party != null).Select(party => DescribeFleet(manager, party)).ToArray();
            return fleets.Length == 0 ? Failed("No player party to list.") : Succeeded(string.Join("\n", fleets));
        }
    }

    // One line per party: its id and ship count, then each ship's registry id, hit points, sail hit points and owner.
    private static string DescribeFleet(IObjectManager objectManager, PartyBase party)
    {
        var ships = party.Ships
            .Select(ship => $"{DescribeId(objectManager, ship)} hp={ship.HitPoints:0.#}/{ship.MaxHitPoints:0} " +
                $"sail={ship.SailHitPoints:0.#}/{ship.MaxSailHitPoints:0} owner={ship.Owner?.Id ?? "none"}")
            .ToArray();
        return $"{party.Id} ships={ships.Length}" + (ships.Length == 0 ? "" : ": " + string.Join(", ", ships));
    }
#endif

    // A controller id names the player's party; any other id is looked up as a registered MobileParty or PartyBase.
    private static bool TryGetListedParty(string id, out IObjectManager objectManager, out PartyBase party, out string error)
    {
        party = null;
        if (MapEventDebugCommands.TryGetPlayerParty(id, requireReady: false, out objectManager, out var playerParty, out error))
        {
            party = playerParty.Party;
            return true;
        }

        if (objectManager == null) return false;

        if (objectManager.TryGetObject<object>(id, out var registered))
            party = registered is MobileParty mobileParty ? mobileParty.Party : registered as PartyBase;

        error = party == null ? $"No player, MobileParty or PartyBase has id {id}." : null;
        return party != null;
    }

    private static string DescribeId(IObjectManager objectManager, Ship ship) =>
        objectManager.TryGetId(ship, out var shipId) ? shipId : "unregistered";

    private static bool TryGetPlayerShip(string controllerId, string indexText, out IObjectManager objectManager, out Ship ship, out string error)
    {
        ship = null;
        if (!MapEventDebugCommands.TryGetPlayerParty(controllerId, requireReady: false, out objectManager, out var party, out error))
            return false;

        if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ||
            index < 0 || index >= party.Ships.Count)
        {
            error = $"ship_index must be an integer from 0 to {party.Ships.Count - 1}; {party.StringId} has {party.Ships.Count} ships.";
            return false;
        }

        ship = party.Ships[index];
        return true;
    }
}
