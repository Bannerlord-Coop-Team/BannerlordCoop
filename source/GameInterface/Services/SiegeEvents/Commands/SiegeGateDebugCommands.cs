#if DEBUG
using Common.Commands;
using Common.Logging;
using GameInterface.Services.MapEvents;
using Serilog;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.SiegeEvents.Commands;

/// <summary>Reads and hits one castle gate of the running siege mission, so peers can compare what each one applied.</summary>
internal static class SiegeGateDebugCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(SiegeGateDebugCommands));

    // coop.debug.siege.gate_state
    public sealed class GateStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "gate_state";
        public string Description => "Prints a castle gate's hit points and what its destruction changed locally.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("gate_id", "Native mission object id from dump_machines all."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryFindGate(args[0], out var gate, out var error)) return Failed(error);

            string state = DescribeGate(gate);
            Logger.Information("[GateState] {State}", state);
            return new CoopCommandResult(true, state);
        }
    }

    // coop.debug.siege.gate_melee_hit
    public sealed class GateMeleeHitCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "gate_melee_hit";
        public string Description => "Hits a castle gate with the local player's wielded weapon, like an infantry strike.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("gate_id", "Native mission object id from dump_machines all."),
            new ExpectedArgs("damage", "Damage per hit."),
            new ExpectedArgs("count", "Number of hits, 1 when omitted.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryFindGate(args[0], out var gate, out var error)) return Failed(error);
            if (!int.TryParse(args[1], out int damage) || damage <= 0) return Failed($"Invalid damage '{args[1]}'");

            int count = 1;
            if (args.Count > 2 && (!int.TryParse(args[2], out count) || count <= 0))
                return Failed($"Invalid count '{args[2]}'");

            var destruction = gate.DestructionComponent;
            if (destruction == null) return Failed($"Gate {gate.Id.Id} has no destruction component");

            var agent = Agent.Main;
            if (agent == null) return Failed("No local player agent");

            // Vanilla zeroes the damage of an empty weapon unless a ram hits.
            var slot = agent.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None) return Failed("The local player agent wields no weapon");

            var weapon = agent.Equipment[slot];
            var frame = gate.GameEntity.GetGlobalFrame();
            for (int hit = 0; hit < count; hit++)
            {
                destruction.TriggerOnHit(agent, damage, frame.origin, frame.rotation.f, in weapon, (int)slot, null);
            }

            string state = DescribeGate(gate);
            Logger.Information("[GateMeleeHit] damage={Damage} count={Count} {State}", damage, count, state);
            return new CoopCommandResult(true, state);
        }
    }

    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");

    private static bool TryFindGate(string gateId, out CastleGate gate, out string error)
    {
        gate = null;
        error = null;
        var mission = Mission.Current;
        if (mission == null)
        {
            error = "No mission is running";
            return false;
        }

        if (!int.TryParse(gateId, out int id))
        {
            error = $"Invalid gate id '{gateId}'";
            return false;
        }

        gate = mission.MissionObjects.OfType<CastleGate>().FirstOrDefault(candidate => candidate.Id.Id == id);
        if (gate != null) return true;

        error = $"No castle gate with id {id}";
        return false;
    }

    private static string DescribeGate(CastleGate gate)
    {
        var destruction = gate.DestructionComponent;
        int points = gate.StandingPoints.Count;
        int pointsOff = gate.StandingPoints.Count(point => point.IsDeactivated);
        CountGateNavMeshFaces(gate, out int enabledFaces, out int faces);

        return $"gate {gate.Id.Id:D5}" +
            $" hp={(destruction != null ? destruction.HitPoint : -1f):0.#}" +
            $" max={(destruction != null ? destruction.MaxHitPoint : -1f):0.#}" +
            $" destroyed={(gate.IsDestroyed ? 1 : 0)} state={gate.State}" +
            $" plankVisible={(gate._plank?.GameEntity.IsVisibleIncludeParents() == true ? 1 : 0)}" +
            $" standingPointsDeactivated={(pointsOff == points ? 1 : 0)} ptsOff={pointsOff}/{points}" +
            $" navMeshOpen={(faces > 0 && enabledFaces == faces ? 1 : 0)} navFaces={enabledFaces}/{faces}" +
            $" isHost={(SiegeMissionAuthorityGate.IsLocalAuthority ? 1 : 0)}" +
            $" simLocal={(SiegeMissionAuthorityGate.IsMachineSimulatedLocally(gate.Id.Id) ? 1 : 0)}";
    }

    // No getter reads a face's ability, so count the gate faces a disabled-skipping lookup still finds.
    private static void CountGateNavMeshFaces(CastleGate gate, out int enabledFaces, out int faces)
    {
        enabledFaces = 0;
        faces = 0;
        var scene = gate.Scene;
        if (scene == null) return;

        int faceCount = scene.GetNavMeshFaceCount();
        for (int face = 0; face < faceCount; face++)
        {
            if (scene.GetIdOfNavMeshFace(face) != gate.NavigationMeshId) continue;

            faces++;
            var center = Vec3.Zero;
            scene.GetNavMeshCenterPosition(face, ref center);
            var record = PathFaceRecord.NullFaceRecord;
            scene.GetNavMeshFaceIndex(ref record, center, true);
            if (record.IsValid() && record.FaceGroupIndex == gate.NavigationMeshId) enabledFaces++;
        }
    }
}
#endif
