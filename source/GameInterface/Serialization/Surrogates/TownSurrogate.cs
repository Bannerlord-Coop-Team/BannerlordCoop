using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class TownSurrogate
    {
        [ProtoMember(1)]
        public readonly string StringId;

        public TownSurrogate(Town obj)
        {
            StringId = obj.StringId;
        }

        /// <summary>
        ///     Prepare the message data to be serialized.
        /// </summary>
        /// <param name="settlement"></param>
        /// <returns></returns>
        public static implicit operator TownSurrogate(Town obj)
        {
            if (obj == null) return null;
            return new TownSurrogate(obj);
        }

        /// <summary>
        ///     Retrieve the settlement sent through the game's object manager.
        /// </summary>
        /// <param name="settlementSurrogate"></param>
        /// <returns></returns>
        public static implicit operator Town(TownSurrogate surrogate)
        {
            if (surrogate == null) return null;
            return MBObjectManager.Instance?.GetObject<Town>(surrogate.StringId);
        }
    }
}
