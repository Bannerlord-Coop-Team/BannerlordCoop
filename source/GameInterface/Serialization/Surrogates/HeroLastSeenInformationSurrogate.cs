using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Hero;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct HeroLastSeenInformationSurrogate : ISurrogate
    {
        [ProtoMember(1)]
        Settlement LastSeenPlace { get; }
        [ProtoMember(2)]
        CampaignTime LastSeenDate { get; }
        [ProtoMember(3)]
        bool IsNearbySettlement { get; }

        private HeroLastSeenInformationSurrogate(HeroLastSeenInformation heroLastSeenInformation)
        {
            LastSeenPlace = heroLastSeenInformation.LastSeenPlace;
            LastSeenDate = heroLastSeenInformation.LastSeenDate;
            IsNearbySettlement = heroLastSeenInformation.IsNearbySettlement;
        }

        private HeroLastSeenInformation Deserailize()
        {
            HeroLastSeenInformation heroLastSeenInformation = new HeroLastSeenInformation();
            heroLastSeenInformation.LastSeenPlace = LastSeenPlace;
            heroLastSeenInformation.LastSeenDate = LastSeenDate;
            heroLastSeenInformation.IsNearbySettlement = IsNearbySettlement;

            return heroLastSeenInformation;
        }

        public static implicit operator HeroLastSeenInformationSurrogate(HeroLastSeenInformation obj)
        {
            return new HeroLastSeenInformationSurrogate(obj);
        }

        public static implicit operator HeroLastSeenInformation(HeroLastSeenInformationSurrogate surrogate)
        {
            return surrogate.Deserailize();
        }
    }
}
