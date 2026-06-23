using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements;
internal class VillageSync : IAutoSync
{
    public VillageSync(AutoSyncRegistry AutoSyncRegistry)
    {
        // Fields
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village.VillageType)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._marketData)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._villageState)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village.VillagerPartyComponent)));
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._owner))); // Uses abstract method PartyBase which can't be prepared. Not sure what to do about this
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._bound)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._tradeBound)));

        // Properties
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Village), nameof(Village.Hearth)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Village), nameof(Village.LastDemandSatisfiedTime)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Village), nameof(Village.TradeTaxAccumulated)));

        // Target Methods
        AutoSyncRegistry.AddTargetMethod(typeof(Village), AccessTools.Method(typeof(VillagerPartyComponent), nameof(VillagerPartyComponent.OnFinalize)));
        AutoSyncRegistry.AddTargetMethod(typeof(Village), AccessTools.Method(typeof(VillagerPartyComponent), nameof(VillagerPartyComponent.OnInitialize)));
    }
}
