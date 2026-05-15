using ProtoBuf.Meta;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Issues;
namespace GameInterface.Surrogates;

public interface ISurrogateCollection { }

internal class SurrogateCollection : ISurrogateCollection
{
    private static object _lock = new();
    public SurrogateCollection()
    {
        lock (_lock)
        {
            AddSurrogate<Vec2, Vec2Surrogate>();
            AddSurrogate<CampaignVec2, CampaignVec2Surrogate>();
            AddSurrogate<Banner, BannerSurrogate>();
            AddSurrogate<CampaignTime, CampaignTimeSurrogate>();
            AddSurrogate<ItemModifierGroup, ItemModifierGroupSurrogate>();
            AddSurrogate<ItemModifier, ItemModifierSurrogate>();
            AddSurrogate<TextObject, TextObjectSurrogate>();
            AddSurrogate<EquipmentElement, EquipmentElementSurrogate>();
            AddSurrogate<IssueBase, IssueBaseSurrogate>();
        }
    }

    private void AddSurrogate<T, TSurrogate>()
    {
        // Already serializable, return
        if (RuntimeTypeModel.Default.CanSerialize(typeof(T))) return;
        
        RuntimeTypeModel.Default.SetSurrogate<T, TSurrogate>();
    }
}
