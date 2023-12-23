using ProtoBuf.Meta;
using TaleWorlds.Library;

namespace Coop.Core.Surrogates;

internal static class SurrogateCollection
{
    public static void AssignSurrogates()
    {
        RuntimeTypeModel.Default.Add(typeof(Vec2), false).SetSurrogate(typeof(Vec2Surrogate));
    }
}
