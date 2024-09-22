using ProtoBuf.Meta;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

public interface ISurrogateCollection { }

internal class SurrogateCollection : ISurrogateCollection
{
    public SurrogateCollection()
    {
        if (RuntimeTypeModel.Default.CanSerialize(typeof(Vec2)) == false)
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();
    }
}
