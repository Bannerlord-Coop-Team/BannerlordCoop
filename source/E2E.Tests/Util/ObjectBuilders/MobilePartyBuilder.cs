using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Library;

namespace E2E.Tests.Util.ObjectBuilders;
internal class MobilePartyBuilder : IObjectBuilder
{
    public object Build()
    {
        var partyComponent = GameObjectCreator.CreateInitializedObject<LordPartyComponent>();

        return MobileParty.CreateParty("This should not set", partyComponent, (party) =>
        {
            partyComponent.InitializeLordPartyProperties(party, Vec2.Zero, 0, null);
        });
    }

    public MobileParty BuildWithHero(Hero hero)
    {
        var componentBuilder = new LordPartyComponentBuilder();

        var partyComponent = componentBuilder.BuildWithHero(hero);

        return MobileParty.CreateParty("This should not set", partyComponent, (party) =>
        {
            partyComponent.InitializeLordPartyProperties(party, Vec2.Zero, 0, null);
        });
    }
}
