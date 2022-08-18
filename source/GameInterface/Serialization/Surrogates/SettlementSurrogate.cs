using System;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class SettlementSurrogate
    {
        [ProtoMember(1)] 
        public readonly string StringId;

        public SettlementSurrogate(Settlement obj)
        {
            StringId = obj.StringId;
        }
        
        /// <summary>
        ///     Prepare the message data to be serialized.
        /// </summary>
        /// <param name="settlement"></param>
        /// <returns></returns>
        public static implicit operator SettlementSurrogate(Settlement obj)
        {
            if(obj == null) return null;
            return new SettlementSurrogate(obj);
        }

        /// <summary>
        ///     Retrieve the settlement sent through the game's object manager.
        /// </summary>
        /// <param name="settlementSurrogate"></param>
        /// <returns></returns>
        public static implicit operator Settlement(SettlementSurrogate surrogate)
        {
            if (surrogate == null) return null;
            return MBObjectManager.Instance?.GetObject<Settlement>(surrogate.StringId);
        }
    }
}