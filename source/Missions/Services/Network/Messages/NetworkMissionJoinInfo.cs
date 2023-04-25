using Common.Messaging;
using Missions.Services.Network.Data;
using ProtoBuf;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network.Messages
{
    /// <summary>
    /// External event for Join Info in Mission
    /// </summary>
    [ProtoContract]
    public class NetworkMissionJoinInfo : INetworkEvent
    {
        [ProtoMember(1)]
        public readonly Guid PlayerId;
        [ProtoMember(2)]
        public readonly Vec3 StartingPosition;
        [ProtoMember(3)]
        public readonly CharacterObject CharacterObject;
        [ProtoMember(4)]
        public readonly bool IsPlayerAlive;
        [ProtoMember(5)]
        public readonly Equipment Equipment;
        [ProtoMember(6)]
        public readonly float PlayerHealth;
        [ProtoMember(7)]
        public readonly AiAgentData[] AiAgentData;

        public NetworkMissionJoinInfo()
        {
            PlayerId = default;
            StartingPosition = default;
            CharacterObject = default;
            IsPlayerAlive = false;
            Equipment = default;
            AiAgentData = Array.Empty<AiAgentData>();
        }

        public NetworkMissionJoinInfo(
            CharacterObject characterObject,
            bool isPlayerAlive,
            Guid playerId,
            Vec3 startingPosition,
            float health,
            AiAgentData[] aiAgentDatas)
        {
            CharacterObject = characterObject;
            PlayerId = playerId;
            StartingPosition = startingPosition;
            IsPlayerAlive = isPlayerAlive;
            PlayerHealth = health;
            Equipment = UpdateEquipment(characterObject.Equipment);
            AiAgentData = aiAgentDatas;
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