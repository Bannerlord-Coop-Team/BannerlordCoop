using Autofac;
using Common.Logging;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using ProtoBuf;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages
{
    /// <summary>
    /// External event for Join Info in Mission
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkMissionJoinInfo : INetworkEvent
    {
        private static readonly ILogger Logger = LogManager.GetLogger<NetworkMissionJoinInfo>();

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
        [ProtoMember(8)]
        public readonly Equipment Equipment;
        [ProtoMember(9)]
        public readonly float[] UnitHealthList;

        [ProtoMember(10)]
        public readonly float PlayerHealth;

        public NetworkMissionJoinInfo(
            CharacterObject characterObject, 
            bool isPlayerAlive, 
            Guid playerId, 
            Vec3 startingPosition, 
            float health, 
            Guid[] unitId, 
            Vec3[] unitStartingPosition, 
            string[] unitIdString,
            float[] unitHealthList)
        {
            CharacterObject = characterObject;
            PlayerId = playerId;
            StartingPosition = startingPosition;
            UnitId = unitId;
            UnitStartingPosition = unitStartingPosition;
            UnitIdString = unitIdString;
            IsPlayerAlive = isPlayerAlive;
            PlayerHealth = health;
            Equipment = UpdateEquipment(characterObject.Equipment);

            string[] weapons =
            {
                Equipment[0].Item?.Name.ToString(),
                Equipment[1].Item?.Name.ToString(),
                Equipment[2].Item?.Name.ToString(),
                Equipment[3].Item?.Name.ToString(),
                Equipment[4].Item?.Name.ToString(),
            };

            Logger.Debug("Packaging equipment {weapons}", weapons);

            UnitHealthList = unitHealthList;
        }

        private Equipment UpdateEquipment(Equipment inEquipment)
        {
            if (Agent.Main == null) return inEquipment;

            for (int i = 0; i < 5; i++)
            {
                MissionWeapon weapon = Agent.Main.Equipment[i];
                inEquipment[i] = new EquipmentElement(weapon.Item, weapon.ItemModifier);
            }

            return inEquipment;
        }
    }
}
