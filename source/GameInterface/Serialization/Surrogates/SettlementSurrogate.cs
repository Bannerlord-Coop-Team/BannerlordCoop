using System;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract]
    public class SettlementSurrogate
    {
        [ProtoMember(1)] 
        public readonly string StringId;

        public SettlementSurrogate(string stringId)
        {
            StringId = stringId;
        }
        
        /// <summary>
        ///     Prepare the message data to be serialized.
        /// </summary>
        /// <param name="settlement"></param>
        /// <returns></returns>
        public static implicit operator SettlementSurrogate(Settlement settlement)
        {
            return new SettlementSurrogate(settlement.StringId);
        }
        
        /// <summary>
        ///     Retrieve the settlement sent through the network on the client.
        /// </summary>
        /// <param name="settlementSurrogate"></param>
        /// <returns></returns>
        public static implicit operator Settlement(SettlementSurrogate settlementSurrogate)
        {
            return MBObjectManager.Instance.GetObject<Settlement>(settlementSurrogate.StringId);
        }
    }
}