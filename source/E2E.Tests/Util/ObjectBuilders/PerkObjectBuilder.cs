using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace E2E.Tests.Util.ObjectBuilders;

internal class PerkObjectBuilder : IObjectBuilder
{
    public object Build()
    {
        string stringid = "Perk Tests";
        return new PerkObject(stringid);
    }
}
