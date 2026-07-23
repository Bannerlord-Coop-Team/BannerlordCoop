using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Util.ObjectBuilders;
internal class MapEventPartyBuilder : IObjectBuilder
{
    public object Build()
    {
        MobileParty party = GameObjectCreator.CreateInitializedObject<MobileParty>();

        return new MapEventParty(party.Party);
    }
}