#if DEBUG
using Common.Commands;
using GameInterface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem.Party;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace Missions.Battles;

// Temporary reproduction diagnostics, excluded from the product fix.
internal class BattleReproductionFixtureCommands
{
    public sealed class ObserveCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.battle";
        public string Name => "reproduction_observe";
        public string Description => "Reads complete mission and registry agent state.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var mission = Mission.Current;
            var session = mission?.GetMissionBehavior<CoopBattleController>()?.Session;
            ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry);
            var missionAgents = mission?.Agents.ToArray() ?? Array.Empty<Agent>();
            var missionSet = new HashSet<Agent>(missionAgents);
            var registered = registry == null ? Array.Empty<CoopAgentInfo>() :
                registry.GetControllerIds().SelectMany(registry.GetAgents).ToArray();
            var fixture = GetFixture(mission);
            var infos = registered.Where(info => info.Agent != null)
                .GroupBy(info => info.Agent).ToDictionary(group => group.Key, group => group.First());
            if (fixture != null)
                foreach (var entry in infos) fixture.Identities[entry.Key] = entry.Value;
            var allAgents = missionAgents.Concat(infos.Keys).Distinct().ToArray();
            var observations = new List<object>();
            foreach (var agent in allAgents)
            {
                bool isRegistered = infos.TryGetValue(agent, out var info);
                if (info == null && fixture != null) fixture.Identities.TryGetValue(agent, out info);
                try
                {
                    bool active = agent.IsActive();
                    // Removed registry entries remain observable without touching expired native visuals.
                    var entity = active ? agent.AgentVisuals?.GetEntity() : null;
                    observations.Add(new
                    {
                        sampledAtUtc = DateTime.UtcNow,
                        registered = isRegistered,
                        missing = false,
                        agentId = info?.AgentId.ToString("D"),
                        originalOwner = info?.OriginalOwner,
                        authority = info?.CurrentAuthority,
                        movementScopeId = info?.MovementScopeId,
                        movementId = info?.MovementId,
                        authorityRevision = info?.AuthorityRevision,
                        inMission = missionSet.Contains(agent),
                        index = agent.Index,
                        characterId = agent.Character?.StringId,
                        characterName = agent.Character?.Name?.ToString(),
                        isHuman = agent.IsHuman,
                        isHero = agent.IsHero,
                        isMainAgent = ReferenceEquals(agent, mission?.MainAgent),
                        side = agent.Team?.Side.ToString(),
                        state = agent.State.ToString(),
                        active,
                        health = agent.Health,
                        healthLimit = agent.HealthLimit,
                        hasVisualEntity = !ReferenceEquals(entity, null),
                        visualVisible = !ReferenceEquals(entity, null) && entity.IsVisibleIncludeParents(),
                        position = active ? new { x = agent.Position.x, y = agent.Position.y, z = agent.Position.z } : null,
                        velocity = active ? new { x = agent.GetRealGlobalVelocity().x,
                            y = agent.GetRealGlobalVelocity().y, z = agent.GetRealGlobalVelocity().z } : null,
                        observationError = (string)null
                    });
                }
                catch (Exception error)
                {
                    observations.Add(new { agentId = info?.AgentId.ToString("D"),
                        inMission = missionSet.Contains(agent), observationError = error.ToString() });
                }
            }
            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
            {
                success = mission != null && session != null && registry != null,
                sampledAtUtc = DateTime.UtcNow,
                missionInstanceId = session?.InstanceId,
                localControllerId = session?.OwnControllerId,
                hostControllerId = session?.HostControllerId,
                hostEpoch = session?.HostEpoch,
                registryAvailable = registry != null,
                missionAgentCount = missionAgents.Length,
                registryAgentCount = registered.Length,
                duplicateRegistryIds = registered.GroupBy(info => info.AgentId)
                    .Where(group => group.Count() > 1).Select(group => group.Key.ToString("D")).ToArray(),
                detachedRegistryIds = registered.Where(info => info.Agent == null)
                    .Select(info => info.AgentId.ToString("D")).ToArray(),
                agents = MergeObservations(observations, fixture?.PreviousObservations)
            }));
        }
    }

    private static ReproductionFixtureBehavior GetFixture(Mission mission)
    {
        if (mission == null) return null;
        var fixture = mission.GetMissionBehavior<ReproductionFixtureBehavior>();
        if (fixture != null) return fixture;
        fixture = new ReproductionFixtureBehavior();
        mission.AddMissionBehavior(fixture);
        return fixture;
    }

    internal static IReadOnlyList<JObject> MergeObservations(IEnumerable<object> observations,
        IDictionary<string, JObject> history)
    {
        var rows = observations.Select(JObject.FromObject).ToList();
        if (history == null) return rows;
        var currentIds = new HashSet<string>();
        foreach (var row in rows)
        {
            string id = (string)row["agentId"];
            if (id == null) continue;
            currentIds.Add(id);
            history[id] = (JObject)row.DeepClone();
        }
        foreach (var entry in history.Where(entry => !currentIds.Contains(entry.Key)))
        {
            var missing = (JObject)entry.Value.DeepClone();
            missing["lastObservedState"] = missing["state"];
            missing["lastObservedHealth"] = missing["health"];
            missing["missing"] = true;
            missing["registered"] = false;
            missing["inMission"] = false;
            missing["active"] = false;
            missing["state"] = "Missing";
            missing["health"] = null;
            missing["position"] = null;
            missing["velocity"] = null;
            rows.Add(missing);
        }
        return rows;
    }

    public sealed class RosterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.battle";
        public string Name => "reproduction_roster";
        public string Description => "Reads a player's roster and hero health for fixture restoration.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The player controller id.")
        };
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ContainerProvider.TryResolve<IPlayerManager>(out var players) ||
                !ContainerProvider.TryResolve<IObjectManager>(out var objects) ||
                !players.TryGetPlayer(args[0], out var player) ||
                !objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
                return new CoopCommandResult(false, "Player party unavailable.", "command_failed");
            var members = party.MemberRoster.GetTroopRoster()
                .OrderBy(element => element.Character.StringId).Select(element => new
                {
                    characterId = element.Character.StringId, count = element.Number,
                    wounded = element.WoundedNumber, xp = element.Xp,
                    heroHealth = element.Character.IsHero ? (int?)element.Character.HeroObject.HitPoints : null
                }).ToArray();
            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
            {
                partyId = player.MobilePartyId, leader = party.LeaderHero?.StringId, members
            }));
        }
    }

    internal static bool TryParseCamera(IReadOnlyList<string> args, out Vec3 position, out Vec3 target, out Guid subject)
    {
        position = default;
        target = default;
        subject = Guid.Empty;
        if (args.Count != 7 || !Guid.TryParse(args[6], out subject) || subject == Guid.Empty)
            return false;
        var values = new float[6];
        for (int i = 0; i < values.Length; i++)
            if (!float.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) ||
                float.IsNaN(values[i]) || float.IsInfinity(values[i]) || Math.Abs(values[i]) > 100000f)
                return false;
        position = new Vec3(values[0], values[1], values[2]);
        target = new Vec3(values[3], values[4], values[5]);
        return (position - target).LengthSquared > 0.01f;
    }

    public sealed class CameraCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.battle";
        public string Name => "reproduction_camera";
        public string Description => "Frames authority coordinates without moving any agent.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("x", "Camera world x."), new ExpectedArgs("y", "Camera world y."),
            new ExpectedArgs("z", "Camera world z."), new ExpectedArgs("target_x", "Subject world x."),
            new ExpectedArgs("target_y", "Subject world y."), new ExpectedArgs("target_z", "Subject world z."),
            new ExpectedArgs("subject_guid", "Authority subject identity, including missing receiver subjects.")
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryParseCamera(args.ToList(), out var position, out var target, out var subject))
                return new CoopCommandResult(false, "Expected finite distinct camera/target coordinates and subject GUID.", "command_failed");
            var mission = Mission.Current;
            if (mission == null || !(ScreenManager.TopScreen is MissionScreen screen) ||
                ReferenceEquals(screen.CombatCamera, null))
                return new CoopCommandResult(false, "A rendered mission is required.", "command_failed");
            var camera = GetFixture(mission);
            camera.Frame(screen, position, target, subject);
            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(camera.Observe()));
        }
    }

    public sealed class CameraStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.battle";
        public string Name => "reproduction_camera_state";
        public string Description => "Reads the selected subject and actual rendered camera.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => new CoopCommandResult(true,
            "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(
                Mission.Current?.GetMissionBehavior<ReproductionFixtureBehavior>()?.Observe() ?? new { active = false }));
    }

    public sealed class RestoreCameraCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.battle";
        public string Name => "reproduction_camera_restore";
        public string Description => "Restores the displaced mission camera.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            Mission.Current?.GetMissionBehavior<ReproductionFixtureBehavior>()?.Restore();
            return new CoopCommandResult(true, "Reproduction camera restored.");
        }
    }

    internal sealed class ReproductionFixtureBehavior : MissionBehavior
    {
        internal readonly Dictionary<Agent, CoopAgentInfo> Identities = new();
        internal readonly Dictionary<string, JObject> PreviousObservations = new();
        private MissionScreen screen;
        private Camera ownedCamera;
        private Camera previousCamera;
        private Guid subject;
        private Vec3 position;
        private Vec3 target;
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        internal void Frame(MissionScreen missionScreen, Vec3 cameraPosition, Vec3 subjectPosition, Guid subjectId)
        {
            if (ReferenceEquals(ownedCamera, null))
            {
                screen = missionScreen;
                previousCamera = screen.CustomCamera;
            }
            try
            {
                if (ReferenceEquals(ownedCamera, null))
                {
                    ownedCamera = Camera.CreateCamera();
                    ownedCamera.FillParametersFrom(screen.CombatCamera);
                }
                ownedCamera.LookAt(cameraPosition, subjectPosition, Vec3.Up);
                subject = subjectId;
                position = cameraPosition;
                target = subjectPosition;
                screen.CustomCamera = ownedCamera;
            }
            catch
            {
                Restore();
                throw;
            }
        }

        internal object Observe()
        {
            bool active = !ReferenceEquals(ownedCamera, null) &&
                ReferenceEquals(screen?.CustomCamera, ownedCamera);
            var rendered = active ? screen.CombatCamera : null;
            return new
            {
                active, subjectId = subject.ToString("D"), sampledAtUtc = DateTime.UtcNow,
                position = new { x = position.x, y = position.y, z = position.z },
                target = new { x = target.x, y = target.y, z = target.z },
                renderedPosition = ReferenceEquals(rendered, null) ? null :
                    new { x = rendered.Position.x, y = rendered.Position.y, z = rendered.Position.z },
                renderedDirection = ReferenceEquals(rendered, null) ? null :
                    new { x = rendered.Direction.x, y = rendered.Direction.y, z = rendered.Direction.z }
            };
        }

        internal void Restore()
        {
            if (ReferenceEquals(ownedCamera, null)) return;
            if (ReferenceEquals(screen?.CustomCamera, ownedCamera)) screen.CustomCamera = previousCamera;
            ownedCamera.ReleaseCamera();
            ownedCamera = null;
            previousCamera = null;
            screen = null;
        }

        public override void OnEndMission() => Restore();
        public override void OnRemoveBehavior() => Restore();
    }
}
#endif
