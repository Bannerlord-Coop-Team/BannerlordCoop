using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class MobilePartyAiBuilder : IObjectBuilder
    {
        public object Build()
        {
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            return new MobilePartyAi(mobileParty);
        }
    }
}
