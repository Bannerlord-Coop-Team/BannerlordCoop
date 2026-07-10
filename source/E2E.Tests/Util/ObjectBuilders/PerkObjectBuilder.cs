using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace E2E.Tests.Util.ObjectBuilders;

internal class PerkObjectBuilder : IObjectBuilder
{
    public object Build()
    {
        return new PerkObject("BowMountedArchery");
    }
}
