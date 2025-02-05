using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace E2E.Tests.Util.ObjectBuilders;
internal class CustomPartyComponentBuilder : IObjectBuilder
{
    public object Build()
    {
        return new CustomPartyComponent();
    }
}
