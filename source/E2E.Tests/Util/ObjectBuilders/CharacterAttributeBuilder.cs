using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;

internal class CharacterAttributeBuilder : IObjectBuilder
{
    public object Build()
    {
        return new CharacterAttribute("intelligence");
    }
}
