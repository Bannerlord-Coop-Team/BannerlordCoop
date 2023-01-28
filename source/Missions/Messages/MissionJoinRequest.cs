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
    public class MissionJoinInfo : INetworkEvent
    {
        [ProtoMember(1)]
        public readonly Guid PlayerId;
        [ProtoMember(2)]
        public readonly Vec3 StartingPosition;

        public CharacterObject CharacterObject
        {
            get { return UnpackCharacter(); }
            set { _packedCharacter = PackCharacter(value); }
        }

        [ProtoMember(3)]
        private byte[] _packedCharacter;

        [ProtoMember(4)]
        public readonly Guid[] UnitId;

        [ProtoMember(5)]
        public readonly Vec3[] UnitStartingPosition;

        [ProtoMember(6)]
        public readonly string[] UnitIdString;

        public MissionJoinInfo(CharacterObject characterObject, Guid playerId, Vec3 startingPosition, Guid[] unitId, Vec3[] unitStartingPosition, string[] unitIdString)
        {
            PlayerId = playerId;
            StartingPosition = startingPosition;
            CharacterObject = characterObject;
            UnitId = unitId;
            UnitStartingPosition = unitStartingPosition;
            UnitIdString = unitIdString;
        }

        private byte[] PackCharacter(CharacterObject characterObject)
        {
            var factory = new BinaryPackageFactory();
            var character = new CharacterObjectBinaryPackage(characterObject, factory);
            character.Pack();

            return BinaryFormatterSerializer.Serialize(character);
        }

        private CharacterObject UnpackCharacter()
        {
            var factory = new BinaryPackageFactory();
            var character = BinaryFormatterSerializer.Deserialize<CharacterObjectBinaryPackage>(_packedCharacter);
            character.BinaryPackageFactory = factory;

            return character.Unpack<CharacterObject>();
        }
    }
}
