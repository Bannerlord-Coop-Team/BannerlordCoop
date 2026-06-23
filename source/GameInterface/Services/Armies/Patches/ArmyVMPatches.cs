using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using HarmonyLib;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ArmyManagement;

namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch(typeof(ArmyManagementVM), nameof(ArmyManagementVM.ExecuteDone))]
internal class ArmyManagementVMExecuteDonePatch
{
    private static ILogger Logger = LogManager.GetLogger<ArmyManagementVM>();
    [HarmonyPrefix]
    static bool Prefix(ArmyManagementVM __instance)
    {
        if (MobileParty.MainParty.Army != null) return true;
        if (!MobileParty.MainParty.MapFaction.IsKingdomFaction) return true;

        var parties = __instance.PartiesInCart
            .Where(p => p.Party != MobileParty.MainParty)
            .Select(p => p.Party)
            .ToList();

        var message = new PlayerCreatedArmy(
            (Kingdom)MobileParty.MainParty.MapFaction,
            Hero.MainHero,
            Hero.MainHero.HomeSettlement,
            Army.ArmyTypes.Defender,
            parties
        );

        MessageBroker.Instance.Publish(__instance, message);

        ChangeClanInfluenceAction.Apply(Clan.PlayerClan, (float)(-__instance.TotalCost));

        __instance._onClose();
        CampaignEventDispatcher.Instance.OnArmyOverlaySetDirty();

        return false;
    }
}