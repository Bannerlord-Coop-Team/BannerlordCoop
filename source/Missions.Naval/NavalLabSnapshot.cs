#if DEBUG
using Newtonsoft.Json;
using System;
using TaleWorlds.Library;

namespace Missions.Naval;

// Diagnostics must not expose engine value types, whose Equals(object) can throw during JSON loop checks.
internal sealed class NavalLabVectorSnapshot
{
    [JsonProperty("x")] public float X { get; }
    [JsonProperty("y")] public float Y { get; }
    [JsonProperty("z")] public float Z { get; }

    public NavalLabVectorSnapshot(Vec3 value)
    {
        if (!value.IsValid) throw new ArgumentOutOfRangeException(nameof(value), "Non-finite diagnostic vector.");
        X = value.x;
        Y = value.y;
        Z = value.z;
    }
}

internal sealed class NavalLabFrameSnapshot
{
    [JsonProperty("origin")] public NavalLabVectorSnapshot Origin { get; }
    [JsonProperty("side")] public NavalLabVectorSnapshot Side { get; }
    [JsonProperty("forward")] public NavalLabVectorSnapshot Forward { get; }
    [JsonProperty("up")] public NavalLabVectorSnapshot Up { get; }

    public NavalLabFrameSnapshot(MatrixFrame value)
    {
        Origin = new NavalLabVectorSnapshot(value.origin);
        Side = new NavalLabVectorSnapshot(value.rotation.s);
        Forward = new NavalLabVectorSnapshot(value.rotation.f);
        Up = new NavalLabVectorSnapshot(value.rotation.u);
    }
}

internal sealed class NavalLabActionSnapshot
{
    public int Channel { get; }
    public int ActionIndex { get; }
    public string ActionType { get; }
    public float Progress { get; }
    public float Weight { get; }
    public string AnimationFlags { get; }

    public NavalLabActionSnapshot(int channel, int actionIndex, string actionType, float progress, float weight, string animationFlags)
    {
        if (float.IsNaN(progress) || float.IsInfinity(progress) || float.IsNaN(weight) || float.IsInfinity(weight))
            throw new ArgumentOutOfRangeException(nameof(progress), "Non-finite diagnostic action state.");
        Channel = channel;
        ActionIndex = actionIndex;
        ActionType = actionType;
        Progress = progress;
        Weight = weight;
        AnimationFlags = animationFlags;
    }
}

internal sealed class NavalLabShipSnapshot
{
    [JsonProperty("id")] public Guid Id { get; }
    [JsonProperty("error")] public string Error { get; }
    [JsonProperty("frame")] public NavalLabFrameSnapshot Frame { get; }
    [JsonProperty("velocity")] public NavalLabVectorSnapshot Velocity { get; }
    [JsonProperty("angularVelocity")] public NavalLabVectorSnapshot AngularVelocity { get; }
    [JsonProperty("crewMass")] public float? CrewMass { get; }
    [JsonProperty("crewWeightedPosition")] public NavalLabVectorSnapshot CrewWeightedPosition { get; }
    [JsonProperty("dynamicBody")] public bool? DynamicBody { get; }
    [JsonProperty("activeSimulation")] public bool? ActiveSimulation { get; }
    [JsonProperty("deckFrames")] public int? DeckFrames { get; }
    [JsonProperty("controllerRudder")] public float? ControllerRudder { get; }
    [JsonProperty("controllerRow")] public string ControllerRow { get; }

    public NavalLabShipSnapshot(Guid id, string error)
    {
        Id = id;
        Error = error;
    }

    public NavalLabShipSnapshot(Guid id, MatrixFrame frame, Vec3 velocity, Vec3 angularVelocity,
        float crewMass, Vec3 crewWeightedPosition, bool dynamicBody, bool activeSimulation, int deckFrames,
        float? controllerRudder = null, string controllerRow = null)
    {
        if (float.IsNaN(crewMass) || float.IsInfinity(crewMass))
            throw new ArgumentOutOfRangeException(nameof(crewMass), "Non-finite diagnostic mass.");
        if (controllerRudder.HasValue && (float.IsNaN(controllerRudder.Value) || float.IsInfinity(controllerRudder.Value)))
            throw new ArgumentOutOfRangeException(nameof(controllerRudder), "Non-finite controller input.");
        ControllerRudder = controllerRudder;
        ControllerRow = controllerRow;
        Id = id;
        Frame = new NavalLabFrameSnapshot(frame);
        Velocity = new NavalLabVectorSnapshot(velocity);
        AngularVelocity = new NavalLabVectorSnapshot(angularVelocity);
        CrewMass = crewMass;
        CrewWeightedPosition = new NavalLabVectorSnapshot(crewWeightedPosition);
        DynamicBody = dynamicBody;
        ActiveSimulation = activeSimulation;
        DeckFrames = deckFrames;
    }
}

internal sealed class NavalLabAgentSnapshot
{
    [JsonProperty("id")] public Guid Id { get; }
    [JsonProperty("error")] public string Error { get; }
    [JsonProperty("controller")] public string Controller { get; }
    [JsonProperty("nativeCaptain")] public bool NativeCaptain { get; }
    [JsonProperty("position")] public NavalLabVectorSnapshot Position { get; }
    [JsonProperty("supportLocalPosition")] public NavalLabVectorSnapshot SupportLocalPosition { get; }
    [JsonProperty("lookDirection")] public NavalLabVectorSnapshot LookDirection { get; }
    [JsonProperty("movementInput")] public NavalLabVectorSnapshot MovementInput { get; }
    [JsonProperty("health")] public float? Health { get; }
    [JsonProperty("mass")] public float? Mass { get; }
    [JsonProperty("stepped")] public string Stepped { get; }
    [JsonProperty("steppedValid")] public bool SteppedValid { get; }
    [JsonProperty("steppedFixtureSlot")] public int SteppedFixtureSlot { get; } = -1;
    [JsonProperty("navmesh")] public ulong? Navmesh { get; }

    public NavalLabAgentSnapshot(Guid id, string controller, bool captain, string error)
    {
        Id = id;
        Controller = controller;
        NativeCaptain = captain;
        Error = error;
    }

    public NavalLabAgentSnapshot(Guid id, string controller, bool captain, Vec3 position, float health,
        float mass, string stepped, bool steppedValid, int steppedFixtureSlot, ulong navmesh,
        Vec3? supportLocalPosition = null, Vec3? lookDirection = null, Vec3? movementInput = null)
        : this(id, controller, captain, null)
    {
        if (float.IsNaN(health) || float.IsInfinity(health) || float.IsNaN(mass) || float.IsInfinity(mass))
            throw new ArgumentOutOfRangeException(nameof(health), "Non-finite diagnostic health or mass.");
        Position = new NavalLabVectorSnapshot(position);
        SupportLocalPosition = supportLocalPosition.HasValue ? new NavalLabVectorSnapshot(supportLocalPosition.Value) : null;
        LookDirection = lookDirection.HasValue ? new NavalLabVectorSnapshot(lookDirection.Value) : null;
        MovementInput = movementInput.HasValue ? new NavalLabVectorSnapshot(movementInput.Value) : null;
        Health = health;
        Mass = mass;
        Stepped = stepped;
        SteppedValid = steppedValid;
        SteppedFixtureSlot = steppedFixtureSlot;
        Navmesh = navmesh;
    }
}
#endif
