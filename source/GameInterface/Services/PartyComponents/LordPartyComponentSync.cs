using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents
{
    internal class LordPartyComponentSync : IAutoSync
    {
        public LordPartyComponentSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(LordPartyComponent), nameof(LordPartyComponent.Owner)));

            autoSyncBuilder.AddField(AccessTools.Field(typeof(LordPartyComponent), nameof(LordPartyComponent._leader)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(LordPartyComponent), nameof(LordPartyComponent._wagePaymentLimit)));
        }
    }
}
