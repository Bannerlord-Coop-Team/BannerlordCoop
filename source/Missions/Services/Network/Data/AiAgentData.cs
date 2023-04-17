using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace Missions.Services.Network.Data
{
    /// <summary>
    /// Data Class for AiAgent
    /// </summary>
    public class AiAgentData
    {
        public readonly Guid UnitId;
        public readonly Vec3 UnitPosition;
        public readonly string UnitIdString;
        public readonly float UnitHealth;

        public AiAgentData()
        {
        }

        public AiAgentData(Guid unitId, Vec3 unitPosition, string unitIdString, float unitHealth)
        {
            UnitId = unitId;
            UnitPosition = unitPosition;
            UnitIdString = unitIdString;
            UnitHealth = unitHealth;
        }
    }
}