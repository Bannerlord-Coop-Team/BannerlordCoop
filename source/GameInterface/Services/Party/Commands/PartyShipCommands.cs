using Common;
using Common.Commands;
using Common.Logging;
using GameInterface.Services.Villages.Commands;
using Serilog;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Party.Commands;

/// <summary>Debug commands that give a player's party War Sails ships and list them.</summary>
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
            if (!MapEventDebugCommands.TryGetPlayerParty(args[0], requireReady: false, out _, out var party, out var error))
                return Failed(error);

            var hull = MBObjectManager.Instance.GetObject<ShipHull>(hullId);
            if (hull == null)
                return Failed($"No ship hull has id {hullId}.");

            ChangeShipOwnerAction.ApplyByProduction(party.Party, new Ship(hull));

            // Campaign ships are not registered or replicated, so the client only sees it after reloading the server save.
            Logger.Information("Added a {HullId} ship to party {PartyId} of player {ControllerId}; the client must rejoin to see the ship",
                hullId, party.StringId, args[0]);
            return Succeeded($"Added {hullId} to {party.StringId} (ships={party.Ships.Count}). " +
                   $"Player {args[0]} must rejoin to see the ship.");
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
    /// <summary>Lists the ships this machine's copy of a player's party owns.</summary>
    public sealed class ListShipsCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.party";

        public string Name => "list_ships";

        public string Description => "Lists the ships of a player's party on this machine.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player whose party ships are listed.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsNavalDlcActive)
                return Failed("NavalDLC is not active.");

            if (!MapEventDebugCommands.TryGetPlayerParty(args[0], requireReady: false, out _, out var party, out var error))
                return Failed(error);

            var ships = party.Ships
                .Select(ship => $"{ship.ShipHull.StringId}(hp={ship.HitPoints:0}/{ship.MaxHitPoints:0})")
                .ToArray();
            return Succeeded($"{party.StringId} ships={ships.Length}" +
                   (ships.Length == 0 ? "" : ": " + string.Join(", ", ships)));
        }
    }
}
