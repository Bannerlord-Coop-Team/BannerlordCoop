using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace Missions.Services.Network.Data
{
    /// <summary>
    /// Data Class for AiAgent
    /// </summary>
    [ProtoContract]
    public class AiAgentData
    {
        [ProtoMember(1)]
        public readonly Guid UnitId;
        [ProtoMember(2)]
        public readonly Vec3 UnitPosition;
        [ProtoMember(3)]
        public readonly string UnitIdString;
        [ProtoMember(4)]
        public readonly float UnitHealth;

        public AiAgentData(Guid unitId, Vec3 unitPosition, string unitIdString, float unitHealth)
        {
            UnitId = unitId;
            UnitPosition = unitPosition;
            UnitIdString = unitIdString;
            UnitHealth = unitHealth;
        }
    }
}