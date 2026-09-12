using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace E2E.Tests.Util.ObjectBuilders;

internal class HeroDeveloperBuilder : IObjectBuilder
{
    public object Build()
    {
        var hero = new Hero();

        return new HeroDeveloper(hero);
    }
}