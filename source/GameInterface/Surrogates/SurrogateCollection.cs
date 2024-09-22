using ProtoBuf.Meta;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

public interface ISurrogateCollection { }

internal class SurrogateCollection : ISurrogateCollection
{
    public SurrogateCollection()
    {
        RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();
    }
}
