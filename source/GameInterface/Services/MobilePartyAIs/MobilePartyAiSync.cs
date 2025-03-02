using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs;
internal class MobilePartyAiSync : IAutoSync
{
    public MobilePartyAiSync(IAutoSyncBuilder autoSyncBuilder)
    {
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobilePartyAi), nameof(MobilePartyAi._mobileParty)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobilePartyAi), nameof(MobilePartyAi._isDisabled)));

        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobilePartyAi), nameof(MobilePartyAi.DefaultBehavior)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobilePartyAi), nameof(MobilePartyAi.PartyMoveMode)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobilePartyAi), nameof(MobilePartyAi.MoveTargetParty)));
    }
}
