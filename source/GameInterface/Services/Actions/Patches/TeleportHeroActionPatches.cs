using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Actions.Messages;
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

        // Send message to server to manage teleported hero
        var message = new HeroTeleported(hero, targetSettlement, targetParty, detail);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }
}
