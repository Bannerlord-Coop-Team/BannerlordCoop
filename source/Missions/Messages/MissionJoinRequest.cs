using ProtoBuf;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Missions.Messages
{
    [ProtoContract]
    public readonly struct MissionJoinInfo
    {
        [ProtoMember(1)]
        public readonly Guid PlayerId;
        [ProtoMember(2)]
        public readonly Vec3 StartingPosition;
        public MissionJoinInfo(Guid playerId, Vec3 startingPosition)
        {
            PlayerId = playerId;
            StartingPosition = startingPosition;
        }
    }


    // TODO create full serailizer
    [ProtoContract]
    public readonly struct SerializableCharacterObject
    {
        [ProtoMember(1)]
        public readonly Guid PlayerGuid;
        public SerializableCharacterObject(CharacterObject characterObject, Guid playerGuid)
        {
            PlayerGuid = playerGuid;
        }
    }
}
