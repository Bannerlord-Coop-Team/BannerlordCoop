using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies;

/// <summary>
/// Registry for <see cref="Army"/> type
/// </summary>
internal class ArmySync : IDynamicSync
{
    public ArmySync(DynamicSyncRegistry autoSyncBuilder)
    {
        //fields

        //properties
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Army), nameof(Army.LeaderParty)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Army), nameof(Army.ArmyOwner)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Army), nameof(Army.Morale)));
    }
}
