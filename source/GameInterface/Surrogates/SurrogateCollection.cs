using ProtoBuf.Meta;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.CampaignSystem.ExplainedNumber.StatExplainer;

namespace GameInterface.Surrogates;

public interface ISurrogateCollection { }

// Public so standalone flows that don't run GameInterfaceModule (the Missions test game managers, the
// legacy MissionTestMod, the serialization unit tests) can register every surrogate in one call —
// `new SurrogateCollection()` — instead of hand-listing them and referencing each surrogate type.
public class SurrogateCollection : ISurrogateCollection
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
            AddSurrogate<ItemData, ItemDataSurrogate>();

            AddSurrogate<ExplainedNumber, ExplainedNumberSurrogate>();
            AddSurrogate<ExplainedNumber.StatExplainer, StatExplainerSurrogate>();
            AddSurrogate<ExplanationLine, ExplanationLineSurrogate>();

            AddSurrogate<PropertyObject, PropertyObjectSurrogate>();

            AddSurrogate<Vec3, Vec3Surrogate>();
            AddSurrogate<Mat3, Mat3Surrogate>();
            AddSurrogate<MatrixFrame, MatrixFrameSurrogate>();
            AddSurrogate<Blow, BlowSurrogate>();
            AddSurrogate<AttackCollisionData, AttackCollisionDataSurrogate>();
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
            AddSurrogate<WeaponComponentData, WeaponComponentDataSurrogate>();
        }
    }

    private void AddSurrogate<T, TSurrogate>()
    {
        // Already serializable, return
        if (RuntimeTypeModel.Default.CanSerialize(typeof(T))) return;
        
        RuntimeTypeModel.Default.SetSurrogate<T, TSurrogate>();
    }
}
