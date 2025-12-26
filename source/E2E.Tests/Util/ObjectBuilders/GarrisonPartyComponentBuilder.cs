using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class GarrisonPartyComponentBuilder : IObjectBuilder
{
    public object Build()
    {
        Settlement settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        return new GarrisonPartyComponent(settlement, new GarrisonPartyComponent.InitializationArgs());
    }
}
