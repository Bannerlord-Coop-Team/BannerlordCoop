using System;
using System.Linq;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class KingdomSurrogate
    {
        [ProtoMember(1)] 
        public readonly string StringId;

        public KingdomSurrogate(Kingdom obj)
        {
            StringId = obj.StringId;
        }

        /// <summary>
        ///     Prepare the message data to be serialized.
        /// </summary>
        /// <param name="kingdom">Kingdom Object</param>
        /// <returns>Kingdom Surrogate</returns>
        public static implicit operator KingdomSurrogate(Kingdom obj)
        {
            if (obj == null) return null;
            return new KingdomSurrogate(obj);
        }

        /// <summary>
        ///     Retrieve the mobile party sent through the game's object manager.
        /// </summary>
        /// <param name="kingdomSurrogate">Kingdom Surrogate</param>
        /// <returns>Kingdom object</returns>
        public static implicit operator Kingdom(KingdomSurrogate surrogate)
        {
            if (surrogate == null) return null;
            return MBObjectManager.Instance?.GetObject<Kingdom>(surrogate.StringId);
        }
    }
}