using Common;
using Common.Commands;
using Coop.Naval.Storms.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using NavalDLC;
using NavalDLC.Map;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Coop.Naval.Storms.Commands;

internal class StormDebugCommands
{
    private const string CommandPrefix = "coop.debug.naval";
    private const float DefaultSpawnDistance = 15f;

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    // coop.debug.naval.spawn_storm
    public sealed class SpawnStormCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;
        private readonly IPlayerManager playerManager;
        private readonly IStormInterface stormInterface;

        public SpawnStormCoopCommand(IObjectManager objectManager, IPlayerManager playerManager, IStormInterface stormInterface)
        {
            this.objectManager = objectManager;
            this.playerManager = playerManager;
            this.stormInterface = stormInterface;
        }

        public string Prefix => CommandPrefix;
        public string Name => "spawn_storm";
        public string Description => "Spawns a storm next to a party through StormManager.CreateStormAtPosition.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("partyId", "Registered party id, defaults to the first player party.", isRequired: false),
            new ExpectedArgs("type", "storm, thunderstorm or hurricane, defaults to the type the storm model picks.", isRequired: false),
            new ExpectedArgs("distance", $"Distance east of the party, defaults to {DefaultSpawnDistance}.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");

            var stormManager = NavalDLCManager.Instance?.StormManager;
            if (stormManager == null) return Failed("NavalDLC storm manager is not available.");

            if (!TryGetParty(args, out var party, out var error)) return Failed(error);

            Storm.StormTypes? stormType = null;
            if (args.Count > 1)
            {
                if (!Enum.TryParse<Storm.StormTypes>(args[1], ignoreCase: true, out var parsedType))
                    return Failed($"Unknown storm type {args[1]}, expected {string.Join(", ", Enum.GetNames(typeof(Storm.StormTypes)))}.");

                stormType = parsedType;
            }

            var distance = DefaultSpawnDistance;
            if (args.Count > 2 && !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out distance))
                return Failed($"Invalid distance {args[2]}.");

            var position = party.Position.ToVec2() + new Vec2(distance, 0f);

            if (stormType.HasValue)
                stormManager.CreateStormAtPosition(position, stormType.Value);
            else
                stormManager.CreateStormAtPosition(position);

            var storm = stormInterface.SpawnedStorms.Last();
            objectManager.TryGetId(storm, out var stormId);

            return Succeeded($"Spawned {storm.StormType} {stormId} at {FormatVec2(storm.CurrentPosition)} near {party.StringId} ({party.Name})");
        }

        private bool TryGetParty(ICoopCommandArgs args, out MobileParty party, out string error)
        {
            party = null;
            error = null;

            if (args.Count > 0)
            {
                if (objectManager.TryGetObject(args[0], out party)) return true;

                error = $"Party with id {args[0]} not found.";
                return false;
            }

            foreach (var player in playerManager.Players)
            {
                if (objectManager.TryGetObject(player.MobilePartyId, out party)) return true;
            }

            error = "No player party found, pass a party id from coop.debug.mobileparty.list.";
            return false;
        }
    }

    public sealed class ListStormsCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;
        private readonly IStormInterface stormInterface;

        public ListStormsCoopCommand(IObjectManager objectManager, IStormInterface stormInterface)
        {
            this.objectManager = objectManager;
            this.stormInterface = stormInterface;
        }

        public string Prefix => CommandPrefix;
        public string Name => "list_storms";
        public string Description => "Lists spawned storms, run on the server and a client to compare.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var storms = stormInterface.SpawnedStorms;
            var sb = new StringBuilder();
            sb.AppendLine($"{storms.Count} storm(s) on {(ModInformation.IsServer ? "SERVER" : "CLIENT")}:");

            foreach (var storm in storms)
            {
                var id = objectManager.TryGetId(storm, out var stormId) ? stormId : "<unregistered>";
                var state = storm.IsInDevelopingState ? "Developing" : storm.IsInFinalizingState ? "Finalizing" : "Active";

                sb.AppendLine($"{id} {storm.StormType} {state} pos={FormatVec2(storm.CurrentPosition)} " +
                    $"dir={FormatVec2(storm._currentMoveDirection)} intensity={storm.Intensity:0.000} radius={storm.EffectRadius:0.0} " +
                    $"finalizingAt={storm._finalizingStateStartCampaignTime}");
            }

            return Succeeded(sb.ToString());
        }
    }

    // coop.debug.naval.deactivate_storm Storm_Created_1
    public sealed class DeactivateStormCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;

        public DeactivateStormCoopCommand(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }

        public string Prefix => CommandPrefix;
        public string Name => "deactivate_storm";
        public string Description => "Calls Storm.ForceDeactivate so the storm finalizes and is removed.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("stormId", "Registered storm id from coop.debug.naval.list_storms."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Command can only be run on the server.");
            if (args.Count != 1) return Failed("Expected a registered storm id.");
            if (!objectManager.TryGetObject<Storm>(args[0], out var storm)) return Failed($"Storm with id {args[0]} not found.");

            storm.ForceDeactivate();
            return Succeeded($"Deactivated {args[0]}, it is removed once its finalizing state ends.");
        }
    }

    private static string FormatVec2(Vec2 vec) =>
        string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00})", vec.X, vec.Y);
}
