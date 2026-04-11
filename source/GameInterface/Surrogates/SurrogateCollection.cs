using ProtoBuf.Meta;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

public interface ISurrogateCollection { }

internal class SurrogateCollection : ISurrogateCollection
{
    private static object _lock = new();
    public SurrogateCollection()
    {
        lock (_lock)
        {
            if (!RuntimeTypeModel.Default.CanSerialize(typeof(Vec2)))
                RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();

            if (!RuntimeTypeModel.Default.CanSerialize(typeof(TextObject)))
                RuntimeTypeModel.Default.SetSurrogate<TextObject, TextObjectSurrogate>();

            if (!RuntimeTypeModel.Default.CanSerialize(typeof(ItemModifier)))
                RuntimeTypeModel.Default.SetSurrogate<ItemModifier, ItemModifierSurrogate>();

            if (!RuntimeTypeModel.Default.CanSerialize(typeof(ItemModifierGroup)))
                RuntimeTypeModel.Default.SetSurrogate<ItemModifierGroup, ItemModifierGroupSurrogate>();

            if (!RuntimeTypeModel.Default.CanSerialize(typeof(CampaignTime)))
                RuntimeTypeModel.Default.SetSurrogate<CampaignTime, CampaignTimeSurrogate>();

            if (!RuntimeTypeModel.Default.CanSerialize(typeof(Banner)))
                RuntimeTypeModel.Default.SetSurrogate<Banner, BannerSurrogate>();

            if (!RuntimeTypeModel.Default.CanSerialize(typeof(CampaignVec2)))
                RuntimeTypeModel.Default.SetSurrogate<CampaignVec2, CampaignVec2Surrogate>();
        }
    }
}
