using GameInterface.DynamicSync;
using HarmonyLib;
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
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village.VillageType))); // VillageType needs to be managed or serialised
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._marketData)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._villageState)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village.VillagerPartyComponent)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._owner))); // Uses abstract method PartyBase which can't be prepared. Not sure what to do about this
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Village), nameof(Village._bound)));

        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Village), nameof(Village.Hearth)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Village), nameof(Village.LastDemandSatisfiedTime)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Village), nameof(Village.TradeTaxAccumulated)));
    }
}
