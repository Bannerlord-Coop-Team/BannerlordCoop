using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns;
internal class TownSync : IAutoSync
{
    public TownSync(IAutoSyncBuilder autoSyncBuilder)
    {
        // Already synced manually
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Governor)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._prosperity)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._wallLevel)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._isCastle)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._ownerClan)));
        // Not synced fields
        // autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._security)));
        // autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._loyalty)));
        // Remove Prop or auto sync
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town.BoostBuildingProcess)));
        // Remove Prop or auto sync
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._tradeTax)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town.InRebelliousState)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._governor)));
    }
}
