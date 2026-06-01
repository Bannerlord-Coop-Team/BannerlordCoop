using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkStartAttackMission : ICommand
{
    [ProtoMember(1)]
    public readonly int RandomTerrainSeed;
    [ProtoMember(2)]
    public readonly float DamageToFriendsMultiplier;
    [ProtoMember(3)]
    public readonly float DamageFromPlayerToFriendsMultiplier;

    public NetworkStartAttackMission(int randomTerrainSeed, float damageToFriendsMultiplier, float damageFromPlayerToFriendsMultiplier)
    {
        RandomTerrainSeed = randomTerrainSeed;
        DamageToFriendsMultiplier = damageToFriendsMultiplier;
        DamageFromPlayerToFriendsMultiplier = damageFromPlayerToFriendsMultiplier;
    }
}
