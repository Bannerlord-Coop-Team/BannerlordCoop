using GameInterface.AutoSync;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents
{
    internal class LordPartyComponentSync : IAutoSync
    {
        public LordPartyComponentSync(AutoSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(LordPartyComponent), nameof(LordPartyComponent.Owner)), debug: true);

            autoSyncBuilder.AddField(AccessTools.Field(typeof(LordPartyComponent), nameof(LordPartyComponent._leader)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(LordPartyComponent), nameof(LordPartyComponent._wagePaymentLimit)));
        }
    }
}
