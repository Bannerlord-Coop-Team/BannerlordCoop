using System;
using System.Linq;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class MobilePartySurrogate
    {
        public static implicit operator MobilePartySurrogate(MobileParty obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator MobileParty(MobilePartySurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}