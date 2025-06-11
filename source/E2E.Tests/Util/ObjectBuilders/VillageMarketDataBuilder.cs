using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class VillageMarketDataBuilder : IObjectBuilder
{
    public object Build()
    {
        Village village = GameObjectCreator.CreateInitializedObject<Village>();
        return new VillageMarketData(village);
    }
}