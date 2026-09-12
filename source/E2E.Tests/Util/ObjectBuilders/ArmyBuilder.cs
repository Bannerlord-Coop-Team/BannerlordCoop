using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Util.ObjectBuilders;

internal class ArmyBuilder : IObjectBuilder
{
    public object Build()
    {
        var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
        var mobileparty = GameObjectCreator.CreateInitializedObject<MobileParty>();
        return new Army(kingdom, mobileparty, Army.ArmyTypes.Besieger);
    }
}
