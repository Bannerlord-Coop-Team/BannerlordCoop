using System.Linq;
using System.Reflection;
using Coop.Mod.Extentions;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract]
    public class CampaignTimeSurrogate
    {
        [ProtoMember(1)]
        readonly long _numTicks;

        private CampaignTimeSurrogate(long numTicks)
        {
            _numTicks = numTicks;
        }

        /// <summary>
        ///     Prepare the serialization of the CampaignTime object from the game.
        /// </summary>
        /// <param name="campaignTime"></param>
        /// <returns></returns>
        public static implicit operator CampaignTimeSurrogate(CampaignTime campaignTime)
        {
            return new CampaignTimeSurrogate(campaignTime.GetNumTicks());
        }

        /// <summary>
        ///     Retrieve the CampaignTime object from the surrogate.
        /// </summary>
        /// <param name="campaignTimeSurrogate">Surrogate object.</param>
        /// <returns>CampaignTime object.</returns>
        public static implicit operator CampaignTime(CampaignTimeSurrogate campaignTimeSurrogate)
        {
            var campaignTimeConstructor = typeof(CampaignTime).GetTypeInfo()
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .First();

            var campaignTime = campaignTimeConstructor.Invoke(new object[] { campaignTimeSurrogate._numTicks });

            return (CampaignTime)campaignTime;
        }
    }
}