using Common.Logging;
using Common.Util;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnGameLoaded))]
internal class BanditPartyHomeSettlementRepairPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<BanditPartyHomeSettlementRepairPatch>();

    [HarmonyPrefix]
    private static void RepairMissingHomeSettlements()
    {
        if (!ContainerProvider.TryResolve<IBanditPartyHomeSettlementRepairer>(out var repairer))
        {
            Logger.Error("Unable to resolve {Repairer}", nameof(IBanditPartyHomeSettlementRepairer));
            return;
        }

        repairer.RepairMissingHomeSettlements(MobileParty.AllBanditParties, Settlement.All);
    }
}
