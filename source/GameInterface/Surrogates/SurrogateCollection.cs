using ProtoBuf.Meta;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Surrogates;

/// <summary>
/// Collection of ProtoBuf surrogates
/// </summary>
public interface ISurrogateCollection { }

/// <inheritdoc cref="ISurrogateCollection"/>
internal class SurrogateCollection : ISurrogateCollection
{
    public SurrogateCollection()
    {
        RuntimeTypeModel.Default.Add(typeof(Vec2), false).SetSurrogate(typeof(Vec2Surrogate));
        RuntimeTypeModel.Default.Add(typeof(Army), false).SetSurrogate(typeof(ArmySurrogate));
        RuntimeTypeModel.Default.Add(typeof(PartyBase), false).SetSurrogate(typeof(PartyBaseSurrogate));
        RuntimeTypeModel.Default.Add(typeof(TextObject), false).SetSurrogate(typeof(TextObjectSurrogate));

    }
}
