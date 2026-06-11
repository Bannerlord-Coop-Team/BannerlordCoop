using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

[HarmonyPatch]
internal class EndCaptivityActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<EndCaptivityActionPatches>();

    [HarmonyPatch(typeof(EndCaptivityAction), nameof(EndCaptivityAction.ApplyInternal))]
    [HarmonyPrefix]
    private static bool Prefix(Hero prisoner, EndCaptivityDetail detail, Hero facilitatior)
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

        // Clients only act for their own hero; every other hero's captivity is server-driven.
        if (prisoner != Hero.MainHero)
        {
            Logger.Error("Client attempted to end captivity for non-controlled hero {HeroId}", prisoner?.StringId);
            return false;
        }

        PlayerCaptivityLogger.Debug("EndCaptivityAction.ApplyInternal intercepted for {HeroId}: detail={Detail} facilitator={FacilitatorId}, publishing EndPlayerCaptivityAttempted",
            prisoner?.StringId, detail, facilitatior?.StringId);

        var message = new EndPlayerCaptivityAttempted(prisoner, detail, facilitatior);
        MessageBroker.Instance.Publish(prisoner, message);

        return false;
    }
}
