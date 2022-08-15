using System;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract]
    public class SettlementSurrogate
    {
        [ProtoMember(1)] 
        public readonly String NetworkIdentifier;

        public SettlementSurrogate(String networkIdentifier)
        {
            NetworkIdentifier = networkIdentifier;
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
            return Settlement.FindFirst(s => s.StringId == settlementSurrogate.NetworkIdentifier);
        }
    }
}