//using GameInterface.Serializers;
//using GameInterface.Serializers.CustomSerializers;
//using System;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.CampaignSystem.Settlements;

//namespace Coop.Mod.Serializers
//{
//    [Serializable]
//    internal class PlayerHeroLastSeenInformationSerializer : ICustomSerializer
//    {
//        private PlayerSettlementSerializer lastSeenPlace;
//        private CampaignTimeSerializer lastSeenDate;
//        private bool isSettlementNearby;

//        public PlayerHeroLastSeenInformationSerializer(Hero.HeroLastSeenInformation heroLastSeenInformation)
//        {
//            lastSeenPlace = new PlayerSettlementSerializer(heroLastSeenInformation.LastSeenPlace);
//            lastSeenDate = new Custom.CampaignTimeSerializer(heroLastSeenInformation.LastSeenDate);
//            isSettlementNearby = heroLastSeenInformation.IsNearbySettlement;
//        }

//        public object Deserialize()
//        {
//            return new Hero.HeroLastSeenInformation
//            { 
//                LastSeenPlace = (Settlement)lastSeenPlace.Deserialize(),
//                LastSeenDate = (CampaignTime)lastSeenDate.Deserialize(),
//                IsNearbySettlement = isSettlementNearby,
//            };
//        }

//        public void ResolveReferences()
//        {
//            throw new NotImplementedException();
//        }
//    }
//}