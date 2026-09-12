using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(CampaignCheats))]
internal class CheatsOnClientsPatch
{
    [HarmonyPatch(nameof(CampaignCheats.CheckCheatUsage))]
    [HarmonyPrefix]
    public static bool CheckCheatUsagePrefix(ref string ErrorType)
    {
        // Server guard to prevent clients using basegame's cheats
        if (ModInformation.IsClient)
        {
            ErrorType = "Cheats are disabled on clients. Run on the server instead.";
            return false;
        }

        return true;
    }
}
