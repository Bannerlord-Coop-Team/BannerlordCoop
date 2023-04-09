using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Missions.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkMissionJoinInfo : INetworkEvent
    {
        [ProtoMember(1)]
        public readonly Guid PlayerId;
        [ProtoMember(2)]
        public readonly Vec3 StartingPosition;
        [ProtoMember(3)]
        public readonly CharacterObject CharacterObject;
        [ProtoMember(4)]
        public readonly Guid[] UnitId;
        [ProtoMember(5)]
        public readonly Vec3[] UnitStartingPosition;
        [ProtoMember(6)]
        public readonly string[] UnitIdString;
        [ProtoMember(7)]
        public readonly bool IsPlayerAlive;

        public NetworkMissionJoinInfo(
            CharacterObject characterObject,
            bool isPlayerAlive, 
            Guid playerId, 
            Vec3 startingPosition, 
            Guid[] unitId, 
            Vec3[] unitStartingPosition, 
            string[] unitIdString)
        {
            CharacterObject = characterObject;
            PlayerId = playerId;
            StartingPosition = startingPosition;
            UnitId = unitId;
            UnitStartingPosition = unitStartingPosition;
            UnitIdString = unitIdString;
            IsPlayerAlive = isPlayerAlive;
        }
    }
}
