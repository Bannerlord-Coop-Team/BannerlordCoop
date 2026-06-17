using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkStartAttackMission : ICommand
{
    [ProtoMember(1)]
    public readonly int RandomTerrainSeed;

    public NetworkStartAttackMission(int randomTerrainSeed)
    {
        RandomTerrainSeed = randomTerrainSeed;
    }
}
