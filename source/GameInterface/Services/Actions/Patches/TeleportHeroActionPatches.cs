using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Clans;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(TeleportHeroAction))]
internal class TeleportHeroActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<TeleportHeroActionPatches>();

    [HarmonyPatch(nameof(TeleportHeroAction.ApplyInternal))]
    [HarmonyPrefix]
    public static bool ApplyInternalPrefix(Hero hero, Settlement targetSettlement, MobileParty targetParty, TeleportHeroAction.TeleportationDetail detail)
    {
        if (ModInformation.IsServer) return true;
        // Sending a member out of the player's own party is still allowed.
        if (!SharedClanPermissions.CanRecallHero(hero) &&
            !(targetSettlement != null && targetParty == null && SharedClanPermissions.CanManageHero(hero))) return false;

        // Send message to server to manage teleported hero
        var message = new HeroTeleported(hero, targetSettlement, targetParty, detail);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }
}
