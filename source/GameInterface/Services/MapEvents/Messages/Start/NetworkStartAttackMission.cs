using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;
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
    public readonly MissionInitializerRecord MissionInitializer;

    [ProtoMember(6)]
    public readonly bool IsHideout;

    [ProtoMember(7)]
    public readonly bool IsDirectAssault;

    public NetworkStartAttackMission(string mapEventId, MissionInitializerRecord missionInitializer,
        string initiatingPartyId, bool isHideout = false, bool isDirectAssault = false)
    {
        MapEventId = mapEventId;
        RandomTerrainSeed = missionInitializer.RandomTerrainSeed;
        AtmosphereOnCampaign = missionInitializer.AtmosphereOnCampaign;
        InitiatingPartyId = initiatingPartyId;
        MissionInitializer = missionInitializer;
        IsHideout = isHideout;
        IsDirectAssault = isDirectAssault;
    }
}
