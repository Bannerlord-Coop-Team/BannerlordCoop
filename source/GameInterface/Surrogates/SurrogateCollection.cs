using ProtoBuf.Meta;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using static TaleWorlds.CampaignSystem.ExplainedNumber.StatExplainer;

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
            AddSurrogate<PropertyOwner<TraitObject>, PropertyOwnerSurrogate>();
            AddSurrogate<ItemRosterElement, ItemRosterElementSurrogate>();
            AddSurrogate<TroopRosterElement, TroopRosterElementSurrogate>();

            AddSurrogate<ExplainedNumber, ExplainedNumberSurrogate>();
            AddSurrogate<ExplainedNumber.StatExplainer, StatExplainerSurrogate>();
            AddSurrogate<ExplanationLine, ExplanationLineSurrogate>();

            AddSurrogate<Vec3, Vec3Surrogate>();
            AddSurrogate<SunInformation, SunInformationSurrogate>();
            AddSurrogate<RainInformation, RainInformationSurrogate>();
            AddSurrogate<SnowInformation, SnowInformationSurrogate>();
            AddSurrogate<AmbientInformation, AmbientInformationSurrogate>();
            AddSurrogate<FogInformation, FogInformationSurrogate>();
            AddSurrogate<SkyInformation, SkyInformationSurrogate>();
            AddSurrogate<NauticalInformation, NauticalInformationSurrogate>();
            AddSurrogate<TimeInformation, TimeInformationSurrogate>();
            AddSurrogate<AreaInformation, AreaInformationSurrogate>();
            AddSurrogate<PostProcessInformation, PostProcessInformationSurrogate>();
            AddSurrogate<AtmosphereInfo, AtmosphereInfoSurrogate>();
            AddSurrogate<MissionInitializerRecord, MissionInitializerRecordSurrogate>();
        }
    }

    private void AddSurrogate<T, TSurrogate>()
    {
        // Already serializable, return
        if (RuntimeTypeModel.Default.CanSerialize(typeof(T))) return;
        
        RuntimeTypeModel.Default.SetSurrogate<T, TSurrogate>();
    }
}
