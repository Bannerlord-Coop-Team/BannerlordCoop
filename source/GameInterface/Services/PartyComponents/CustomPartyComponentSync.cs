using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents
{
    internal class CustomPartyComponentSync : IAutoSync
    {
        public CustomPartyComponentSync(AutoSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._name)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._homeSettlement)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._owner)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._customPartyBaseSpeed)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._partyMountStringId)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._partyHarnessStringId)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CustomPartyComponent), nameof(CustomPartyComponent._avoidHostileActions)));
        }
    }
}