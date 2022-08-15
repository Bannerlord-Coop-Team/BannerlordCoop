using System;
using System.Linq;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.Models
{
    [ProtoContract]
    public class KingdomSurrogate
    {
        [ProtoMember(1)] 
        public readonly String NetworkIdentifier;

        public KingdomSurrogate(String networkIdentifier)
        {
            NetworkIdentifier = networkIdentifier;
        }

        /// <summary>
        ///     Prepare the message data to be serialized.
        /// </summary>
        /// <param name="kingdom">Kingdom Object</param>
        /// <returns>Kingdom Surrogate</returns>
        public static implicit operator KingdomSurrogate(Kingdom kingdom)
        {
            return new KingdomSurrogate(kingdom.StringId);
        }

        /// <summary>
        ///     Retrieve the mobile party sent through the network on the client.
        /// </summary>
        /// <param name="kingdomSurrogate">Kingdom Surrogate</param>
        /// <returns>Kingdom object</returns>
        public static implicit operator Kingdom(KingdomSurrogate kingdomSurrogate)
        {
            return Kingdom.All.First(k => k.StringId == kingdomSurrogate.NetworkIdentifier);
        }
    }
}