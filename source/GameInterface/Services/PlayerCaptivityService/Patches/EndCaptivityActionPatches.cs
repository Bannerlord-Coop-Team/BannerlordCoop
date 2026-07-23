using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

[HarmonyPatch]
internal class EndCaptivityActionPatches
{
    [HarmonyPatch(typeof(EndCaptivityAction), nameof(EndCaptivityAction.ApplyInternal))]
    [HarmonyPrefix]
    private static bool Prefix(Hero prisoner, EndCaptivityDetail detail, Hero facilitatior, bool showNotification)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // The server is authoritative for ending captivity (AI ransoms, escapes, captor defeated, peace).
        if (ModInformation.IsServer)
        {
            // A player (client) hero needs the coop release: native only partially frees them (and not at
            // all if the captor is already gone, e.g. defeated) and never restores the deactivated coop
            // player party. Route it through the captivity handler. AI heroes use the native release.
            if (prisoner != null && prisoner.IsPlayerHero())
            {
                PlayerCaptivityLogger.Debug("EndCaptivityAction.ApplyInternal intercepted on server for player hero {HeroId}: detail={Detail} facilitator={FacilitatorId}",
                    prisoner.StringId, detail, facilitatior?.StringId);

                MessageBroker.Instance.Publish(prisoner, new PlayerCaptivityEndedByServer(prisoner, detail, facilitatior));
                return false;
            }

            return true;
        }

        // A local release of another hero is player intent, so ask the authoritative server to apply it.
        if (prisoner != Hero.MainHero)
        {
            PlayerCaptivityLogger.Debug("EndCaptivityAction.ApplyInternal intercepted for non-controlled hero {HeroId}: detail={Detail} facilitator={FacilitatorId}, publishing EndCaptivityAttempted",
                prisoner?.StringId, detail, facilitatior?.StringId);

            MessageBroker.Instance.Publish(prisoner, new EndCaptivityAttempted(prisoner, detail, facilitatior, showNotification));
            return false;
        }

        PlayerCaptivityLogger.Debug("EndCaptivityAction.ApplyInternal intercepted for {HeroId}: detail={Detail} facilitator={FacilitatorId}, publishing EndPlayerCaptivityAttempted",
            prisoner?.StringId, detail, facilitatior?.StringId);

        var message = new EndPlayerCaptivityAttempted(prisoner, detail, facilitatior);
        MessageBroker.Instance.Publish(prisoner, message);

        return false;
    }
}
