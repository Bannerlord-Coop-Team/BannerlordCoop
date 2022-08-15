using System;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Models
{
    [ProtoContract]
    public class CultureSurrogate
    {
        [ProtoMember(1)] 
        public readonly String NetworkIdentifier;

        public CultureSurrogate(String networkIdentifier)
        {
            NetworkIdentifier = networkIdentifier;
        }

        /// <summary>
        ///     Prepare the message data to be serialized.
        /// </summary>
        /// <param name="culture">Culture object</param>
        /// <returns>Surrogate object</returns>
        public static implicit operator CultureSurrogate(CultureObject culture)
        {
            return new CultureSurrogate(culture.StringId);
        }
        
        /// <summary>
        ///     Retrieve the CultureObject sent through the network on the client.
        /// </summary>
        /// <param name="settlementSurrogate">Settlement surrogate</param>
        /// <returns>Culture object</returns>
        public static implicit operator CultureObject(CultureSurrogate settlementSurrogate)
        {
            return MBObjectManager.Instance.GetObject<CultureObject>(settlementSurrogate.NetworkIdentifier);
        }
    }
}