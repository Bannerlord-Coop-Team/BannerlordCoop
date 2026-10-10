#if DEBUG
using Common.Commands;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Missions.Naval;

/// <summary>DEBUG: rope throws from the own hull at the nearest copied hull, through vanilla's connect path.</summary>
public static class NavalRopeDebugCommands
{
    /// <summary>Vanilla drops a flying hook farther than this from its station (DistanceSquared &gt; 1600).</summary>
    internal const float ThrowRange = 40f;

    internal static bool IsTargetFree(ShipAttachmentPointMachine target) =>
        target.CurrentAttachment == null && target.LinkedAttachmentMachine?.CurrentAttachment == null;

    internal static bool IsStationFree(ShipAttachmentMachine source) =>
        source.CurrentAttachment == null && source.LinkedAttachmentPointMachine?.CurrentAttachment == null;

    internal static float Distance(ShipAttachmentMachine source, ShipAttachmentPointMachine target) =>
        source.GameEntity.GlobalPosition.Distance(NavalDebugHulls.HookPosition(target));

    // Vanilla's own attachment score for this pair, ignoring interaction range and blocking.
    internal static bool IsAligned(ShipAttachmentMachine source, ShipAttachmentPointMachine target) =>
        ShipAttachmentMachine.ComputePotentialAttachmentValue(source, target, checkInteractionDistance: false,
            checkConnectionBlock: false, allowWiderAngleBetweenConnections: true) > 0f;

    internal static bool IsInRange(float distance) => !float.IsNaN(distance) && distance <= ThrowRange;

    /// <summary>The closest free, aligned point in range, or -1.</summary>
    internal static int BestTarget(ShipAttachmentMachine source, ShipAttachmentPointMachine[] targets, out float distance)
    {
        int best = -1;
        distance = float.MaxValue;
        for (int index = 0; index < targets.Length; index++)
        {
            float candidate = Distance(source, targets[index]);
            if (candidate >= distance || !IsInRange(candidate) || !IsTargetFree(targets[index]) || !IsAligned(source, targets[index]))
                continue;

            best = index;
            distance = candidate;
        }

        return best;
    }

    internal static bool TryParseThrow(IReadOnlyList<string> args, out int source, out int? target, out string error)
    {
        source = -1;
        target = null;
        error = null;
        if (args == null || args.Count < 1 || args.Count > 2)
            error = "Usage: coop.debug.naval.rope_throw <source_station_index> [target_station_index]";
        else if (!TryParseIndex(args[0], out source))
            error = "source_station_index must be a whole number from 0.";
        else if (args.Count == 2)
        {
            if (TryParseIndex(args[1], out int value)) target = value;
            else error = "target_station_index must be a whole number from 0.";
        }

        return error == null;
    }

    private static bool TryParseIndex(string value, out int index) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0;

    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");

    private static string KeyOf(TaleWorlds.Engine.ScriptComponentBehavior script, MissionShip hull) =>
        NavalShipEngine.EntityPath(script.GameEntity, hull.GameEntity);

    // coop.debug.naval.rope_list
    /// <summary>Lists the own hull's rope stations and the nearest copied hull's attachment points with distances.</summary>
    public sealed class RopeListCoopCommand : ICoopCommand
    {
        private readonly INavalDebugHulls hulls;

        public RopeListCoopCommand(INavalDebugHulls hulls)
        {
            this.hulls = hulls;
        }

        public string Prefix => "coop.debug.naval";

        public string Name => "rope_list";

        public string Description => "Lists this client's own rope stations and the nearest other hull's attachment points with distances.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string error = hulls.TryGetOwnHull(out var own);
            if (error != null) return Failed(error);

            var sources = hulls.RopeStations(own);
            var foreign = hulls.NearestForeignHull(own);
            var targets = foreign == null ? Array.Empty<ShipAttachmentPointMachine>() : hulls.AttachmentPoints(foreign);
            var state = new
            {
                ownShip = hulls.ShipIdOf(own),
                foreignShip = foreign == null ? (Guid?)null : hulls.ShipIdOf(foreign),
                hullDistance = foreign == null ? (float?)null : own.GlobalFrame.origin.Distance(foreign.GlobalFrame.origin),
                throwRange = ThrowRange,
                sources = sources.Select((source, index) =>
                {
                    int best = BestTarget(source, targets, out float distance);
                    return new
                    {
                        index,
                        key = KeyOf(source, own),
                        user = source.PilotAgent?.Name,
                        free = IsStationFree(source),
                        state = source.CurrentAttachment?.State.ToString(),
                        bestTarget = best < 0 ? (int?)null : best,
                        bestDistance = best < 0 ? (float?)null : distance,
                    };
                }).ToArray(),
                targets = targets.Select((target, index) =>
                {
                    var nearest = sources.Select((source, sourceIndex) => (sourceIndex, distance: Distance(source, target)))
                        .OrderBy(pair => pair.distance).FirstOrDefault();
                    return new
                    {
                        index,
                        key = KeyOf(target, foreign),
                        free = IsTargetFree(target),
                        nearestSource = sources.Length == 0 ? (int?)null : nearest.sourceIndex,
                        distance = sources.Length == 0 ? (float?)null : nearest.distance,
                    };
                }).ToArray(),
            };

            return new CoopCommandResult(true, "NAVAL_ROPE_LIST " + JsonConvert.SerializeObject(state));
        }
    }

    // coop.debug.naval.rope_throw 3 12
    /// <summary>Throws a rope from an own station at a point of the nearest copied hull.</summary>
    public sealed class RopeThrowCoopCommand : ICoopCommand
    {
        private readonly INavalDebugHulls hulls;

        public RopeThrowCoopCommand(INavalDebugHulls hulls)
        {
            this.hulls = hulls;
        }

        public string Prefix => "coop.debug.naval";

        public string Name => "rope_throw";

        public string Description => "Throws a rope from an own rope station at the nearest other hull through vanilla's connect path.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("source_station_index", "The own rope station, as listed by coop.debug.naval.rope_list.", true),
            new ExpectedArgs("target_station_index", "The other hull's attachment point; the closest eligible one when omitted.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryParseThrow(args, out int sourceIndex, out int? targetIndex, out string error)) return Failed(error);

            error = hulls.TryGetOwnHull(out var own);
            if (error != null) return Failed(error);

            var sources = hulls.RopeStations(own);
            if (sourceIndex >= sources.Length) return Failed($"The own hull has {sources.Length} rope stations.");
            var source = sources[sourceIndex];
            if (source.PilotAgent != null) return Failed($"Rope station {sourceIndex} is in use by {source.PilotAgent.Name}.");
            if (!IsStationFree(source)) return Failed($"Rope station {sourceIndex} already holds a rope.");

            var foreign = hulls.NearestForeignHull(own);
            if (foreign == null) return Failed("There is no other player's hull to throw at.");
            var targets = hulls.AttachmentPoints(foreign);

            int chosen;
            float distance;
            if (targetIndex == null)
            {
                chosen = BestTarget(source, targets, out distance);
                if (chosen < 0) return Failed($"No free, aligned attachment point is within {ThrowRange} m of rope station {sourceIndex}.");
            }
            else
            {
                chosen = targetIndex.Value;
                if (chosen >= targets.Length) return Failed($"The other hull has {targets.Length} attachment points.");
                distance = Distance(source, targets[chosen]);
                if (!IsInRange(distance)) return Failed($"Attachment point {chosen} is {distance:F1} m away, beyond {ThrowRange} m.");
                if (!IsTargetFree(targets[chosen])) return Failed($"Attachment point {chosen} already holds a rope.");
                if (!IsAligned(source, targets[chosen])) return Failed($"Attachment point {chosen} does not face rope station {sourceIndex}.");
            }

            var target = targets[chosen];
            // Explicit target selection bypasses the player's aim, not the rope's flight, pull or break behavior.
            source.ConnectWithAttachmentPointMachine(target, forceBridge: false);
            if (source.CurrentAttachment == null) return Failed("Vanilla did not create the rope.");

            return new CoopCommandResult(true, string.Format(CultureInfo.InvariantCulture,
                "NAVAL_ROPE_THROW ownShip={0} source={1}:{2} targetShip={3} target={4}:{5} distance={6:F1} state={7}",
                hulls.ShipIdOf(own), sourceIndex, KeyOf(source, own), hulls.ShipIdOf(foreign), chosen, KeyOf(target, foreign),
                distance, source.CurrentAttachment.State));
        }
    }
}
#endif
