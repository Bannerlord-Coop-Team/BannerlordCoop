using System;
using TaleWorlds.CampaignSystem;
using System.Reflection;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class VillageMarketDataSerializer : ICustomSerializer
    {
        [NonSerialized]
        VillageMarketData newVillageMarketData;

        FieldInfo villageFieldInfo;
        Guid village;

        public VillageMarketDataSerializer(VillageMarketData villageMarketData)
        {
            villageFieldInfo = villageMarketData.GetType().GetField("_village", BindingFlags.NonPublic | BindingFlags.Instance);
            Village village = (Village)villageFieldInfo.GetValue(villageMarketData);
            this.village = CoopObjectManager.GetGuid(village);
        }

        public object Deserialize()
        {
            newVillageMarketData = new VillageMarketData(null);

            return newVillageMarketData;
        }

        public void ResolveReferenceGuids()
        {
            if (newVillageMarketData == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            villageFieldInfo.SetValue(newVillageMarketData, village);
        }
    }
}