using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Serialization.Surrogates
{
    // TODO implement correctly
    [ProtoContract]
    internal class MobilePartySurrogate
    {
        public static implicit operator MobilePartySurrogate(MobileParty obj)
        {
            return new MobilePartySurrogate();
        }

        public static implicit operator MobileParty(MobilePartySurrogate surrogate)
        {
            return null;
        }

        public static implicit operator MobilePartySurrogate(PartyBase obj)
        {
            return new MobilePartySurrogate();
        }

        public static implicit operator PartyBase(MobilePartySurrogate surrogate)
        {
            return null;
        }
    }
}
