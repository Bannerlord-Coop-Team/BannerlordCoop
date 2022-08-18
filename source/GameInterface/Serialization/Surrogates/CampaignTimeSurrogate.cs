using System.Linq;
using System.Reflection;
using Coop.Mod.Extentions;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct CampaignTimeSurrogate
    {
        [ProtoMember(1)]
        readonly long _numTicks;

        private CampaignTimeSurrogate(CampaignTime campaignTime)
        {
            _numTicks = campaignTime.GetNumTicks();
        }

        private CampaignTime Deserialize()
        {
            var campaignTimeConstructor = typeof(CampaignTime).GetTypeInfo()
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .First();

            var campaignTime = campaignTimeConstructor.Invoke(new object[] { _numTicks });

            return (CampaignTime)campaignTime;
        }

        /// <summary>
        ///     Prepare the serialization of the CampaignTime object from the game.
        /// </summary>
        /// <param name="campaignTime"></param>
        /// <returns></returns>
        public static implicit operator CampaignTimeSurrogate(CampaignTime campaignTime)
        {
            return new CampaignTimeSurrogate(campaignTime);
        }

        /// <summary>
        ///     Retrieve the CampaignTime object from the surrogate.
        /// </summary>
        /// <param name="campaignTimeSurrogate">Surrogate object.</param>
        /// <returns>CampaignTime object.</returns>
        public static implicit operator CampaignTime(CampaignTimeSurrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}