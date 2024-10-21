using ProtoBuf.Meta;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

public interface ISurrogateCollection { }

internal class SurrogateCollection : ISurrogateCollection
{
    public SurrogateCollection()
    {
        if (RuntimeTypeModel.Default.CanSerialize(typeof(Vec2)) == false)
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(TextObject)) == false)
            RuntimeTypeModel.Default.SetSurrogate<TextObject, TextObjectSurrogate>();

        if (RuntimeTypeModel.Default.CanSerialize(typeof(CampaignTime)) == false)
            RuntimeTypeModel.Default.SetSurrogate<CampaignTime, CampaignTimeSurrogate>();
    }
}
