using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Save-system representation of one server-owned siege-aftermath choice. Keeping this as a
/// registered save type lets the generic campaign-behavior store preserve the whole pending
/// generation without applying gameplay effects merely because a save snapshot was requested.
/// </summary>
internal sealed class PendingAftermathSaveData
{
    [SaveableField(1)]
    internal Settlement Settlement;

    [SaveableField(2)]
    internal MobileParty LeaderParty;

    [SaveableField(3)]
    internal Hero LeaderHero;

    [SaveableField(4)]
    internal Clan PreviousOwnerClan;

    [SaveableField(5)]
    internal Dictionary<MobileParty, float> Contributions;

    [SaveableField(6)]
    internal CampaignTime ParkedAt;

    [SaveableField(7)]
    internal Clan CaptureOwnerClan;

    [SaveableField(8)]
    internal Clan CapturerClan;

    // The save system constructs this type reflectively.
    private PendingAftermathSaveData()
    {
    }

    internal PendingAftermathSaveData(Settlement settlement, MobileParty leaderParty, Hero leaderHero,
        Clan previousOwnerClan, Dictionary<MobileParty, float> contributions, CampaignTime parkedAt,
        Clan captureOwnerClan, Clan capturerClan)
    {
        Settlement = settlement;
        LeaderParty = leaderParty;
        LeaderHero = leaderHero;
        PreviousOwnerClan = previousOwnerClan;
        Contributions = contributions;
        ParkedAt = parkedAt;
        CaptureOwnerClan = captureOwnerClan;
        CapturerClan = capturerClan;
    }
}

/// <summary>
/// Bannerlord discovers <see cref="SaveableTypeDefiner"/> implementations in loaded module
/// assemblies. The high, module-specific base keeps these definitions outside TaleWorlds' ranges.
/// </summary>
public sealed class SiegeAftermathSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_177_000;

    public SiegeAftermathSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(PendingAftermathSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<PendingAftermathSaveData>));
    }
}
