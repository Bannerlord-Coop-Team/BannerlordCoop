using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;

internal class SkillObjectBuilder : IObjectBuilder
{
    public object Build()
    {
        return new SkillObject("Charm");
    }
}
