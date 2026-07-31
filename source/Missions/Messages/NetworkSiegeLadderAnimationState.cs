using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>
/// Mission host → peers (over the mesh): one siege ladder's continuous animation snapshot.
/// Discrete ladder gameplay state remains part of <see cref="NetworkSiegeMachineState"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkSiegeLadderAnimationState : IEvent
{
    [ProtoMember(1)]
    public readonly int LadderId;
    [ProtoMember(2)]
    public readonly float AnimationSpeed;
    [ProtoMember(3)]
    public readonly float AnimationProgress;
    /// <summary>SiegeLadder.LadderAnimationState.</summary>
    [ProtoMember(4)]
    public readonly int AnimationState;
    [ProtoMember(5)]
    public readonly float FallAngularSpeed;
    [ProtoMember(6)]
    public readonly MatrixFrame Frame;
    /// <summary>Authority ladder skeleton channel animation index; -1 when no animation is active.</summary>
    [ProtoMember(7)]
    public readonly int AnimationIndex;
    [ProtoMember(8)]
    public readonly int HostEpoch;

    public NetworkSiegeLadderAnimationState(
        int ladderId,
        float animationSpeed,
        float animationProgress,
        int animationState,
        float fallAngularSpeed,
        MatrixFrame frame,
        int animationIndex,
        int hostEpoch = 0)
    {
        LadderId = ladderId;
        AnimationSpeed = animationSpeed;
        AnimationProgress = animationProgress;
        AnimationState = animationState;
        FallAngularSpeed = fallAngularSpeed;
        Frame = frame;
        AnimationIndex = animationIndex;
        HostEpoch = hostEpoch;
    }
}
