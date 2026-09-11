#if DEBUG
using System;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

public interface INavalMissionAdapter : IDisposable
{
    void Preflight();
    Mission Open(NavalLabManifest manifest, MissionBehavior controller, string ownControllerId);
    Agent[] Agents { get; }
    string Blocker { get; }
    object StartupDiagnostics { get; }
    void SetAuthority(bool simulate);
    void MaterializeFactoryProbe(bool electedHost, Func<bool> authorityValid);
    string CompleteDeployment();
    void Hold();
    MatrixFrame[] ReadFrames();
    bool ApplyFrames(MatrixFrame[] frames);
    void SetHelm(int ship, float rudder, bool row);
    string SetHeldHelm(int ship, bool take);
    string StartAgentControl(string kind, int ship, float value);
    void TickAgentControl(float dt);
    void CancelControls();
    object Inspect();
}

public interface INavalNativeMissionAdapter
{
    void ConfigureNative(Func<bool> authority, Action<Missions.Messages.NetworkNavalLabHelmInput> sendInput);
    Missions.Messages.NetworkNavalLabStations CreateStations();
    void ApplyStations(Missions.Messages.NetworkNavalLabStations stations);
    bool ObserveStations(Missions.Messages.NetworkNavalLabStations stations);
    void ApplyNativeInput(Missions.Messages.NetworkNavalLabHelmInput input);
    void NeutralizeNativeInput(int ship);
    Missions.Messages.NetworkNavalLabSailState[] ReadSailStates();
    void ApplySailFeedback(Missions.Messages.NetworkNavalLabFrames frames);
    void ClearSailFeedback();
    object InspectSailStatus();
    string RequestSail(int state);
    string RequestNativeHelm(Guid operationId, int ship, bool take);
    object InspectHelmStatus();
    string RequestAxesPulse(Guid operationId, int ship, float lateral, bool row, long deadlineUtcTicks);
    object InspectControlStatus();
}

public enum NavalLabMode { Activation, HeldHelm, SingleClientNative, FactoryAuthorityProbe, TwoClientNative }

public sealed class NavalLabManifest
{
    public NavalLabMode Mode { get; }
    public string InstanceId { get; }
    public Guid IncarnationId { get; }
    private readonly string[] controllers;
    private readonly Guid[] combatants;
    private readonly Guid[] ships;
    public string[] Controllers => (string[])controllers.Clone();
    public Guid[] Combatants => (Guid[])combatants.Clone();
    public Guid[] Ships => (Guid[])ships.Clone();
    public const string SceneId = "battle_terrain_opensea_northern";
    public const string HullId = "nord_medium_ship";
    public const int CrewPerShip = 5;

    public NavalLabManifest(string instanceId, Guid incarnationId, string[] controllers,
        Guid[] combatants, Guid[] ships, NavalLabMode mode = NavalLabMode.Activation)
    {
        if (incarnationId == Guid.Empty || instanceId != "naval-lab:" + incarnationId.ToString("N"))
            throw new ArgumentException("A tagged fixture incarnation is required.", nameof(instanceId));
        int participants = mode == NavalLabMode.SingleClientNative ? 1 : 2;
        if (controllers == null || controllers.Length != participants || controllers.Any(string.IsNullOrWhiteSpace)
            || controllers.Distinct().Count() != participants)
            throw new ArgumentException("The mode requires distinct participant controllers.", nameof(controllers));
        if (combatants == null || combatants.Length != participants * CrewPerShip
            || combatants.Contains(Guid.Empty) || combatants.Distinct().Count() != combatants.Length)
            throw new ArgumentException("One immutable id per fixture combatant is required.", nameof(combatants));
        if (ships == null || ships.Length != participants || ships.Contains(Guid.Empty) || ships.Distinct().Count() != participants
            || ships.Intersect(combatants).Any())
            throw new ArgumentException("One immutable ship id per participant is required.", nameof(ships));
        if (!Enum.IsDefined(typeof(NavalLabMode), mode)) throw new ArgumentException("Unknown lab mode.", nameof(mode));
        Mode = mode;
        InstanceId = instanceId;
        IncarnationId = incarnationId;
        this.controllers = (string[])controllers.Clone();
        this.combatants = (Guid[])combatants.Clone();
        this.ships = (Guid[])ships.Clone();
    }
}
#endif
