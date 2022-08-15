using System;
using System.Linq;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Serialization.Models
{
    [ProtoContract]
    public class MobilePartySurrogate
    {
        [ProtoMember(1)] 
        public readonly String NetworkIdentifier;

        public MobilePartySurrogate(String networkIdentifier)
        {
            NetworkIdentifier = networkIdentifier;
        }

        /// <summary>
        ///     Prepare the message data to be serialized.
        /// </summary>
        /// <param name="mobileParty">MobileParty object</param>
        /// <returns>MobileParty Surrogate</returns>
        public static implicit operator MobilePartySurrogate(MobileParty mobileParty)
        {
            return new MobilePartySurrogate(mobileParty.StringId);
        }

        /// <summary>
        ///     Retrieve the mobile party sent through the network on the client.
        /// </summary>
        /// <param name="mobilePartySurrogate">MobileParty Surrogate</param>
        /// <returns>MobileParty object</returns>
        public static implicit operator MobileParty(MobilePartySurrogate mobilePartySurrogate)
        {
            return MobileParty.All.First(m => m.StringId == mobilePartySurrogate.NetworkIdentifier);
        }
    }
}