using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
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

        if (prisoner != Hero.MainHero)
        {
            Logger.Error("Client attempted to end captivity for a non-main hero.");
            return true;
        }

        var message = new EndPlayerCaptivityAttempted(prisoner, detail, facilitatior);
        MessageBroker.Instance.Publish(prisoner, message);

        return false;
    }
}
