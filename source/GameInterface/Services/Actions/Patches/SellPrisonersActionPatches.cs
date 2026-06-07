using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Actions;
using GameInterface.Policies;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(SellPrisonersAction))]
internal class SellPrisonersActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SellPrisonersActionPatches>();

    // Needs to be expanded in future to handle the references to Hero.MainHero and Clan.PlayerClan
    // Should be generic for all client heroes
    [HarmonyPatch(nameof(SellPrisonersAction.ApplyInternal))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
