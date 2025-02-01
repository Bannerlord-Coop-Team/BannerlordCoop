using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class HideoutBuilder : IObjectBuilder
{
    public object Build()
    {
        return new Hideout();
    }

    public Hideout BuildWithSettlement()
    {
        Settlement settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        MobileParty party = GameObjectCreator.CreateInitializedObject<MobileParty>();
        Hideout hideout = GameObjectCreator.CreateInitializedObject<Hideout>();

        PartyBase partyBase = new PartyBase(party, settlement);

        hideout._owner = partyBase;

        return hideout;
    }
}