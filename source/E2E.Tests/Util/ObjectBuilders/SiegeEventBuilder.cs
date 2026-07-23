using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace E2E.Tests.Util.ObjectBuilders;

internal class SiegeEventBuilder : IObjectBuilder
{
    public object Build()
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var party = GameObjectCreator.CreateInitializedObject<MobileParty>();

        foreach (MobileParty item in MobileParty.All)
        {
            if (item.Ai != null) continue;

            item.Ai = new MobilePartyAi(item);
        }

        return new SiegeEvent(settlement, party);
    }
}