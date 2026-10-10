using ProtoBuf;

namespace Coop.Naval.Storms.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct StormStateData
{
    [ProtoMember(1)]
    public readonly int StormType;

    [ProtoMember(2)]
    public readonly float PositionX;

    [ProtoMember(3)]
    public readonly float PositionY;

    [ProtoMember(4)]
    public readonly float Intensity;

    [ProtoMember(5)]
    public readonly float Speed;

    [ProtoMember(6)]
    public readonly long DevelopingStateFinishTicks;

    [ProtoMember(7)]
    public readonly long FinalizingStateStartTicks;

    [ProtoMember(8)]
    public readonly float DesiredMoveDirectionX;

    [ProtoMember(9)]
    public readonly float DesiredMoveDirectionY;

    [ProtoMember(10)]
    public readonly float CurrentMoveDirectionX;

    [ProtoMember(11)]
    public readonly float CurrentMoveDirectionY;

    // Flattened x, y, effect radius triples of the storm's trail used for wet weather checks
    [ProtoMember(12)]
    public readonly float[] PreviousPositionsAndRadius;

    [ProtoMember(13)]
    public readonly int NextUpdatePreviousDataArrayIndex;

    [ProtoMember(14)]
    public readonly long NextUpdateTicks;

    public StormStateData(
        int stormType,
        float positionX,
        float positionY,
        float intensity,
        float speed,
        long developingStateFinishTicks,
        long finalizingStateStartTicks,
        float desiredMoveDirectionX,
        float desiredMoveDirectionY,
        float currentMoveDirectionX,
        float currentMoveDirectionY,
        float[] previousPositionsAndRadius,
        int nextUpdatePreviousDataArrayIndex,
        long nextUpdateTicks)
    {
        StormType = stormType;
        PositionX = positionX;
        PositionY = positionY;
        Intensity = intensity;
        Speed = speed;
        DevelopingStateFinishTicks = developingStateFinishTicks;
        FinalizingStateStartTicks = finalizingStateStartTicks;
        DesiredMoveDirectionX = desiredMoveDirectionX;
        DesiredMoveDirectionY = desiredMoveDirectionY;
        CurrentMoveDirectionX = currentMoveDirectionX;
        CurrentMoveDirectionY = currentMoveDirectionY;
        PreviousPositionsAndRadius = previousPositionsAndRadius;
        NextUpdatePreviousDataArrayIndex = nextUpdatePreviousDataArrayIndex;
        NextUpdateTicks = nextUpdateTicks;
    }
}
