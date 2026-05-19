using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs;
internal class MobilePartyAiSync : IDynamicSync
{
    public MobilePartyAiSync(DynamicSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobilePartyAi), nameof(MobilePartyAi.AiBehaviorPartyBase)));

        // This is readonly (now done in the lifetime handler)
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobilePartyAi), nameof(MobilePartyAi._mobileParty)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobilePartyAi), nameof(MobilePartyAi._isDisabled)));

        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobilePartyAi), nameof(MobilePartyAi.DefaultBehavior)));
    }
}
