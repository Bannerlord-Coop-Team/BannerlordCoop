using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.HeroDevelopers;

internal class HeroDeveloperSync : IAutoSync
{
    public HeroDeveloperSync(AutoSyncRegistry AutoSyncRegistry)
    {
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(HeroDeveloper), nameof(HeroDeveloper.UnspentFocusPoints)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(HeroDeveloper), nameof(HeroDeveloper.UnspentAttributePoints)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(HeroDeveloper), nameof(HeroDeveloper.Hero)));

        AutoSyncRegistry.AddField(AccessTools.Field(typeof(HeroDeveloper), nameof(HeroDeveloper._skillXps)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(HeroDeveloper), nameof(HeroDeveloper._newFocuses)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(HeroDeveloper), nameof(HeroDeveloper._totalXp)), coalesce: true);
    }
}
