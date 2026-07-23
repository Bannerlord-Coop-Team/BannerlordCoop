using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages.Lifetime;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for lifecycle of <see cref="Clan"/> objects.
/// </summary>
[HarmonyPatch]
internal class ClanLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanLifetimePatches>();

    [HarmonyPatch(typeof(DestroyClanAction), "ApplyInternal")]
    [HarmonyPrefix]
    static bool DestroyPrefix(Clan destroyedClan, int details)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(Clan));
            return false;
        }

        MessageBroker.Instance.Publish(destroyedClan, new ClanDestroyed(destroyedClan, details));

        return true;
    }
}
