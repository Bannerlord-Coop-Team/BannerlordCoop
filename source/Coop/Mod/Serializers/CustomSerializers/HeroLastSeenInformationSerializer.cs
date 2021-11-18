using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class HeroLastSeenInformationSerializer : ICustomSerializer
    {
        private SettlementSerializer lastSeenPlace;
        private CampaignTimeSerializer lastSeenDate;
        private bool isSettlementNearby;

        public HeroLastSeenInformationSerializer(Hero.HeroLastSeenInformation heroLastSeenInformation)
        {
            lastSeenPlace = new SettlementSerializer(heroLastSeenInformation.LastSeenPlace);
            lastSeenDate = new CampaignTimeSerializer(heroLastSeenInformation.LastSeenDate);
            isSettlementNearby = heroLastSeenInformation.IsNearbySettlement;
        }

        public object Deserialize()
        {
            return new Hero.HeroLastSeenInformation
            { 
                LastSeenPlace = (Settlement)lastSeenPlace.Deserialize(),
                LastSeenDate = (CampaignTime)lastSeenDate.Deserialize(),
                IsNearbySettlement = isSettlementNearby,
            };
        }

        public void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}