using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;

internal class SkillObjectBuilder : IObjectBuilder
{
    private static int skillCounter;

    public object Build()
    {
        return new SkillObject($"Skill Tests {++skillCounter}");
    }
}