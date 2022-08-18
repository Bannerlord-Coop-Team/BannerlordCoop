using System;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class CultureObjectSurrogate
    {
        [ProtoMember(1)]
        public readonly string StringId;

        public CultureObjectSurrogate(CultureObject obj)
        {
            StringId = obj.StringId;
        }

        /// <summary>
        ///     Prepare the message data to be serialized.
        /// </summary>
        /// <param name="culture">Culture object</param>
        /// <returns>Surrogate object</returns>
        public static implicit operator CultureObjectSurrogate(CultureObject obj)
        {
            if(obj == null) return null;
            return new CultureObjectSurrogate(obj);
        }

        /// <summary>
        ///     Retrieve the CultureObject sent through the game's object manager.
        /// </summary>
        /// <param name="settlementSurrogate">Settlement surrogate</param>
        /// <returns>Culture object</returns>
        public static implicit operator CultureObject(CultureObjectSurrogate surrogate)
        {
            if(surrogate == null) return null;
            return MBObjectManager.Instance?.GetObject<CultureObject>(surrogate.StringId);
        }
    }
}