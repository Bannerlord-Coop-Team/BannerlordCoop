using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents;

/// <summary>
/// Registers <see cref="PatrolPartyComponent"/> fields and properties with the DynamicSync
/// system so that changes made during construction (and later mutations) are automatically
/// broadcast from the server to all clients.
///
/// Synced members:
/// - <c>_homeSettlement</c>  — the settlement this patrol party belongs to.
/// - <c>IsNaval</c>          — whether the party is a naval patrol.
/// </summary>
internal class PatrolPartyComponentSync : IDynamicSync
{
    public PatrolPartyComponentSync(DynamicSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PatrolPartyComponent), "_homeSettlement"));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PatrolPartyComponent), nameof(PatrolPartyComponent.IsNaval)));
    }
}
