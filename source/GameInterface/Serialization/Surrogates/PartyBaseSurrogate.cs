using System;
using System.Linq;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class PartyBaseSurrogate
    {
        public static implicit operator PartyBaseSurrogate(PartyBase obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator PartyBase(PartyBaseSurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}