using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Missions.Messages
{
    [ProtoContract]
    public readonly struct MissionJoinRequest
    {
        [ProtoMember(1)]
        public readonly Guid PlayerId;
        [ProtoMember(2)]
        public readonly Vec3 OriginalPosition;
        public MissionJoinRequest(Guid playerId, Vec3 originalPosition)
        {
            PlayerId = playerId;
            OriginalPosition = originalPosition;
        }
    }

    [ProtoContract]
    public readonly struct MissionJoinResponse
    {
        [ProtoMember(1)]
        public readonly Guid PlayerId;
        [ProtoMember(2)]
        public readonly Vec3 OriginalPosition;

        public MissionJoinResponse(Guid playerId, Vec3 originalPosition)
        {
            PlayerId = playerId;
            OriginalPosition = originalPosition;
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
