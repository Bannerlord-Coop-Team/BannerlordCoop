using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Clans;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(ChangeGovernorAction))]
internal class ChangeGovernorActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ChangeGovernorActionPatches>();

    [HarmonyPatch(nameof(ChangeGovernorAction.ApplyInternal))]
    [HarmonyPrefix]
    public static bool ApplyInternalPrefix(Town fortification, Hero governor)
    {
        if (ModInformation.IsServer)
        {
            if (governor == null || !governor.IsPlayerHero()) return true;
            if (governor.Clan != fortification.OwnerClan || governor.GovernorOf != null ||
                !Campaign.Current.Models.ClanPoliticsModel.CanHeroBeGovernor(governor)) return false;

            // Player governors keep their party and roles instead of teleporting to the settlement.
            var oldGovernor = fortification.Governor;
            fortification.Governor = governor;
            CampaignEventDispatcher.Instance.OnGovernorChanged(fortification, oldGovernor, governor);
            return false;
        }
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (!CoopClanPermissions.CanManageClan(fortification.OwnerClan)) return false;

        // Send message to server to manage changed governor
        var message = new GovernorChanged(fortification, governor);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }

    [HarmonyPatch(nameof(ChangeGovernorAction.ApplyGiveUpInternal))]
    [HarmonyPrefix]
    public static bool ApplyGiveUpInternalPrefix(Hero governor)
    {
        // Governor state syncs over several channels, so a removal can arrive after the
        // governorship is already cleared. Vanilla dereferences GovernorOf unguarded;
        // treat removing a non-governor as a no-op instead of re-announcing or crashing.
        if (governor?.GovernorOf == null) return false;

        if (ModInformation.IsServer) return true;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (!CoopClanPermissions.CanManageClan(governor.GovernorOf.OwnerClan)) return false;

        // Send message to server to manage removed governor
        var message = new GovernorRemoved(governor);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }
}
