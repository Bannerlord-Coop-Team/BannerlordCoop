using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class PartyBaseBuilder : IObjectBuilder
    {
        public object Build()
        {
            MobileParty party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            
            return new PartyBase(party);
        }
    }
}
