//using System;
//using TaleWorlds.CampaignSystem;
//using System.Reflection;
//using Common;
//using TaleWorlds.CampaignSystem.Settlements;
//using GameInterface.Serializers;

//namespace Coop.Mod.Serializers.Custom
//{
//    [Serializable]
//    public class VillageMarketDataSerializer : ICustomSerializer
//    {
//        [NonSerialized]
//        VillageMarketData newVillageMarketData;

//        FieldInfo villageFieldInfo;
//        Guid village;

//        public VillageMarketDataSerializer(VillageMarketData villageMarketData)
//        {
//            villageFieldInfo = villageMarketData.GetType().GetField("_village", BindingFlags.NonPublic | BindingFlags.Instance);
//            Village village = (Village)villageFieldInfo.GetValue(villageMarketData);
//            this.village = CoopObjectManager.GetGuid(village);
//        }

//        public object Deserialize()
//        {
//            newVillageMarketData = new VillageMarketData(null);

//            return newVillageMarketData;
//        }

//        public void ResolveReferences()
//        {
//            Village village = (Village)CoopObjectManager.GetObject(this.village);

//            if (newVillageMarketData == null)
//            {
//                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
//            }

//            villageFieldInfo.SetValue(newVillageMarketData, village);
//        }
//    }
//}