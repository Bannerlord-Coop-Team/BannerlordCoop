using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;

internal class SkillObjectBuilder : IObjectBuilder
{
    public object Build()
    {
        string stringid = "Skill Tests";
        return new SkillObject(stringid);
    }
}
