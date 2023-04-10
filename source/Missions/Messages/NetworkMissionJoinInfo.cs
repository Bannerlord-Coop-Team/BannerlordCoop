using Autofac;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkMissionJoinInfo : INetworkEvent
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
        private CharacterObject _characterObject;
        private readonly IBinaryPackageFactory packageFactory;

        [ProtoMember(3)]
        private byte[] _packedCharacter;
        [ProtoMember(4)]
        public readonly Guid[] UnitId;
        [ProtoMember(5)]
        public readonly Vec3[] UnitStartingPosition;
        [ProtoMember(6)]
        public readonly string[] UnitIdString;
        [ProtoMember(7)]
        public readonly bool IsPlayerAlive;

        public Equipment Equipment
        {
            get { return UnpackEquipment(); }
            set { _packedEquipment = PackEquipment(value); }
        }
        private Equipment _equipment;

        [ProtoMember(8)]
        private byte[] _packedEquipment;

        [ProtoMember(9)]
        public readonly float[] UnitHealthList;

        [ProtoMember(10)]
        public readonly float PlayerHealth;

        public NetworkMissionJoinInfo(IBinaryPackageFactory packageFactory, CharacterObject characterObject, bool isPlayerAlive, Guid playerId, Vec3 startingPosition, float health, Guid[] unitId, Vec3[] unitStartingPosition, string[] unitIdString, float[] unitHealthList)
        {
            this.packageFactory = packageFactory;
            CharacterObject = characterObject;
            PlayerId = playerId;
            StartingPosition = startingPosition;
            UnitId = unitId;
            UnitStartingPosition = unitStartingPosition;
            UnitIdString = unitIdString;
            IsPlayerAlive = isPlayerAlive;
            PlayerHealth = health;
            Equipment = UpdateEquipment(characterObject.Equipment);
            UnitHealthList = unitHealthList;
        }

        private Equipment UpdateEquipment(Equipment inEquipment)
        {
            for(int i = 0; i < 4; i++)
            {
                MissionWeapon weapon = Agent.Main.Equipment[i];
                inEquipment[i] = new EquipmentElement(weapon.Item, weapon.ItemModifier);
            }

            return inEquipment;
        }

        private byte[] PackCharacter(CharacterObject characterObject)
        {
            var character = new CharacterObjectBinaryPackage(characterObject, packageFactory);
            character.Pack();

            return BinaryFormatterSerializer.Serialize(character);
        }

        private CharacterObject UnpackCharacter()
        {
            if (_characterObject != null) return _characterObject;

            var character = BinaryFormatterSerializer.Deserialize<CharacterObjectBinaryPackage>(_packedCharacter);

            _characterObject = character.Unpack<CharacterObject>(packageFactory);

            return _characterObject;
        }

        private byte[] PackEquipment(Equipment equipment)
        {
            var character = new EquipmentBinaryPackage(equipment, packageFactory);
            character.Pack();

            return BinaryFormatterSerializer.Serialize(character);
        }

        private Equipment UnpackEquipment()
        {
            if (_equipment != null) return _equipment;

            var character = BinaryFormatterSerializer.Deserialize<EquipmentBinaryPackage>(_packedEquipment);

            _equipment = character.Unpack<Equipment>(packageFactory);

            return _equipment;
        }
    }
}
