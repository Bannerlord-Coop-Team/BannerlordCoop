using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkStartAttackMission : ICommand
{
    [ProtoMember(1)]
    public readonly int RandomTerrainSeed;

    [ProtoMember(2)]
    public readonly string MapEventId;

    [ProtoMember(3)]
    public readonly AtmosphereInfo AtmosphereOnCampaign;

    [ProtoMember(4)]
    public readonly string InitiatingPartyId;

    [ProtoMember(5)]
    public readonly int BattleSize;

    public NetworkStartAttackMission(string mapEventId, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign,
        string initiatingPartyId, int battleSize)
    {
        MapEventId = mapEventId;
        RandomTerrainSeed = randomTerrainSeed;
        AtmosphereOnCampaign = atmosphereOnCampaign;
        InitiatingPartyId = initiatingPartyId;
        BattleSize = battleSize;
    }
}
