using ProtoBuf;
using TaleWorlds.Library;

namespace GameInterface.Missions.Services.Network.Data
{
    /// <summary>
    /// Data Class for AiAgent
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class AiAgentData
    {
        [ProtoMember(1)]
        public readonly string UnitId;
        [ProtoMember(2)]
        public readonly Vec3 UnitPosition;
        [ProtoMember(3)]
        public readonly string UnitIdString;
        [ProtoMember(4)]
        public readonly float UnitHealth;

        public AiAgentData(string unitId, Vec3 unitPosition, string unitIdString, float unitHealth)
        {
            UnitId = unitId;
            UnitPosition = unitPosition;
            UnitIdString = unitIdString;
            UnitHealth = unitHealth;
        }
    }
}