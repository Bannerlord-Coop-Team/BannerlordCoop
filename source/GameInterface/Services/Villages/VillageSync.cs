using GameInterface.DynamicSync;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements;
internal class VillageSync : IDynamicSync
{
    public VillageSync(DynamicSyncRegistry dynamicSyncRegistry)
    {
        // Fields
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village.VillageType)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._marketData)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._villageState)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village.VillagerPartyComponent)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._owner))); // Uses abstract method PartyBase which can't be prepared. Not sure what to do about this
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._bound)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._tradeBound)));

        // Properties
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Village), nameof(Village.Hearth)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Village), nameof(Village.LastDemandSatisfiedTime)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Village), nameof(Village.TradeTaxAccumulated)));

        // Target Methods
        dynamicSyncRegistry.AddTargetMethod(typeof(Village), AccessTools.Method(typeof(VillagerPartyComponent), nameof(VillagerPartyComponent.OnFinalize)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Village), AccessTools.Method(typeof(VillagerPartyComponent), nameof(VillagerPartyComponent.OnInitialize)));
    }
}
